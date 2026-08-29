# auth-app

A small React app with two pages, **Sign Up** and **Sign In**, styled with Bootstrap
and talking to the `auth-api` .NET Web API.

- `/signup` - email, password, confirm password
- `/signin` - email, password
- `/` - redirects to `/signin`

---

# Part 1 - Running with Docker (step by step)

This section assumes you have never used Docker before. Run the commands in
order, and read the "What just happened" note after each one. Every command is
run from the `auth-app` folder unless it says otherwise.

## What we are building

Two containers that talk to each other:

```
   Your browser
        |
        |  http://localhost:8080
        v
+----------------------+          +----------------------+
|   auth-app           |  /api/   |   auth-api           |
|   (nginx + React)    |--------->|   (.NET Web API)     |
|   port 80            |          |   port 8080          |
+----------------------+          +----------------------+
             both joined to the network "auth-net"
```

The browser only ever talks to **auth-app**. When the React code calls
`/api/auth/signin`, nginx forwards that request on to **auth-api**. The browser
never contacts the API directly.

## Step 0 - Check Docker is running

```bash
docker --version
```

You should see something like `Docker version 29.7.2`. If you instead get
"command not found" or "cannot connect to the Docker daemon", start Docker
Desktop and wait for the whale icon to stop animating.

## Step 1 - Create the network

```bash
docker network create auth-net
```

**What just happened:** you made a private network for your containers.

**Why this matters:** containers find each other by *name*, but only on a
network you create yourself. On Docker's built-in `bridge` network, name
lookup does not work, and nginx would fail to find `auth-api`. This one command
prevents a whole category of confusing errors later.

If you have run this before you will see `network with name auth-net already
exists`. That is harmless - carry on.

## Step 2 - Build the React image

```bash
docker build -t auth-app:latest .
```

**What just happened:** Docker read the [Dockerfile](Dockerfile) and produced an
image - a frozen snapshot of your app, ready to run.

Reading the command:

| Part | Meaning |
| --- | --- |
| `docker build` | Build an image from a Dockerfile |
| `-t auth-app:latest` | **t**ag it with the name `auth-app`, version `latest` |
| `.` | The build context: send this folder to Docker |

The first build takes 1-2 minutes because it downloads the base images. Later
builds are much faster because Docker reuses cached layers.

The build has two stages. Stage 1 uses Node to run `npm run build` and turn your
`.jsx` files into plain HTML, CSS and JavaScript. Stage 2 throws all of Node
away and copies only that finished output into a tiny nginx web server. That is
why the result is about 96 MB rather than 1.5 GB.

Check it exists:

```bash
docker images auth-app
```

## Step 3 - Start the API container first

The API needs to be running before the UI is useful. From the **auth-api**
folder, build it if you have not already:

```bash
docker build -t auth-api:latest .
```

Then start it (PowerShell - the backtick continues the line):

```powershell
docker run -d `
  --name auth-api `
  --network auth-net `
  -p 8081:8080 `
  -e ASPNETCORE_ENVIRONMENT=Development `
  -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=AuthDB;User Id=sa;Password=StrongPassword@123;TrustServerCertificate=True;" `
  auth-api:latest
```

In Git Bash use `\` instead of a backtick at the end of each line.

Reading the flags:

| Flag | Meaning |
| --- | --- |
| `-d` | **D**etached: run in the background and give you your prompt back |
| `--name auth-api` | Name the container. nginx looks it up by this exact name |
| `--network auth-net` | Join the network from Step 1 |
| `-p 8081:8080` | Publish **host** port 8081 to **container** port 8080 |
| `-e KEY=value` | Set an environment variable inside the container |

`host.docker.internal` in the connection string is a special name meaning "the
Windows machine running Docker", which is how a container reaches SQL Server
installed on your PC.

Confirm the API is up by opening <http://localhost:8081/swagger>.

## Step 4 - Start the React container

```bash
docker run -d --name auth-app --network auth-net -p 8080:80 auth-app:latest
```

**What just happened:** you turned the image from Step 2 into a running
container. `-p 8080:80` means "anything I send to port 8080 on my PC, hand to
port 80 inside the container", which is where nginx is listening.

## Step 5 - Open the app

<http://localhost:8080/signin>

Create an account on the Sign Up page, then sign in with it.

---

## Everyday commands

```bash
docker ps
```
List running containers, including which networks they are on. Add `-a` to
include stopped ones.

```bash
docker logs auth-app
```
Show that container's output. This is the first place to look when something
misbehaves. Add `-f` to follow live, and press Ctrl+C to stop watching.

```bash
docker stop auth-app
docker start auth-app
```
Stop and restart a container without deleting it.

```bash
docker rm -f auth-app
```
Delete the container. `-f` forces it even while running. The *image* is
untouched, so you can always create a new container from it.

```bash
docker exec -it auth-app sh
```
Open a shell **inside** the running container to look around. Try
`ls /usr/share/nginx/html` to see your built files. Type `exit` to leave.

## After you change your code

A container does **not** see edits to your source files. The image is a
snapshot, so you must build a new one and replace the container:

```bash
docker rm -f auth-app
docker build -t auth-app:latest .
docker run -d --name auth-app --network auth-net -p 8080:80 auth-app:latest
```

Forgetting this is the single most common beginner confusion: you edit a file,
refresh the browser, and nothing changes. For everyday coding use `npm start`
instead (Part 2) - Docker is for shipping the finished result.

---

## Troubleshooting

**`port is already allocated`**

Something else is using that host port. Either stop it, or pick a different
host port - only the left-hand number needs to change:

```bash
docker run -d --name auth-app --network auth-net -p 8091:80 auth-app:latest
```

Then browse to <http://localhost:8091/signin>.

**`The container name "/auth-app" is already in use`**

A container with that name already exists, running or not. Delete it first:

```bash
docker rm -f auth-app
```

**`failed to resolve source metadata` / `no such host` during build**

Docker could not reach Docker Hub to download the base images. This is a
network problem, not a mistake in your Dockerfile. Test it on its own:

```bash
docker pull nginx:alpine
```

If that fails too, fully quit Docker Desktop from the system tray and reopen
it. If you use a VPN, connect the VPN *first*, then start Docker Desktop.

**Sign in fails with a network error, but the UI loads**

nginx cannot reach the API. Check both containers are on the same network:

```bash
docker ps --format "{{.Names}} | {{.Networks}}"
```

Both `auth-app` and `auth-api` must show `auth-net`. If one does not, delete
that container and run it again with `--network auth-net`.

**`npm ci` fails with `can only install packages when your package.json and package-lock.json are in sync`**

Your lock file does not match `package.json`. Fix it on your machine, then
rebuild:

```bash
npm install
docker build -t auth-app:latest .
```

**The page loads but routes give a 404 or 500**

This is an nginx config problem in [nginx.conf](nginx.conf). Test the config
without starting the site:

```bash
docker run --rm --entrypoint nginx auth-app:latest -t
```

---

# Part 2 - Running locally without Docker

For day-to-day development this is faster, because the page reloads as you type.

```bash
npm install
npm start
```

Then open <http://localhost:3000>.

You also need a `.env` file in this folder telling the app where the API is:

```
# Local development (npm start)
REACT_APP_API_BASE_URL=https://localhost:7143
```

Two rules that catch people out:

1. **Comments need their own line starting with `#`.** There is no `//` comment
   syntax. Writing `REACT_APP_API_BASE_URL= // note` makes the value literally
   `// note`, and requests go to a nonsense URL.
2. **Restart `npm start` after editing `.env`.** It is only read at startup.

The Docker build ignores this file on purpose - the [Dockerfile](Dockerfile)
sets `REACT_APP_API_BASE_URL=` to empty so the app uses relative `/api/` URLs
and goes through the nginx proxy instead.

When running locally, the API must be started from Visual Studio or with
`dotnet run`, and your browser must trust its HTTPS certificate. If sign in
fails immediately with "Unable to reach the server", open
<https://localhost:7143/swagger> once and accept the warning, or run:

```bash
dotnet dev-certs https --trust
```

## Other npm scripts

| Command | What it does |
| --- | --- |
| `npm start` | Dev server on port 3000 with hot reload |
| `npm run build` | Production build into the `build` folder |
| `npm test` | Test runner in watch mode |

`npm run eject` also exists, but it is a **one-way** operation that copies all
the build configuration into your project permanently. You do not need it.

---

## Project structure

```
auth-app/
  public/index.html      Bootstrap CSS is loaded here from a CDN
  src/
    App.jsx              Routes: /signup, /signin, / -> /signin
    pages/SignUp.jsx     Sign up form
    pages/SignIn.jsx     Sign in form
    api/axiosClient.js   The one shared axios instance
    api/authApi.js       signUp() and signIn()
    api/apiError.js      Turns an axios failure into a normal response
    models/auth.js       Request and response shapes
  Dockerfile             Two-stage build: node builds it, nginx serves it
  nginx.conf             Serves React, proxies /api/ to auth-api
  .dockerignore          Keeps node_modules out of the build
```

## Learn more

- [Create React App documentation](https://facebook.github.io/create-react-app/docs/getting-started)
- [React documentation](https://reactjs.org/)
- [Docker getting started](https://docs.docker.com/get-started/)
