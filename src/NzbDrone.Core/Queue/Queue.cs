using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Queue
{
    public class Queue : ModelBase
    {
        public Artist Artist { get; set; }
        public Album Album { get; set; }
        public Language Language { get; set; }
        public QualityModel Quality { get; set; }
        public decimal Size { get; set; }
        public string Title { get; set; }
        public decimal Sizeleft { get; set; }
        public TimeSpan? Timeleft { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
        public string Status { get; set; }
        public string TrackedDownloadStatus { get; set; }
        public List<TrackedDownloadStatusMessage> StatusMessages { get; set; }
        public string DownloadId { get; set; }
        public RemoteAlbum RemoteAlbum { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string DownloadClient { get; set; }
        public string Indexer { get; set; }
        public string OutputPath { get; set; }
        public string ErrorMessage { get; set; }
        public bool DownloadForced { get; set; }
    }
}
