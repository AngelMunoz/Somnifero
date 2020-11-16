

export class App {

    static get inject() {
        return ['RoomsHub'];
    }

    constructor(roomHub) {
        this.hubs = [];
        this.$roomHub = roomHub;
    }

    activate() {
        return this.$roomHub.invoke("GetHubs")
            .then(result => {
                this.hubs = [...result];
            });
    }

    startHub(hub) {
        console.log(hub);
    }
}