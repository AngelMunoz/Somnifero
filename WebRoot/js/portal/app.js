const roomsHub = new signalR.HubConnectionBuilder()
    .withUrl("/rooms")
    .configureLogging(signalR.LogLevel.Information)
    .build();

export class App {

    constructor() {
        this.$roomHub = roomsHub;
        this.hubs = [];
    }

    activate() {
        return this.$roomHub.start()
            .then(() => this.$roomHub.invoke("GetHubs"))
            .then(result => {
                this.hubs = [...result];
            });
    }

    deactivate() {
        return this.$roomHub.stop()
    }

    startHub(hub) {
        console.log(hub);
    }
}