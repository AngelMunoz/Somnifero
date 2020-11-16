import { Aurelia, PLATFORM } from 'https://unpkg.com/aurelia-script@1.5.2/dist/aurelia_router.esm.min.js'
const aurelia = new Aurelia();
aurelia
    .use
    .standardConfiguration()
    .developmentLogging();

aurelia.start()
    .then(aur => aur.setRoot(PLATFORM.moduleName("/js/portal/app.js"), document.querySelector("[name=portal]")))
    .catch(error => {
        console.error(`Something went wrong starting the portal page:\n"${error.message}"`);
        UIkit.notification({
            message: 'There was an error starting this page, please reload to avoid data loss',
            status: 'danger',
            pos: 'top-right',
            timeout: 5000
        });
    });