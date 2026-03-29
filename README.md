<div align="center">
</div>

<h1 align="center">Pekora/Project X  (latest version from 30 AUGUST 2025)</h1>
<p align="center">Pekora is roblox revival, i decided to leak this because chloe is an big racist and a femboy.</p>
<p align="center">Also please do not contact me on trying to get the source setup. there are many guides on how to and if you read this guide properly you shouldn't need help.</p>

<div align="center">

[![Go](https://img.shields.io/badge/GoLang-blue?logo=go)](https://github.com/tooblewtf/bloxd)
[![Dotnet 6](https://img.shields.io/badge/.NET-6.0.0-purple?logo=dotnet)](https://github.com/tooble.wtf/bloxd)
[![Node.js](https://img.shields.io/badge/Node.JS-24.3.0-green?logo=nodedotjs)](https://github.com/tooblewtf/bloxd)
[![trklzz](https://img.shields.io/badge/LEAKED_BY-tooble/trklzz-red?logo=github)](https://github.com/tooblewtf)

Pekora is a heavily modified and skidded economy simulator source lmao

pekora discord bot : https://github.com/tooblewtf/pekora-discord-bot
pekora clients : https://github.com/tooblewtf/pekora-client

ALL LEAKED BY TOOBLE / TOOBLE TARAFINDAN LEAKLENDI

</div>

# WARNING

Please do not re-host this to the public. we know it is a source code that can start your own revival but re-hosting is unrecommended for reasons.
This repository is only created for leak this shitty ass revival and how they work.
If you want to build your own roblox please DON'T MAKE A REVIVAL WITH A SRC BUILD YOUR OWN.

# HOW TO SETUP

## things you need

- <a href="https://nodejs.org/dist/v18.16.1/node-v18.16.1-x64.msi">Node.js</a>, *to run the renderer/build panel*
- <a href="https://sbp.enterprisedb.com/getfile.jsp?fileid=1258627">PostgreSQL</a>, *for the database*
- <a href="https://builds.dotnet.microsoft.com/dotnet/Sdk/6.0.412/dotnet-sdk-6.0.412-win-x64.exe">.NET 6.0</a>, *to run the website*
- <a href="https://go.dev/dl/go1.20.6.windows-amd64.msi">Go</a>, *for asset validation*
- <a href="https://github.com/redis-windows/redis-windows/releases/download/8.6.2/Redis-8.6.2-Windows-x64-cygwin-with-Service.zip">Redis Server</a> *important*

## requirements
- at least Windows 10, Linux is untested as my server is a Windows machine. You should use Wine to run everything if you are using linux (or if running a Debian vps, you can use Proxmox and run a windows VM.)
- a 10 character long domain that supports both HTTP and HTTPS
- knowledge on how things like this work (you should have at least some experience with websites and coding to be able to host this. it's really not hard to set up if you know what you're doing.)

1. Create a PG user, DB, and create a file called `"config.json"` in `services/api`. Put this in it (replacing the DB, User, and Pass with your credentials):

    ```json
    {
        "knex": {
            "client": "pg",
            "connection": {
                "host": "127.0.0.1",
                "user": "postgres",
                "password": "postgres",
                "database": "db_name_here"
            }
        }
    }
    ```

2. Install nodejs, go (lang), and dotnet 6. Go into the `services/api` directory in a terminal, run:

    ```bash
    npm i
    npx knex migrate:latest
    ```

3. Go into the `services/Roblox/Roblox.Website` folder and rename `appsettings.example.json` to `appsettings.json`. Put in your DB info and any other configurable things. Also make sure to edit the "Directories" stuff (change `/home/my_username/source-code/` to the exact path of the unzipped source code, i.e. the path this README file is in).

4. Go into the `services/Roblox/Roblox.Website` folder in a terminal, and run:

    ```bash
    dotnet run
    ```

    If everything is successful, you should be able to visit the site at `http://localhost:5000/`.

5. Start up the admin service by opening a new terminal, going into the `services/admin` folder, and running:

    ```bash
    npm i
    npm run dev
    ```

6. Open `services/2016-roblox-main/docs/get-started.md` and follow the guide for setting up 2016-roblox (this is the frontend). You should change the:

    ```
    https://{0}.roblox.com{1}
    ```

    API format to:

    ```
    http://localhost:5000/apisite/{0}{1}
    ```

7. Register an account, then copy your user id and replace the `"12"` in `"OwnerUserId"` (inside appsettings) with your user id. `ctrl+c` the `dotnet run` command to close it, then run it again to start the site back up. You should now be able to go to:

    ```
    http://localhost:5000/admin/
    ```

    for admin stuff.

8. In order to upload things, you will have to start up the "asset validation service". You can do this by going into `services/AssetValidationServiceV2` in a terminal and running:

    ```bash
    go run main.go
    ```

Note that the `game-server` program will probably need a lot of edits to actually work as a game service and/or render service.
