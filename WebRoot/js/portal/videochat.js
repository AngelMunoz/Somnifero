import { enhance } from 'https://unpkg.com/aurelia-script@1.5.2/dist/aurelia.esm.min.js'
const videoChatHub = new signalR.HubConnectionBuilder()
  .withUrl("/videochat")
  .configureLogging(signalR.LogLevel.Information)
  .build();

const peerConnections = [];
const config = {
  iceServers: [
    {
      "urls": "stun:stun.l.google.com:19302",
    },
    // { 
    //   "urls": "turn:TURN_IP?transport=tcp",
    //   "username": "TURN_USERNAME",
    //   "credential": "TURN_CREDENTIALS"
    // }
  ]
};


videoChatHub.on("OnAnswer", (id, description) => {
  peerConnections[id].setRemoteDescription(description);
});

videoChatHub.on("OnCandidate", (id, candidate) => {
  peerConnections[id].addIceCandidate(new RTCIceCandidate(candidate));
});

videoChatHub.on("OnDisconnectPeer", id => {
  peerConnections[id].close();
  delete peerConnections[id];
});

window.addEventListener('unload', async () => await videoChatHub.stop());


class VideoChatViewModel {

  constructor() {
    this.videoTag = null;
    this.stream = null
    this.selectedAudio = null;
    this.selectedVideo = null;

    this.audioDevices = [];
    this.videoDevices = [];

    this.activated();
    videoChatHub.on("OnJoined", this.onJoined.bind(this));
  }

  async onJoined(id) {
    const peerConnection = new RTCPeerConnection(config);
    peerConnections[id] = peerConnection;

    const stream = this.videoTag.srcObject;
    stream.getTracks().forEach(track => peerConnection.addTrack(track, stream));

    peerConnection.onicecandidate = event => {
      if (!event.candidate) return;
      videoChatHub.send("OnCandidate", id, event.candidate);
    };

    try {
      var sdp = await peerConnection.createOffer();
      await peerConnection.setLocalDescription(sdp);
      await videoChatHub.send("OnOffer", id, peerConnection.localDescription);
    } catch (e) {
      throw e;
      return;
    }
  };

  async activated() {
    await videoChatHub.start();
    try {
      await this.getDevices();
    } catch (e) {
      console.warn({ e });
      return;
    }
    this.selectedAudio = this.audioDevices[0];
    this.selectedVideo = this.videoDevices[0];
    try {
      await this.getStream();
    } catch (e) {
      console.warn({ e });
      return;
    }
  }

  async getDevices() {
    try {
      var devices = await navigator.mediaDevices.enumerateDevices();
    } catch (e) {
      throw e;
    }
    for (const devInfo of devices) {
      if (devInfo.kind === 'audioinput') {
        this.audioDevices = [...this.audioDevices, { label: devInfo.label, id: devInfo.deviceId }]
      }
      if (devInfo.kind === 'videoinput') {
        this.videoDevices = [...this.videoDevices, { label: devInfo.label, id: devInfo.deviceId }]
      }
    }
  }

  async getStream() {
    if (this.stream) {
      for (const track of this.stream.getTracks()) { track.stop(); }
    }
    const constraints = {
      audio: { deviceId: this.selectedAudio.id ? { exact: this.selectedAudio.id } : undefined },
      video: { deviceId: this.selectedVideo.id ? { exact: this.selectedVideo.id } : undefined }
    };
    try {
      this.stream = await navigator.mediaDevices.getUserMedia(constraints);
    } catch (e) {
      throw e;
    }
    this.videoTag.srcObject = this.stream;
    await videoChatHub.send("OnBroadcaster");
  }

  onAudioSourceChanged() {
    return this.getStream();
  }

  onVideoSourceChanged() {
    return this.getStream();
  }

}

enhance({
  host: document.querySelector("[name=broadcast]"),
  root: VideoChatViewModel
})