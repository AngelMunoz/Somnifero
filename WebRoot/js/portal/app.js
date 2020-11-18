const roomsHub = new signalR.HubConnectionBuilder()
    .withUrl("/rooms")
    .configureLogging(signalR.LogLevel.Information)
    .build();

export class App {

    constructor() {
        this.$roomHub = roomsHub;
        this.hubs = [];
        this.pagination = { page: 1, limit: 10, count: 0 }
        this.newRoomParams = { name: "", isPublic: false };
        this.activeHubs = new Map();
        this.activeTab = null;
        this.draft = "";
    }

    async activate() {
        try {
            await this.$roomHub.start()
        } catch (error) {
            console.error({ error });
        }
        try {
            await this.getRooms();
        } catch (error) {
            console.log({ error });
        }
        this.subscribeToRoom();
    }

    deactivate() {
        this.unsubscribeToRooms(this.hubs.map(room => room.name));
        return this.$roomHub.stop();
    }

    unsubscribeToRooms(rooms) {
        for (const room of rooms) {
            this.$roomHub.off(room);
        }
    }

    async addRoom(event) {
        event.preventDefault();
        try {
            await this.$roomHub.send("AddRoom", this.newRoomParams.name, this.newRoomParams.isPublic, []);
            this.newRoomParams = { name: "", isPublic: false };
            return this.getRooms();
        } catch (error) {
            console.error({ error });
        }
    }

    async startHub(hub) {
        if (this.activeHubs.has(hub)) { return; }
        this.activeHubs.set(hub.name, hub);
        this.activeTab = hub;
        await this.$roomHub.send("JoinRoom", hub.name);
    }

    setActiveTab(tab) {
        this.activeTab = { ...tab };
        this.draft = "";
    }

    async closeTab(tab) {
        await this.$roomHub.send("LeaveRoom", tab.name);
        this.activeTab = null;
        this.activeHubs.delete(tab.name);
    }

    sendMessage() {
        this.$roomHub.invoke("SendMessageToGroup", this.activeTab.name, this.draft);
    }

    getRooms() {
        return this.$roomHub.invoke("GetPublicRooms", this.pagination.page, this.pagination.limit)
            .then(({ result: { items, count } }) => {
                this.hubs = [...items];
                this.pagination.count = count;
            });
    }

    updatePublicRoomList(args) {
        console.log(args);
    }

    subscribeToRoom(room) {
        this.$roomHub.on(`SendMessage`, this.processMessage.bind(this))
    }

    processMessage(group, message) {
        const hub = this.activeHubs.get(group);
        hub.messages = [...(hub.messages || []), message];
    }
}

/**
 * Computed property for pages
 */
function pages() {
    const pages = Math.ceil(this.pagination.count / this.pagination.limit)
    return pages;
}
pages.dependencies = ['pagination.limit', 'pagination.count'];
Object.defineProperty(App.prototype, 'pages', {
    get: pages,
    enumerable: true
});