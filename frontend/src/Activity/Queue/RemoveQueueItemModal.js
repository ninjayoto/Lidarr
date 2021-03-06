import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import Button from 'Components/Link/Button';
import Modal from 'Components/Modal/Modal';
import FormGroup from 'Components/Form/FormGroup';
import FormLabel from 'Components/Form/FormLabel';
import FormInputGroup from 'Components/Form/FormInputGroup';
import ModalContent from 'Components/Modal/ModalContent';
import ModalHeader from 'Components/Modal/ModalHeader';
import ModalBody from 'Components/Modal/ModalBody';
import ModalFooter from 'Components/Modal/ModalFooter';
import styles from './RemoveQueueItemModal.css';

class RemoveQueueItemModal extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      blacklist: false,
      skipredownload: false
    };
  }

  //
  // Listeners

  onBlacklistChange = ({ value }) => {
    this.setState({ blacklist: value });
  }

  onSkipReDownloadChange = ({ value }) => {
    this.setState({ skipredownload: value });
  }

  onRemoveQueueItemConfirmed = () => {
    const blacklist = this.state.blacklist;
    const skipredownload = this.state.skipredownload;

    this.setState({
      blacklist: false,
      skipredownload: false
    });
    this.props.onRemovePress(blacklist, skipredownload);
  }

  onModalClose = () => {
    this.setState({
      blacklist: false,
      skipredownload: false
    });
    this.props.onModalClose();
  }

  //
  // Render

  render() {
    const {
      isOpen,
      sourceTitle
    } = this.props;

    const blacklist = this.state.blacklist;
    const skipredownload = this.state.skipredownload;

    return (
      <Modal
        isOpen={isOpen}
        size={sizes.MEDIUM}
        onModalClose={this.onModalClose}
      >
        <ModalContent
          onModalClose={this.onModalClose}
        >
          <ModalHeader>
            Remove - {sourceTitle}
          </ModalHeader>

          <ModalBody>
            <div className={styles.message}>
              Are you sure you want to remove '{sourceTitle}' from the queue?
            </div>

            <FormGroup>
              <FormLabel>Blacklist Release</FormLabel>
              <FormInputGroup
                type={inputTypes.CHECK}
                name="blacklist"
                value={blacklist}
                helpText="Prevents Lidarr from automatically grabbing this release again"
                onChange={this.onBlacklistChange}
              />
            </FormGroup>

            {
              blacklist &&
                <FormGroup>
                  <FormLabel>Skip Redownload</FormLabel>
                  <FormInputGroup
                    type={inputTypes.CHECK}
                    name="skipredownload"
                    value={skipredownload}
                    helpText="Prevents Lidarr from trying download an alternative release for this item"
                    onChange={this.onSkipReDownloadChange}
                  />
                </FormGroup>
            }

          </ModalBody>

          <ModalFooter>
            <Button onPress={this.onModalClose}>
              Close
            </Button>

            <Button
              kind={kinds.DANGER}
              onPress={this.onRemoveQueueItemConfirmed}
            >
              Remove
            </Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    );
  }
}

RemoveQueueItemModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  sourceTitle: PropTypes.string.isRequired,
  onRemovePress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default RemoveQueueItemModal;
