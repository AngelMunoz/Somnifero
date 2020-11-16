import { Aurelia, PLATFORM } from 'https://unpkg.com/aurelia-script@1.5.2/dist/aurelia_router.esm.min.js'
const aurelia = new Aurelia();
aurelia
    .use
    .standardConfiguration()
    .developmentLogging();

const roomsHub = new signalR.HubConnectionBuilder()
    .withUrl("/rooms")
    .configureLogging(signalR.LogLevel.Information)
    .build();

Promise.all([aurelia.start(), roomsHub.start()])
    .then(([aur]) => {
        aur
            .container
            .registerSingleton("RoomsHub", function RoomsHub() {
                return roomsHub;
            });
        return aur.setRoot(PLATFORM.moduleName("/js/portal/app.js"), document.querySelector("[name=portal]"));
    })
    .catch(error => {
        console.error(`Something went wrong starting the portal page:\n"${error.message}"`);
        UIkit.notification({
            message: 'There was an error starting this page, please reload to avoid data loss',
            status: 'danger',
            pos: 'top-right',
            timeout: 5000
        });
    });