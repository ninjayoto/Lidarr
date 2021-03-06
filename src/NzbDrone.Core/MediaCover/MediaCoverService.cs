using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.MediaCover
{
    public interface IMapCoversToLocal
    {
        void ConvertToLocalUrls(int entityId, MediaCoverEntity coverEntity, IEnumerable<MediaCover> covers);
        string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes mediaCoverTypes, int? height = null);
    }

    public class MediaCoverService :
        IHandleAsync<ArtistUpdatedEvent>,
        IHandleAsync<ArtistDeletedEvent>,
        IMapCoversToLocal
    {
        private readonly IImageResizer _resizer;
        private readonly IAlbumService _albumService;
        private readonly IHttpClient _httpClient;
        private readonly IDiskProvider _diskProvider;
        private readonly ICoverExistsSpecification _coverExistsSpecification;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        private readonly string _coverRootFolder;

        public MediaCoverService(IImageResizer resizer,
                                 IAlbumService albumService,     
                                 IHttpClient httpClient,
                                 IDiskProvider diskProvider,
                                 IAppFolderInfo appFolderInfo,
                                 ICoverExistsSpecification coverExistsSpecification,
                                 IConfigFileProvider configFileProvider,
                                 IEventAggregator eventAggregator,
                                 Logger logger)
        {
            _resizer = resizer;
            _albumService = albumService;
            _httpClient = httpClient;
            _diskProvider = diskProvider;
            _coverExistsSpecification = coverExistsSpecification;
            _configFileProvider = configFileProvider;
            _eventAggregator = eventAggregator;
            _logger = logger;

            _coverRootFolder = appFolderInfo.GetMediaCoverPath();
        }

        public string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes coverTypes, int? height = null)
        {
            var heightSuffix = height.HasValue ? "-" + height.ToString() : "";

            if (coverEntity == MediaCoverEntity.Album)
            {
                return Path.Combine(GetAlbumCoverPath(entityId), coverTypes.ToString().ToLower() + heightSuffix + ".jpg");
            }
            else
            {
                return Path.Combine(GetArtistCoverPath(entityId), coverTypes.ToString().ToLower() + heightSuffix + ".jpg");
            }
        }

        public void ConvertToLocalUrls(int entityId, MediaCoverEntity coverEntity, IEnumerable<MediaCover> covers)
        {
            foreach (var mediaCover in covers)
            {
                var filePath = GetCoverPath(entityId, coverEntity, mediaCover.CoverType, null);

                if (coverEntity == MediaCoverEntity.Album)
                {
                    mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/Albums/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + ".jpg";
                }
                else
                {
                    mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + ".jpg";
                }

                if (_diskProvider.FileExists(filePath))
                {
                    var lastWrite = _diskProvider.FileGetLastWrite(filePath);
                    mediaCover.Url += "?lastWrite=" + lastWrite.Ticks;
                }
            }
        }

        private string GetArtistCoverPath(int artistId)
        {
            return Path.Combine(_coverRootFolder, artistId.ToString());
        }

        private string GetAlbumCoverPath(int albumId)
        {
            return Path.Combine(_coverRootFolder, "Albums", albumId.ToString());
        }

        private void EnsureArtistCovers(Artist artist)
        {
            foreach (var cover in artist.Metadata.Value.Images)
            {
                var fileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType);
                var alreadyExists = false;
                
                try
                {
                    var lastModifiedServer = GetCoverModifiedDate(cover.Url);

                    alreadyExists = _coverExistsSpecification.AlreadyExists(lastModifiedServer, fileName);

                    if (!alreadyExists)
                    {
                        DownloadCover(artist, cover, lastModifiedServer);
                    }
                }
                catch (WebException e)
                {
                    _logger.Warn("Couldn't download media cover for {0}. {1}", artist, e.Message);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't download media cover for {0}", artist);
                }

                EnsureResizedCovers(artist, cover, !alreadyExists);
            }
        }

        private void EnsureAlbumCovers(Album album)
        {
            foreach (var cover in album.Images.Where(e => e.CoverType == MediaCoverTypes.Cover))
            {
                var fileName = GetCoverPath(album.Id, MediaCoverEntity.Album, cover.CoverType, null);
                var alreadyExists = false;
                try
                {
                    var lastModifiedServer = GetCoverModifiedDate(cover.Url);
                    alreadyExists = _coverExistsSpecification.AlreadyExists(lastModifiedServer, fileName);
                    if (!alreadyExists)
                    {
                        DownloadAlbumCover(album, cover, lastModifiedServer);
                    }
                }
                catch (WebException e)
                {
                    _logger.Warn("Couldn't download media cover for {0}. {1}", album, e.Message);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't download media cover for {0}", album);
                }

                EnsureResizedAlbumCovers(album, cover, !alreadyExists);
            }
        }

        private void DownloadCover(Artist artist, MediaCover cover, DateTime lastModified)
        {
            var fileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType);

            _logger.Info("Downloading {0} for {1} {2}", cover.CoverType, artist, cover.Url);
            _httpClient.DownloadFile(cover.Url, fileName);

            try
            {
                _diskProvider.FileSetLastWriteTime(fileName, lastModified);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to set modified date for {0} image for artist {1}", cover.CoverType, artist);
            }
        }

        private void DownloadAlbumCover(Album album, MediaCover cover, DateTime lastModified)
        {
            var fileName = GetCoverPath(album.Id, MediaCoverEntity.Album, cover.CoverType, null);

            _logger.Info("Downloading {0} for {1} {2}", cover.CoverType, album, cover.Url);
            _httpClient.DownloadFile(cover.Url, fileName);

            try
            {
                _diskProvider.FileSetLastWriteTime(fileName, lastModified);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to set modified date for {0} image for album {1}", cover.CoverType, album);
            }
        }

        private void EnsureResizedCovers(Artist artist, MediaCover cover, bool forceResize, Album album = null)
        {
            int[] heights = GetDefaultHeights(cover.CoverType);

            foreach (var height in heights)
            {
                var mainFileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType);
                var resizeFileName = GetCoverPath(artist.Id, MediaCoverEntity.Artist, cover.CoverType, height);

                if (forceResize || !_diskProvider.FileExists(resizeFileName) || _diskProvider.GetFileSize(resizeFileName) == 0)
                {
                    _logger.Debug("Resizing {0}-{1} for {2}", cover.CoverType, height, artist);

                    try
                    {
                        _resizer.Resize(mainFileName, resizeFileName, height);
                    }
                    catch
                    {
                        _logger.Debug("Couldn't resize media cover {0}-{1} for artist {2}, using full size image instead.", cover.CoverType, height, artist);
                    }
                }
            }
        }

        private void EnsureResizedAlbumCovers(Album album, MediaCover cover, bool forceResize)
        {
            int[] heights = GetDefaultHeights(cover.CoverType);

            foreach (var height in heights)
            {
                var mainFileName = GetCoverPath(album.Id, MediaCoverEntity.Album, cover.CoverType, null);
                var resizeFileName = GetCoverPath(album.Id, MediaCoverEntity.Album, cover.CoverType, height);

                if (forceResize || !_diskProvider.FileExists(resizeFileName) || _diskProvider.GetFileSize(resizeFileName) == 0)
                {
                    _logger.Debug("Resizing {0}-{1} for {2}", cover.CoverType, height, album);
                    
                    try
                    {
                        _resizer.Resize(mainFileName, resizeFileName, height);
                    }
                    catch
                    {
                        _logger.Debug("Couldn't resize media cover {0}-{1} for album {2}, using full size image instead.", cover.CoverType, height, album);
                    }
                }
            }
        }

        private DateTime GetCoverModifiedDate(string url)
        {
            var lastModifiedServer = DateTime.Now;

            var headers = _httpClient.Head(new HttpRequest(url)).Headers;

            if (headers.LastModified.HasValue)
            {
                lastModifiedServer = headers.LastModified.Value;
            }

            return lastModifiedServer;
        }

        private int[] GetDefaultHeights(MediaCoverTypes coverType)
        {
            switch (coverType)
            {
                default:
                    return new int[] { };

                case MediaCoverTypes.Poster:
                case MediaCoverTypes.Disc:
                case MediaCoverTypes.Logo:
                case MediaCoverTypes.Headshot:
                    return new[] { 500, 250 };

                case MediaCoverTypes.Banner:
                    return new[] { 70, 35 };

                case MediaCoverTypes.Fanart:
                case MediaCoverTypes.Screenshot:
                    return new[] { 360, 180 };
                case MediaCoverTypes.Cover:
                    return new[] { 250 };
            }
        }

        public void HandleAsync(ArtistUpdatedEvent message)
        {
            EnsureArtistCovers(message.Artist);

            var albums = _albumService.GetAlbumsByArtist(message.Artist.Id);
            foreach (Album album in albums)
            {
                EnsureAlbumCovers(album);
            }

            _eventAggregator.PublishEvent(new MediaCoversUpdatedEvent(message.Artist));
        }

        public void HandleAsync(ArtistDeletedEvent message)
        {
            var path = GetArtistCoverPath(message.Artist.Id);
            if (_diskProvider.FolderExists(path))
            {
                _diskProvider.DeleteFolder(path, true);
            }
        }

    }
}
