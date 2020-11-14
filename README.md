# Somnifero
An experimental repo about a Giraffe monolith you can either develop with docker or without it


# Development
Using docker-compose is the usual way to get both the database and the project running

you can just run `docker-compose up` and voila the database and the container will be built for you.

> **NOTE**: If you get red squiggly lines do a dotnet restore locally so ionide can pick up your dependencies

or if you're not using docker you will need to have a database running locally then just do as you would normally

## Debug
To debug just download the docker extension for vscode, be sure your container is running (`docker-compose up`) and press <kbd>F5</kbd> you can hit breakpoints now


# Deploying
you can either do a local `dotnet publish -c Release` or use the dockerfile present in the root `docker build -t imagename:version` that will give you a fresh docker image you can deploy anywhere you want 