# Deploying `auth-api` + `auth-app` to Azure — click-by-click

**No command line.** Every step below is done in a web browser: the Azure Portal
for the infrastructure, the Azure DevOps web UI for the pipelines.

**Development environment only.** One set of resources, one deployment target.
No staging slots, no approval gates, no production. Swagger stays switched on so
you can test the API from a browser.

Work through §1 → §11 in order. Don't skip ahead — §3 creates things that §7
needs to already exist.

---

## 0. What you are building

```
   YOU push code
        │
        ▼
┌──────────────────────────────────────────────┐
│  Azure DevOps  (dev.azure.com)               │
│                                              │
│   auth-api repo ──► Build pipeline ─┐        │
│   auth-app repo ──► Build pipeline ─┤        │
│                                     │        │
│                     Release pipelines│       │
└─────────────────────────────────────┼────────┘
                                      │
                 ┌────────────────────┴───────┐
                 ▼                            ▼
        ┌─────────────────┐          ┌─────────────────┐
        │ Container       │          │  Azure SQL      │
        │ Registry (ACR)  │          │  AuthDB         │
        │ auth-api:42     │          └────────▲────────┘
        │ auth-app:42     │                   │
        └────────┬────────┘                   │
                 │ pulls image                │
      ┌──────────┴──────────┐                 │
      ▼                     ▼                 │
┌──────────────┐    ┌──────────────┐          │
│ Web App      │───►│ Web App      │──────────┘
│ auth-app-dev │    │ auth-api-dev │
│ nginx :80    │    │ .NET :8080   │
└──────────────┘    └──────────────┘
   you open this
```

Five things get created. That's the whole system:

| # | What | Why you need it |
|---|---|---|
| 1 | **Resource group** | A folder. Everything else goes inside it, and deleting it deletes everything. |
| 2 | **Container Registry (ACR)** | A private Docker Hub. Pipelines push images here; Web Apps pull from here. |
| 3 | **SQL Database** | Replaces the `host.docker.internal,1433` SQL Server you run locally. |
| 4 | **Two Web Apps** (on one App Service Plan) | One runs your API container, one runs your nginx container. |
| 5 | **Azure DevOps project** | Holds the two repos, the two build pipelines and the two release pipelines. |

### Names you will need over and over

Simple, consistent names throughout — no random digits to keep track of:

| Thing | Name | Globally unique? |
|---|---|---|
| Resource group | `rg-auth-dev` | no |
| Region | `Central India` | — |
| Container registry | `acrauthdev` | **yes** |
| SQL server | `sql-auth-dev` | **yes** |
| SQL database | `AuthDB` | no |
| SQL admin login | `authadmin` | no |
| SQL admin password | *(12+ chars, upper + lower + digit + symbol)* | — |
| App Service Plan | `asp-auth-dev` | no |
| API Web App | `auth-api-dev` | **yes** |
| Front-end Web App | `auth-app-dev` | **yes** |
| Application Insights | `ai-auth-dev` | no |

Note the container registry name has **no dashes** — ACR only accepts lowercase
letters and digits.

#### If a name is already taken

Four of these share a namespace with every other Azure customer, so the portal
may reject one with *"name is not available"*. When that happens, append your
initials — `auth-api-dev-vk` — and stay consistent from that point on. The names
that matter later are the two Web Apps, because they become your URLs and they
appear in five other places:

| If you rename the Web App | Also update |
|---|---|
| `auth-api-dev` | §2.4 Dockerfile `ARG`, §3.6 Health check, §5.3 variable `apiAppName`, §9.9 App name |
| `auth-app-dev` | §3.6 `Cors__AllowedOrigins__0`, §5.3 variable `webAppName`, §10 App name |

The registry and SQL server names are easier — they only appear in the variable
group and the connection string respectively.

Your two public URLs will **not** be a clean `auth-api-dev.azurewebsites.net`.
Azure now appends a random string and the region to every new App Service
hostname, and the random part is **different for each app**:

```
https://auth-api-dev-awgfe0gaezf6hne6.centralindia-01.azurewebsites.net   ← the API
https://auth-app-dev-bue3fweyffc3g9ec.centralindia-01.azurewebsites.net   ← open this in a browser
```

You can't choose these. Read each one from the portal after the app is created:
**App Services** → click the app → **Overview** → **Default domain**. Copy it,
don't retype it.

Throughout this guide, wherever you see a short URL like
`https://auth-api-dev.azurewebsites.net`, substitute your real default domain.
The **resource names** in the table above are unaffected — the pipeline's "App
name" field and the variable group both use `auth-api-dev`, not the hostname.

The three places the hostname actually matters:

| Where | Which app's domain |
|---|---|
| §2.4 `auth-app` Dockerfile `ARG` | the **API**'s |
| §3.6 `Cors__AllowedOrigins__0` | the **front end**'s |
| §9 / §10 verification in a browser | both |

Note those first two are crossed over, and that's correct: the front end needs to
know where to send requests, and the API needs to know which origin to accept
them from.

---

## 1. Before you start

| # | Thing | How to check |
|---|---|---|
| 1 | **A paid Azure subscription** ✅ | [portal.azure.com](https://portal.azure.com) → search *Subscriptions* → you should see one with status **Active** |
| 2 | **Owner** role on it | Subscriptions → click yours → **Access control (IAM)** → **View my access**. You need **Owner**, or both **Contributor** and **User Access Administrator**. §3.8 fails without this. |
| 3 | **A Microsoft account for Azure DevOps** | Use the same one you sign in to Azure with |
| 4 | **Docker Desktop** *(only if you want to test images locally first)* | Optional |

### The one thing that will block you on day one

Azure DevOps billing is **separate** from your Azure subscription. A new private
Azure DevOps project starts with **zero** build capacity, and your first pipeline
run will sit in a queue forever showing:

> *No hosted parallelism has been purchased or granted.*

Having a paid Azure subscription does **not** fix this. Pick one:

| Option | Cost | Ready in | Where |
|---|---|---|---|
| **Request the free grant** ← do this now | Free (1800 min/month) | 1–3 business days | Fill the form at [aka.ms/azpipelines-parallelism-request](https://aka.ms/azpipelines-parallelism-request) |
| **Buy 1 hosted parallel job** | ~$40/month | Immediately | DevOps → Organization settings → Billing → set up billing → Parallel jobs → MS Hosted CI/CD = 1 |
| **Run an agent on your own PC** | Free | ~15 min | DevOps → Organization settings → Agent pools → Default → New agent |

Submit the free-grant form **before** you start §3, so it's approved by the time
you reach §7. If you can't wait, buy one job and cancel it later.

---

## 2. Four code changes to make first

These are edits in Visual Studio / VS Code, not portal steps. Make them, commit
them, then carry on. Nothing else in your two projects has to change.

### 2.1 `auth-api` — apply migrations automatically at startup

Right now you run `dotnet ef database update` by hand. For a development
environment, letting the app do it on startup removes an entire pipeline stage.

In `Program.cs`, just before `app.Run();`:

```csharp
// DEV ONLY. Applies any pending EF migrations when the app starts.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();
}
```

The `IsDevelopment()` guard matters: your dev Web App will run with
`ASPNETCORE_ENVIRONMENT=Development`, so this runs there and nowhere else.

> ⚠️ **Never do this in production.** With more than one instance, several copies
> race to apply the same migration; you also lose the chance to review the SQL
> before it touches real data. Production applies a reviewed script from the
> pipeline instead. It's fine here precisely because this is a single-instance
> dev box with throwaway data.

### 2.2 `auth-api` — add a health endpoint

App Service needs a cheap URL to confirm your container actually started. Also in
`Program.cs`, before `app.Run();`:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .ExcludeFromDescription();
```

Keep it away from the database — it should answer even when SQL is asleep.

### 2.3 `auth-api` — trust the Azure proxy

App Service handles HTTPS and forwards plain HTTP to your container. Without
this, anything generating an absolute URL says `http://`. Near the **top** of the
middleware pipeline:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

### 2.4 `auth-app` — point the front end at the Azure API

Your Dockerfile currently forces the base URL to empty:

```dockerfile
ENV REACT_APP_API_BASE_URL=
```

That was right for local Docker, where nginx proxied `/api/` to the `auth-api`
container over the `auth-net` network. On Azure the two containers are separate
Web Apps on different hostnames, so the front end needs the API's full URL.

Change the **build stage** to:

```dockerfile
FROM node:24-alpine AS build
WORKDIR /src

# Your dev API URL. Overridable later with --build-arg.
ARG REACT_APP_API_BASE_URL="https://auth-api-dev-awgfe0gaezf6hne6.centralindia-01.azurewebsites.net"
ENV REACT_APP_API_BASE_URL=$REACT_APP_API_BASE_URL

COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build
```

Use **your** API Web App name from the §0 table.

> **Why a default value instead of passing it in the pipeline?** Because you only
> have one environment, one value is correct everywhere, and the pipeline task
> stays a simple two-field form. When you add a second environment, you'd pass
> `--build-arg` instead.

**Remember this:** Create React App bakes this value into the JavaScript bundle
at *build* time. If the API's URL ever changes, you must **rebuild the image** —
restarting the Web App or changing a setting in the portal will do nothing.

### 2.5 Push the changes

Use whatever Git UI you already have — Visual Studio's **Git Changes** panel, VS
Code's **Source Control** tab, or GitHub Desktop. Commit both projects. You'll
connect them to Azure Repos in §4.

---

## 3. Create the Azure resources in the portal

Sign in at [portal.azure.com](https://portal.azure.com). Every step here uses the
search bar at the very top of the page — type the service name, click it in the
results. That's faster and more reliable than hunting through the left menu.

### 3.1 Resource group

1. Top search bar → type **Resource groups** → click it.
2. **+ Create**.
3. Fill in:

| Field | Value |
|---|---|
| Subscription | your subscription |
| Resource group | `rg-auth-dev` |
| Region | `Central India` |

4. **Review + create** → **Create**.

Everything from here on goes into this group. When you're done experimenting,
deleting this one group removes every charge.

### 3.2 Container Registry

1. Search bar → **Container registries** → **+ Create**.
2. **Basics** tab:

| Field | Value | Note |
|---|---|---|
| Resource group | `rg-auth-dev` | |
| Registry name | `acrauthdev` | Globally unique. **Lowercase letters and digits only** — no dashes, no underscores. If it's taken, add your initials: `acrauthdevvk` |
| Location | `Central India` | |
| Pricing plan | **Basic** | ~$5/month, plenty here |

3. **Review + create** → **Create**. Takes about a minute.
4. When it's done, **Go to resource** → **Overview**. Copy the **Login server**
   value — it looks like `acrauthdev.azurecr.io`. Write it down; you need it
   three more times.

### 3.3 SQL Database

This one creates two things at once: a *server* (the logical host) and a
*database* on it.

1. Search bar → **SQL databases** → **+ Create**.
2. **Basics** tab:

| Field | Value |
|---|---|
| Resource group | `rg-auth-dev` |
| Database name | `AuthDB` |
| Server | click **Create new** (see below) |
| Want to use SQL elastic pool? | **No** |
| Workload environment | **Development** |

3. The **Create SQL Database Server** panel opens:

| Field | Value |
|---|---|
| Server name | `sql-auth-dev` |
| Location | `Central India` |
| Authentication method | **Use SQL authentication** |
| Server admin login | `authadmin` |
| Password / Confirm | your strong password |

Click **OK**. **Write that password down now** — the portal will not show it
again, and you need it in §3.6.

4. Back on Basics, under **Compute + storage**, click **Configure database**:

| Field | Value | Why |
|---|---|---|
| Service tier | **General Purpose** | |
| Compute tier | **Serverless** | Bills only while in use |
| Max vCores | `1` | |
| Min vCores | `0.5` | |
| Enable auto-pause | ✅ ticked, **1 hour** | Sleeps when idle — this is what keeps the bill near zero |

Click **Apply**.

5. **Backup storage redundancy**: choose **Locally-redundant backup storage**.
   Geo-redundant costs more and means nothing for dev data.

6. Go to the **Networking** tab — **this is the step people miss**:

| Field | Value | Why |
|---|---|---|
| Connectivity method | **Public endpoint** | |
| Allow Azure services and resources to access this server | **Yes** | Without this your Web App gets *"Cannot open server"* on every request |
| Add current client IP address | **Yes** | So you can use the portal's Query editor in §3.10 |

7. **Additional settings** tab → Use existing data: **None**.
8. **Review + create** → **Create**. Takes 3–5 minutes.

#### Get the connection string

Once deployment finishes: **Go to resource** → left menu **Settings** →
**Connection strings** → **ADO.NET** tab. Copy the whole string. It looks like:

```
Server=tcp:sql-auth-dev.database.windows.net,1433;Initial Catalog=AuthDB;Persist Security Info=False;User ID=authadmin;Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Two edits before you use it:

- Replace `{your_password}` with the real password — **remove the curly braces**.
- Change `Connection Timeout=30` to `Connection Timeout=60`. A serverless
  database that has auto-paused takes 30–60 seconds to wake, and the first
  request after an idle period will otherwise fail.

Paste the result into Notepad. You need it in §3.6.

### 3.4 App Service Plan + the API Web App

The plan is the Linux VM your containers run on. Creating the first Web App
creates the plan too.

1. Search bar → **App Services** → **+ Create** → **Web App**.
2. **Basics** tab:

| Field | Value | Note |
|---|---|---|
| Resource group | `rg-auth-dev` | |
| Name | `auth-api-dev` | Globally unique. The real hostname gets a random suffix appended — read it off the Overview page afterwards (§0). |
| Publish | **Container** | ⚠️ **The most-missed field on this page.** "Code" is the default and the rest of the form looks the same either way. It can't be changed after creation, so check it before clicking Create. |
| Operating System | **Linux** | |
| Region | `Central India` | |
| Linux Plan | **Create new** → `asp-auth-dev` | |
| Pricing plan | **Basic B1** | ~$13/month, hosts **both** Web Apps |

3. **Container** tab. Your image doesn't exist yet, so use a placeholder:

| Field | Value |
|---|---|
| Image Source | **Other container registries** |
| Access Type | **Public** |
| Registry server URL | `https://mcr.microsoft.com` |
| Image and tag | `azuredocs/aci-helloworld:latest` |

4. **Monitoring** tab → **Enable Application Insights**: **Yes** → Create new
   resource → name it `ai-auth-dev`.

   ⚠️ **This toggle won't actually do anything for a Linux container**, and the
   app's Overview page will later say *Application Insights: Not supported*.
   Azure's automatic instrumentation works by injecting an agent into the host,
   which it can't do inside your own image. Create the resource anyway — you'll
   wire it up properly from inside the app in §12.1, which is a two-line code
   change and gives better data than the agent would.

5. **Review + create** → **Create**.

### 3.5 The front-end Web App

#### First, what you're actually creating

You already have an App Service **Plan** (`asp-auth-dev`) and one **Web App**
(`auth-api-dev`) sitting on it. Those are two different things, and the
difference is the whole point of this step:

```
   asp-auth-dev  ← the PLAN: a Linux VM. 1 core, 1.75 GB RAM. THIS is what bills.
   ├── auth-api-dev    ← a WEB APP: your .NET container, listening on 8080
   └── auth-app-dev    ← a WEB APP: your nginx container, listening on 80
                          (this is what you're adding now)
```

A plan is rented hardware. A Web App is one containerised application running on
it. One plan can host many apps, and **you pay for the plan, not for the apps**.

So the choice here is:

| | One plan, two apps ← what we're doing | Two plans, one app each |
|---|---|---|
| Cost | ~$13/month | ~$26/month |
| CPU + RAM | Shared between both containers | Dedicated to each |
| Scaling | Scale up, both get bigger | Independent |
| Restarting one | Doesn't touch the other | Doesn't touch the other |

For a dev environment with two small containers, sharing is obviously right. Your
nginx container serves static files and uses almost nothing.

#### Why two Web Apps at all, rather than one?

A Web App runs **one** container image on **one** port. You have two images
listening on two different ports (8080 and 80), so they need two apps. This is
the direct Azure equivalent of your local `docker run` twice on the `auth-net`
network — same two containers, different host.

#### Now the clicks

1. Top search bar → **App Services** → **+ Create** → **Web App**.

2. **Basics** tab:

| Field | Value | Why |
|---|---|---|
| Subscription | yours | |
| Resource group | `rg-auth-dev` | **Must match.** A plan can only be shared by apps in the same group. |
| Name | `auth-app-dev` | Globally unique. The real hostname gets a random suffix appended — read it off the Overview page afterwards (§0). |
| Publish | **Container** | ⚠️ Not "Code". Same as §3.4. |
| Operating System | **Linux** | Must match the plan's OS. A Linux plan cannot host a Windows app or vice versa. |
| Region | `Central India` | **Must match the plan's region**, or the plan won't appear in the next dropdown. |

3. Now the important field. Scroll to **Pricing plans**:

| Field | What to do |
|---|---|
| **Linux Plan (Central India)** | Open the dropdown. **`asp-auth-dev` should already be listed.** Select it. |
| Pricing plan | Now greyed out, showing **Basic B1** |

   Do **not** click "Create new" here. That's the mistake this section exists to
   prevent — it silently doubles your bill and nothing about it looks wrong
   afterwards.

   The **Pricing plan** field greys out because tier is a property of the plan,
   not of the app. `auth-api-dev` and `auth-app-dev` are on the same hardware, so
   they cannot be on different tiers.

<details>
<summary>❓ `asp-auth-dev` isn't in the dropdown</summary>

Three possible causes, in order of likelihood:

1. **Region mismatch.** The dropdown is literally labelled "Linux Plan
   *(Central India)*" — it only lists plans in the region selected above. Go back
   and set Region to match §3.4.
2. **Wrong resource group.** Plans aren't shared across groups. Set it to
   `rg-auth-dev`.
3. **Publish is set to "Code", or OS is Windows.** Your plan is a Linux
   container plan. Fix Publish → **Container** and OS → **Linux**, and the plan
   reappears.
</details>

4. **Container** tab — same placeholder as §3.4, because your `auth-app` image
   doesn't exist in the registry yet:

| Field | Value |
|---|---|
| Image Source | **Other container registries** |
| Access Type | **Public** |
| Registry server URL | `https://mcr.microsoft.com` |
| Image and tag | `azuredocs/aci-helloworld:latest` |

   This is a throwaway. It exists only so the Web App can be created at all —
   §10's release pipeline replaces it with your real image. Until then, opening
   the site shows a generic Azure "hello world" page, which is expected and not a
   sign anything is broken.

5. **Networking** tab — leave every default. Public access on, no VNet.

6. **Monitoring** tab → **Enable Application Insights**: **No**.

   Different answer from §3.4, and the reason is worth understanding.
   Application Insights instruments **server-side** code — incoming requests,
   exceptions, outbound SQL calls. This Web App runs plain nginx handing out
   pre-built `.js` and `.css` files. There's no application code executing on the
   server, so there is nothing to instrument; you'd pay for ingestion of static
   file requests and learn nothing.

   Front-end monitoring is a genuinely different tool — the Application Insights
   **JavaScript SDK**, added to your React app, reporting page loads and browser
   errors from the user's machine. Worth adding later; it isn't this toggle.

7. **Review + create** → **Create**. About a minute.

#### Verify before moving on

Search bar → **App Service plans** → click **asp-auth-dev** → left menu
**Settings** → **Apps**.

You should see **exactly two** entries, both in `rg-auth-dev`, both **Running**:

```
auth-api-dev
auth-app-dev
```

If only one is listed, the second landed on a plan of its own. Check
**Resource groups** → `rg-auth-dev` — if there are two App Service plans, delete
the extra app, delete the extra plan, and redo step 3 selecting the existing
plan. Catching this now costs two minutes; catching it on next month's invoice
costs $13.

> Ignore the **Kind** column on this screen. It sometimes reads `app,linux` for a
> perfectly good container app. The reliable check is below.

Then confirm each app is actually a container app. Click into it → **Overview** →
the **Properties** tab → the **Web app** panel:

| Field | Expected |
|---|---|
| Publishing model | **Container** |
| Container Image | `mcr.microsoft.com/azuredocs/aci-helloworld:latest` |

If Publishing model says **Code**, the **Publish** field was left on its default
during creation. That can't be changed afterwards — delete the Web App (Overview
→ Delete; this does not touch the plan or the other app) and create it again with
**Publish: Container**.

Also on this page: **Runtime status: Issues Detected** is normal. That's the
placeholder image complaining, and it clears once your real image is deployed.

#### A note on sharing

Both containers share one core and 1.75 GB of RAM. That's fine here — nginx
serving static files is nearly free, and your .NET API has room. But it does mean
the two aren't isolated: if the API pegs the CPU, the front end gets slower too,
and **Scale up** applies to both at once.

That trade-off is exactly right for a dev environment and exactly wrong for
production, where you'd separate them, or move the React app to Azure Static Web
Apps (which has a real free tier) and leave the plan to the API alone.

### 3.6 Settings for the API Web App

1. Search bar → **App Services** → click **auth-api-dev**.
2. Left menu → **Settings** → **Environment variables**.
   *(Older portal layout: **Configuration** → **Application settings**.)*
3. On the **App settings** tab, click **+ Add** once per row:

| Name | Value |
|---|---|
| `WEBSITES_PORT` | `8080` |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ConnectionStrings__DefaultConnection` | *(the connection string from §3.3)* |
| `Cors__AllowedOrigins__0` | `https://auth-app-dev-bue3fweyffc3g9ec.centralindia-01.azurewebsites.net` |

4. Click **Apply** → **Confirm**. The app restarts.

What each one does, because these four are where deployments go wrong:

- **`WEBSITES_PORT = 8080`** — tells App Service which port inside your container
  to send traffic to. Your API listens on 8080 because the Dockerfile's
  `USER app` is non-root and can't bind ports below 1024. Get this wrong and you
  get a 502 with no other explanation.
- **`ASPNETCORE_ENVIRONMENT = Development`** — switches on Swagger (so you can
  test in a browser at `/swagger`) and switches on the auto-migration from §2.1.
  Correct for a dev environment, wrong for anything real.
- **`ConnectionStrings__DefaultConnection`** — the double underscore is how a
  nested config key is written as an environment variable. Same convention as the
  `-e` flag in your local `docker run`. ASP.NET Core reads it as
  `ConnectionStrings:DefaultConnection`.
- **`Cors__AllowedOrigins__0`** — the `__0` sets the **first element of the
  array** your `appsettings.json` defines. Miss this one and every call from the
  browser fails with a CORS error, even though the API works fine when tested
  directly.

5. Left menu → **Settings** → **Configuration** → **General settings** tab. Set
   **Health check path** to `/health` → **Save**.

### 3.7 Settings for the front-end Web App

1. App Services → **auth-app-dev** → **Environment variables**.
2. **+ Add**:

| Name | Value |
|---|---|
| `WEBSITES_PORT` | `80` |

3. **Apply** → **Confirm**.

That's the only setting it needs. Everything the React app knows about the API
was compiled into its JavaScript bundle back in §2.4.

### 3.8 Let the Web Apps pull images from ACR

Your registry is private. Each Web App needs permission to read from it. The
clean way uses a **managed identity** — an automatic Azure identity for the app,
with no password stored anywhere.

**Do this twice, once per Web App.**

**Step A — switch the identity on:**

1. App Services → **auth-api-dev** → left menu **Settings** → **Identity**.
2. **System assigned** tab → Status: **On** → **Save** → **Yes**.
3. An **Object (principal) ID** appears. You don't need to copy it.

**Step B — grant it read access to the registry:**

1. Search bar → **Container registries** → **acrauthdev**.
2. Left menu → **Access control (IAM)** → **+ Add** → **Add role assignment**.
3. **Role** tab → search `AcrPull` → select it → **Next**.
4. **Members** tab → Assign access to: **Managed identity** → **+ Select members**.
5. In the panel: Subscription = yours, Managed identity = **App Service**, then
   click **auth-api-dev** → **Select**.
6. **Review + assign** → **Review + assign** again.

**Step C — tell the Web App to use it:**

1. Back to App Services → **auth-api-dev** → left menu **Deployment Center**.
2. Settings:

| Field | Value |
|---|---|
| Source | **Container Registry** |
| Container type | **Single Container** |
| Registry source | **Azure Container Registry** |
| Authentication | **Managed Identity** |
| Identity | **System assigned** |
| Subscription | yours |
| Registry | `acrauthdev` |
| Image | *(dropdown will be empty — see note)* |

3. The **Image** dropdown is empty because you haven't pushed anything yet.
   Leave this page for now — click away without saving. You'll come back in §9
   once the first image exists.

Now **repeat Steps A and B for `auth-app-dev`**. Each Web App gets its own
separate identity, so the role assignment genuinely has to be done twice.

> Role assignments take 30–60 seconds to take effect. If your first deployment
> fails with *unauthorized* or *image pull failed*, wait a minute and redeploy
> before assuming something is broken.

<details>
<summary>Fallback: if "Add role assignment" is greyed out</summary>

You don't have permission to assign roles. Use registry credentials instead:

1. Container registries → **acrauthdev** → **Settings** → **Access keys**.
2. Turn **Admin user** to **Enabled**. Copy the **Username** and **password**.
3. In each Web App → **Environment variables** → **+ Add**:

| Name | Value |
|---|---|
| `DOCKER_REGISTRY_SERVER_URL` | `https://acrauthdev.azurecr.io` |
| `DOCKER_REGISTRY_SERVER_USERNAME` | *(the username)* |
| `DOCKER_REGISTRY_SERVER_PASSWORD` | *(the password)* |

It works fine — it's just a shared password sitting in configuration.
</details>

### 3.9 Check what you have

Search bar → **Resource groups** → **rg-auth-dev**. You should see six items:

| Name | Type |
|---|---|
| `acrauthdev` | Container registry |
| `ai-auth-dev` | Application Insights |
| `asp-auth-dev` | App Service plan |
| `auth-api-dev` | App Service |
| `auth-app-dev` | App Service |
| `sql-auth-dev` | SQL server |
| `AuthDB` | SQL database |

If anything is missing, go back and create it now. §7 onwards assumes all of it
exists.

### 3.10 (Optional) Look at the empty database

Your `Profiles` table doesn't exist yet — the app creates it on first startup
thanks to §2.1. To watch that happen later:

1. Search bar → **SQL databases** → **AuthDB** → left menu **Query editor
   (preview)**.
2. Sign in with `authadmin` and your password.
3. Run: `SELECT * FROM sys.tables;` — empty for now.

Come back after §10 and the `Profiles` table will be there.

---

## 4. Set up Azure DevOps

### 4.1 Organisation and project

1. Go to [dev.azure.com](https://dev.azure.com) and sign in with the same account
   you use for Azure.
2. **+ New organization** → accept the terms → name it `vishaltechnology5` →
   region **India** → **Continue**.
3. **+ New project**:

| Field | Value |
|---|---|
| Project name | `Authentication` |
| Visibility | **Private** |
| Version control | Git |
| Work item process | Agile |

4. **Create project**.

> ⚠️ **One project, two repos — not two projects.** Service connections, variable
> groups and environments all live at the *project* level. Two projects means
> creating and maintaining every one of them twice, for zero benefit.

### 4.2 Create the two repositories

1. Left menu → **Repos** → **Files**.
2. A repo named `Authentication` already exists. At the top of the page, click the
   **repo name dropdown** → **New repository**.
3. Create the first:

| Field | Value |
|---|---|
| Repository type | Git |
| Repository name | `auth-api` |
| Add a README | **unticked** |

4. Repeat for `auth-app`.
5. Optionally delete the default `Authentication` repo: repo dropdown → **Manage
   repositories** → `...` next to it → **Delete**.

### 4.3 Push your code (no command line)

Each repo's landing page offers **Clone in VS Code** and **Clone in Visual
Studio** buttons. Either works, but the simplest route if your code already
exists locally:

**Visual Studio:**
1. Open the `auth-api` solution.
2. Menu **Git** → **Create Git Repository**.
3. Choose **Azure DevOps** → sign in → pick organisation `vishaltechnology5`, project
   `Authentication`, repository `auth-api`.
4. **Create and Push**.

**VS Code:**
1. Open the `auth-app` folder.
2. **Source Control** tab (the branch icon in the left rail) → **Publish to
   Azure Repos**, or **Initialize Repository** then **Publish Branch** and pick
   the Azure DevOps remote.

**Already have them on GitHub?** Even easier — Repos → Files → **Import a
repository** → paste the GitHub URL → **Import**.

Your two repo URLs will be:

```
https://dev.azure.com/vishaltechnology5/Authentication/_git/auth-api
https://dev.azure.com/vishaltechnology5/Authentication/_git/auth-app
```

Confirm both repos show your files, and that each has an `AuthApi.csproj` /
`package.json` **at the top level** — not nested inside another folder. The
pipelines in §7 assume the project sits at the repo root.

---

## 5. Service connections

These are how Azure DevOps is allowed to touch your Azure subscription. You need
two.

### 5.1 Connection to your Azure subscription

1. Bottom-left → **Project settings** → under Pipelines → **Service connections**.
2. **Create service connection** → **Azure Resource Manager** → **Next**.
3. Authentication method: **Workload Identity federation (automatic)** → **Next**.

   *(This is the modern passwordless option. If your account can't create it,
   fall back to "Service principal (automatic)".)*

4. Fill in:

| Field | Value |
|---|---|
| Scope level | **Subscription** |
| Subscription | yours |
| Resource group | `rg-auth-dev` |
| Service connection name | `azure-sub` |
| Grant access permission to all pipelines | ✅ **tick this** |

5. **Save**. A browser popup may ask you to sign in — allow it.

### 5.2 Connection to your container registry

1. **Create service connection** → **Docker Registry** → **Next**.
2. Fill in:

| Field | Value |
|---|---|
| Registry type | **Azure Container Registry** |
| Authentication Type | **Service Principal** |
| Subscription | yours |
| Azure container registry | `acrauthdev` |
| Service connection name | `acr-connection` |
| Grant access permission to all pipelines | ✅ **tick this** |

3. **Save**.

Ticking "grant access to all pipelines" both times saves you clicking through an
authorisation prompt on every pipeline's first run.

### 5.3 Variable group

One place for the names you'd otherwise retype into four pipelines.

1. Left menu → **Pipelines** → **Library** → **+ Variable group**.
2. Name it `auth-dev`.
3. **+ Add** one row at a time:

| Name | Value |
|---|---|
| `acrLoginServer` | `acrauthdev.azurecr.io` |
| `apiAppName` | `auth-api-dev` |
| `webAppName` | `auth-app-dev` |
| `resourceGroup` | `rg-auth-dev` |

4. **Save**.
5. Open the **Pipeline permissions** tab → **+** → **Open access** (or add each
   pipeline once they exist).

No secrets in here — nothing the pipelines do needs a password, because the two
service connections handle all the authentication.

---

## 6. How the two pipelines fit together

For each app you build **two** pipelines, and this split is the thing to
understand before clicking:

| Pipeline | Does what | Runs when |
|---|---|---|
| **Build** | Turns source code into a Docker image and pushes it to ACR | Every commit to `main` |
| **Release** | Tells the Web App to pull that image and restart | Automatically, whenever a build succeeds |

Two apps × two pipelines = four things to create. They're each about five clicks
once you've done the first.

> **Why not one YAML file doing both?** YAML pipelines are what most teams use
> now, and Appendix B has the equivalent. But the classic editor is entirely
> point-and-click, which is what you asked for, and the concepts map across
> one-to-one when you switch later.

---

## 7. Build pipeline for `auth-api`

1. Left menu → **Pipelines** → **Pipelines** → **Create Pipeline**.
2. At the **bottom** of the "Where is your code?" page, click the small link:
   **Use the classic editor to create a pipeline without YAML**.

   This link is easy to miss — it's below the list of source options, in smaller
   grey text.

3. Select source:

| Field | Value |
|---|---|
| Source | **Azure Repos Git** |
| Team project | `Authentication` |
| Repository | **auth-api** |
| Default branch | `main` |

   → **Continue**.

4. On the template page, click **Empty job** (the link at the top — not one of
   the templates).

5. Click the **Agent job 1** row and set:

| Field | Value |
|---|---|
| Agent pool | **Azure Pipelines** |
| Agent Specification | **ubuntu-latest** |

6. Click the **+** on the Agent job row. Search for **Docker**. Click **Add**.

7. Click the new **buildAndPush** task and fill in:

| Field | Value |
|---|---|
| Display name | `Build and push API image` |
| Container registry | **acr-connection** |
| Container repository | `auth-api` |
| Command | **buildAndPush** |
| Dockerfile | `**/Dockerfile` |
| Build context | `**` |
| Tags | `$(Build.BuildId)` *(on its own line)* |

   In the **Tags** box, put `$(Build.BuildId)` on the first line and `latest` on
   the second. Two lines, no commas.

8. Go to the **Variables** tab at the top → **Variable groups** → **Link variable
   group** → `auth-dev` → **Link**.

9. Go to the **Triggers** tab → tick **Enable continuous integration**. Branch
   filter: Include `main`.

10. Top right → **Save & queue** → **Save and run**.

11. Watch it run. First time takes 3–6 minutes — it's compiling your .NET app
    inside the Docker build.

**Why it's this short:** your Dockerfile is already multi-stage. It restores,
builds and publishes the app inside the SDK image, so the pipeline doesn't need a
separate .NET build step. One task really is the whole build.

### Confirm the image landed

Azure Portal → **Container registries** → `acrauthdev` → left menu
**Services** → **Repositories**. You should see `auth-api` with two tags: `1`
(your build number) and `latest`.

### Rename the pipeline

Pipelines → **All** tab. It's called `Authentication-CI` or similar. Click `...`
→ **Rename/move** → `auth-api-build`.

Do this now. With four pipelines all named after the project, you will not be
able to tell them apart.

---

## 8. Build pipeline for `auth-app`

Exactly the same as §7, with three changes:

| Step | Change |
|---|---|
| §7.3 Repository | **auth-app** |
| §7.7 Container repository | `auth-app` |
| §7.7 Display name | `Build and push front-end image` |

Everything else — agent, Dockerfile path, build context, tags, variable group,
CI trigger — is identical.

Rename it `auth-app-build` when it finishes.

Confirm in the portal that the registry now has **two** repositories: `auth-api`
and `auth-app`.

> This build takes longer than the API one — `npm ci` plus a CRA production
> build is slow. 5–8 minutes is normal.

---

## 9. Release pipeline for `auth-api`

This is the part that actually puts your image on the Web App.

1. Left menu → **Pipelines** → **Releases** → **+ New** → **New release pipeline**.
2. A template panel opens on the right. Click **Empty job** at the top.
3. Name the stage `Dev` → close the panel with the **X**.
4. Click **+ Add** next to **Artifacts** (the left box):

| Field | Value |
|---|---|
| Source type | **Build** |
| Project | `Authentication` |
| Source (build pipeline) | **auth-api-build** |
| Default version | **Latest** |
| Source alias | leave as-is |

   → **Add**.

5. Click the **lightning bolt** icon on the artifact box → **Continuous
   deployment trigger**: **Enabled**. Close the panel.

   This is what makes a successful build deploy itself.

6. Click **1 job, 0 task** inside the **Dev** stage.
7. Click the **Agent job** row → Agent Specification: **ubuntu-latest**.
8. Click the **+** on the Agent job row → search **Azure Web App for Containers**
   → **Add**.
9. Click the new task and fill in:

| Field | Value |
|---|---|
| Display name | `Deploy API container` |
| Azure subscription | **azure-sub** |
| App name | **auth-api-dev** *(pick from the dropdown)* |
| Image name | `$(acrLoginServer)/auth-api:$(Build.BuildId)` |
| *(leave Startup command empty)* | |

10. Go to the **Variables** tab → **Variable groups** → **Link variable group**
    → `auth-dev` → scope **Dev** → **Link**.

    This is what makes `$(acrLoginServer)` resolve.

11. Top of the page: click the pipeline name (`New release pipeline`) and rename
    it **`auth-api-release`** → **Save** → **OK**.

12. **Create release** (top right) → **Create**. Watch the Dev stage go green.

### Finish the Deployment Center setup

Now that a real image exists, go back to the page you left in §3.8 Step C:

1. Portal → App Services → **auth-api-dev** → **Deployment Center**.
2. It should now show your ACR image. Confirm **Authentication: Managed
   Identity** and that Registry/Image are populated → **Save** if anything
   changed.

### Check it worked

Open in a browser:

```
https://auth-api-dev-awgfe0gaezf6hne6.centralindia-01.azurewebsites.net/health
```

Expect `{"status":"healthy"}`. Then:

```
https://auth-api-dev-awgfe0gaezf6hne6.centralindia-01.azurewebsites.net/swagger
```

Swagger loads because `ASPNETCORE_ENVIRONMENT=Development`. **Use it to test both
endpoints right now** — expand `POST /api/auth/signup`, click **Try it out**,
edit the JSON, **Execute**. You should get a `201`, and a `409` if you run the
same request twice.

The first request may take up to a minute: the container is cold *and* the
serverless database has to wake up. That's also when your §2.1 auto-migration
creates the `Profiles` table. Go back to Query editor (§3.10) and run
`SELECT * FROM Profiles;` to see the account you just made.

---

## 10. Release pipeline for `auth-app`

Same as §9, with these changes:

| Step | Change |
|---|---|
| §9.4 Source (build pipeline) | **auth-app-build** |
| §9.9 Display name | `Deploy front-end container` |
| §9.9 App name | **auth-app-dev** |
| §9.9 Image name | `$(acrLoginServer)/auth-app:$(Build.BuildId)` |
| §9.11 Pipeline name | `auth-app-release` |

Then repeat the §3.8 Step C Deployment Center check for this Web App too.

### The moment of truth

```
https://auth-app-dev-bue3fweyffc3g9ec.centralindia-01.azurewebsites.net/signin
```

Create an account. Then sign in with it.

---

## 11. When something doesn't work

Work down this table. Nearly every first-deployment failure is in the top five
rows.

| What you see | Cause | Fix |
|---|---|---|
| Pipeline stuck in queue, "no hosted parallelism" | Build capacity | §1 — request the grant, buy a job, or run a local agent |
| Browser shows **"Application Error"** or a 502 | Wrong `WEBSITES_PORT` | `8080` for the API, `80` for the front end (§3.6 / §3.7) |
| Deployment succeeds but site never loads | Image pull failed | Portal → Web App → **Deployment Center** → **Logs**. Redo §3.8, wait 60s, redeploy |
| Sign-up returns **500** | Database unreachable | SQL server → **Networking** → "Allow Azure services" must be **Yes** (§3.3 step 6) |
| Browser console: **"blocked by CORS policy"** | Origin not allow-listed | `Cors__AllowedOrigins__0` must be the exact front-end URL — `https`, no trailing slash. Then restart the API app |
| Front end loads but every call fails | Bundle points at the wrong API | §2.4 — the URL is baked in at build time. Fix the Dockerfile, commit, let it rebuild |
| First request after a break times out, then works | Serverless SQL auto-pause | Expected. `Connection Timeout=60` in the connection string handles it |
| Hard refresh on `/signup` gives 404 | nginx SPA fallback | Your `nginx.conf` already has `try_files`. Confirm it was copied into the image |
| Release task: "resource not found" | Wrong app name or subscription | Pick the App name from the **dropdown**, don't type it |
| `$(acrLoginServer)` appears literally in the log | Variable group not linked | §9.10 — link `auth-dev` in the **release** pipeline too, not just the build |
| Build task: "Dockerfile not found" | Path | `**/Dockerfile` and build context `**` |
| "Add role assignment" greyed out | Missing permission | Use the fallback in §3.8 |

### The three places to look

1. **Live container output** — Portal → your Web App → **Monitoring** → **Log
   stream**. This is where a crashing .NET app prints its stack trace. If it's
   empty: **Monitoring** → **App Service logs** → turn **Application logging**
   on, level Information, **Save**, then reopen Log stream.

2. **Why the container didn't start** — Web App → **Deployment Center** →
   **Logs** tab. Image pull failures and port mismatches show up here.

3. **What the app actually did** — Portal → **Application Insights** →
   `ai-auth-dev` → **Failures** for exceptions, **Performance** for slow
   requests. Every EF Core SQL call appears with its duration, which is how you
   tell "slow because of the database" from "slow because of the container".

### Restarting things

Web App → **Overview** → **Restart**. Do this after any change to Environment
variables — most take effect on restart, and the portal doesn't always prompt.

---

## 12. What to do after it works

In the order I'd actually do them:

### 12.1 Turn Application Insights on properly

The `ai-auth-dev` resource exists but is collecting nothing — Azure's agent-based
instrumentation doesn't work inside a custom Linux container, which is why the
API's Overview page says *Application Insights: Not supported*.

Two steps to fix it, and the result is better than the agent would have given you
anyway:

**1. Add the SDK to `auth-api`.** In Visual Studio: right-click the project →
**Manage NuGet Packages** → Browse → install
`Microsoft.ApplicationInsights.AspNetCore`. Then in `Program.cs`, alongside your
other service registrations:

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

Commit — the pipeline rebuilds and redeploys.

**2. Give it the connection string.** Portal → **Application Insights** →
`ai-auth-dev` → **Overview** → copy **Connection String**. Then App Services →
`auth-api-dev` → **Environment variables** → **+ Add**:

| Name | Value |
|---|---|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | *(the connection string)* |

**Apply** → **Confirm**.

After the next request, **Application Insights** → `ai-auth-dev` → **Failures**
shows exceptions with full stack traces, and **Performance** shows every EF Core
SQL call with its duration. That last one matters here: your
`ExceptionHandlingMiddleware` deliberately returns *"Something went wrong"* to the
caller, so without this you have no way to see what actually broke.

Don't bother for `auth-app-dev` — nginx serving static files has nothing to
report. Browser-side monitoring is a separate tool (the Application Insights
JavaScript SDK inside your React app).

### 12.2 Keep an eye on cost

| Resource | Approx. monthly |
|---|---|
| App Service Plan B1 | ~$13 (both apps) |
| Container Registry Basic | ~$5 |
| SQL serverless, auto-pausing | ~$5–15 |
| Application Insights | $0 under 5 GB |
| **Total** | **~$25–35** |

Set a budget alert: Portal → **Cost Management + Billing** → **Cost Management**
→ **Budgets** → **+ Add**. Scope it to `rg-auth-dev`, amount $50, alert at 80%.

Also cap Application Insights so a traffic spike can't surprise you:
**Application Insights** → `ai-auth-dev` → **Usage and estimated costs** →
**Daily cap** → 1 GB/day.

To stop paying entirely: **Resource groups** → `rg-auth-dev` → **Delete resource
group**. Type the name to confirm. Everything goes.

To pause without deleting: App Services → each app → **Stop**. The plan still
bills, so also consider **App Service plan** → **Scale up** → **Free F1** —
though note F1 won't run containers, so you'd need to scale back to B1 before
deploying again.

### 12.3 Add a real deployment gate

Right now every green build goes straight to the Web App. Add a pause:

Releases → `auth-api-release` → **Edit** → click the **person icon** on the left
of the **Dev** stage → **Pre-deployment approvals**: **Enabled** → add yourself →
**Save**.

Overkill for a dev environment, but it's a two-minute change and it's the concept
every interviewer asks about.

### 12.4 Protect `main`

Repos → **Branches** → hover `main` → `...` → **Branch policies**:

- **Require a minimum number of reviewers**: 1
- **Build validation** → **+** → pick `auth-api-build` → Trigger: Automatic

Do it for **both** repos — they're configured separately, and forgetting the
second is easy.

### 12.5 Take the password out of the connection string

Right now `ConnectionStrings__DefaultConnection` contains your SQL password in
plain text in the portal. Better: give the API's managed identity — the one you
already created in §3.8 — a login on the database, and change the connection
string to use `Authentication=Active Directory Default` with no password at all.

This is the single biggest security improvement available here, and it closes one
of the gaps your own project README lists.

### 12.6 Then add a production environment

Everything in this guide is deliberately dev-only. Production differs in four
ways, all of which you now have the vocabulary for:

| | Dev (this guide) | Production |
|---|---|---|
| Migrations | Auto-applied at startup (§2.1) | Reviewed SQL script run by the pipeline |
| Environment | `Development` — Swagger on | `Production` — Swagger off |
| Plan | B1, no slots | S1+, deploy to a **staging slot** then swap |
| Deploy | Straight to the app | Approval gate, then swap, with rollback by swapping back |

Build that as a **second** release stage on the same pipelines rather than a
second set of pipelines.

---

## Appendix A: every setting in one place

Handy when you're checking whether something got typed wrong.

**API Web App — `auth-api-dev`**

| Setting | Value |
|---|---|
| Publish | Container, Linux |
| `WEBSITES_PORT` | `8080` |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ConnectionStrings__DefaultConnection` | `Server=tcp:sql-auth-dev.database.windows.net,1433;Initial Catalog=AuthDB;User ID=authadmin;Password=…;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;` |
| `Cors__AllowedOrigins__0` | `https://auth-app-dev-bue3fweyffc3g9ec.centralindia-01.azurewebsites.net` |
| Health check path | `/health` |
| Identity | System assigned = On, with `AcrPull` on the registry |

**Front-end Web App — `auth-app-dev`**

| Setting | Value |
|---|---|
| Publish | Container, Linux |
| `WEBSITES_PORT` | `80` |
| Identity | System assigned = On, with `AcrPull` on the registry |

**SQL server — `sql-auth-dev`**

| Setting | Value |
|---|---|
| Allow Azure services | **Yes** |
| Your client IP | Added |
| Compute | Serverless, auto-pause 1 hour |

**Azure DevOps**

| Item | Name |
|---|---|
| Service connection (Azure) | `azure-sub` |
| Service connection (registry) | `acr-connection` |
| Variable group | `auth-dev` |
| Build pipelines | `auth-api-build`, `auth-app-build` |
| Release pipelines | `auth-api-release`, `auth-app-release` |

**Docker task settings (both build pipelines)**

| Field | Value |
|---|---|
| Container registry | `acr-connection` |
| Container repository | `auth-api` / `auth-app` |
| Command | `buildAndPush` |
| Dockerfile | `**/Dockerfile` |
| Build context | `**` |
| Tags | `$(Build.BuildId)` and `latest`, one per line |

---

## Appendix B: the same thing as YAML

When you're ready to move off the classic editor — and you should eventually,
because YAML lives in the repo, gets code-reviewed, and is what job descriptions
mean by "CI/CD" — this file replaces both the build and release pipeline for
`auth-api`. Commit it as `azure-pipelines.yml` at the repo root, then
**Pipelines → New pipeline → Azure Repos Git → Existing Azure Pipelines YAML
file**.

```yaml
trigger:
  branches:
    include: [ main ]

variables:
  - group: auth-dev
  - name: tag
    value: '$(Build.BuildId)'

stages:
- stage: Build
  jobs:
  - job: build
    pool:
      vmImage: 'ubuntu-latest'
    steps:
    - task: Docker@2
      displayName: Build and push API image
      inputs:
        command: buildAndPush
        containerRegistry: 'acr-connection'
        repository: 'auth-api'
        dockerfile: 'Dockerfile'
        buildContext: '.'
        tags: |
          $(tag)
          latest

- stage: Deploy
  dependsOn: Build
  condition: succeeded()
  jobs:
  - job: deploy
    pool:
      vmImage: 'ubuntu-latest'
    steps:
    - task: AzureWebAppContainer@1
      displayName: Deploy API container
      inputs:
        azureSubscription: 'azure-sub'
        appName: '$(apiAppName)'
        containers: '$(acrLoginServer)/auth-api:$(tag)'
```

The front-end version is identical with `auth-app`, `$(webAppName)` and the
`auth-app` repository substituted in.

Everything you configured by clicking maps directly: the Docker task is the same
task, the service connection names are the same strings, and the variable group
is linked by name instead of through a dialog.

---

> Azure's portal layout shifts every few months — menu items get renamed and
> moved. If a menu name here doesn't match what you see, the portal's own search
> box at the top of each resource blade will find it. The sequence of steps and
> the values you enter don't change.
