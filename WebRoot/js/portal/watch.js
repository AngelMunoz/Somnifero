import { enhance } from 'https://unpkg.com/aurelia-script@1.5.2/dist/aurelia.esm.min.js'
const videoChatHub = new signalR.HubConnectionBuilder()
  .withUrl("/videochat")
  .configureLogging(signalR.LogLevel.Information)
  .build();
let peerConnection;
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



class WatchBroadcastViewModel {

  constructor() {
    this.videoTag = null;
    this.muted = true;
    this.activated();
    videoChatHub.on("OnBroadcaster", async () => await videoChatHub.send("OnClientJoined"));
    videoChatHub.on("OnDisconnectPeer", () => peerConnection.close());
    videoChatHub.on("OnOffer", this.onOffer.bind(this));
    videoChatHub.on("OnCandidate", this.onCandidate.bind(this));
  }

  async onOffer(id, description) {
    peerConnection = new RTCPeerConnection(config);
    peerConnection.ontrack = event => {
      this.videoTag.srcObject = event.streams[0];
    };
    peerConnection.onicecandidate = event => {
      if (!event.candidate) return;
      videoChatHub.send("OnCandidate", id, event.candidate);
    };
    try {
      await peerConnection.setRemoteDescription(description)
      const sdp = await peerConnection.createAnswer();
      await peerConnection.setLocalDescription(sdp)
      await videoChatHub.send("OnAnswer", id, peerConnection.localDescription);
    } catch (e) {
      console.warn({ e });
      throw e;
    }
  }

  async onCandidate(id, candidate) {
    try {
      await peerConnection.addIceCandidate(new RTCIceCandidate(candidate))
    } catch (e) {
      console.warn({ e });
      throw e;
    }
  }

  async activated() {
    try {
      await videoChatHub.start();
      await videoChatHub.send("OnClientJoined");
    } catch (e) {
      console.warn({ e });
      return;
    }
  }

}

enhance({
  host: document.querySelector("[name=videochat]"),
  root: WatchBroadcastViewModel
})