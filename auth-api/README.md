# auth-api — Project Reference

An ASP.NET Core (.NET 10) Web API that backs the `auth-app` React front end.
It exposes exactly two endpoints — sign up and sign in — over a single
`Profiles` table in SQL Server, and returns one response envelope for every
outcome.

> This file documents *what the project is and how it is built*.
> For a step-by-step "run it in Docker" walkthrough, see [README-DOCKER.md](README-DOCKER.md).

---

## 1. What it does

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/auth/signup` | POST | Create a new account (email + password) |
| `/api/auth/signin` | POST | Verify an email/password pair |

There is no token, cookie or session. Sign in only answers the question
"do these credentials match a stored profile?". Adding JWT later means adding a
token issuer in `AuthService` and returning it inside `ApiResponse` — nothing
else in the pipeline has to change.

---

## 2. Technology

| Piece | Choice | Where |
|---|---|---|
| Framework | ASP.NET Core, `net10.0` | [AuthApi.csproj](AuthApi.csproj) |
| ORM | Entity Framework Core 10 (SQL Server provider) | [Data/AuthDbContext.cs](Data/AuthDbContext.cs) |
| Password hashing | `PasswordHasher<Profile>` (PBKDF2, random per-password salt) | [Services/AuthService.cs](Services/AuthService.cs) |
| API docs | Swashbuckle / Swagger UI (Development only) | [Program.cs](Program.cs) |
| Container | Multi-stage Dockerfile, non-root `app` user, port 8080 | [Dockerfile](Dockerfile) |

---

## 3. Layout

```
auth-api/
├─ Program.cs                 Composition root: DI, CORS, pipeline order
├─ Controllers/
│  └─ AuthController.cs       The only controller. Thin: validate → service → status code
├─ Services/
│  ├─ IAuthService.cs         AuthOutcome enum + AuthResult
│  └─ AuthService.cs          All sign up / sign in rules
├─ Repositories/
│  ├─ IProfileRepository.cs   EmailExists / GetByEmail / Add
│  └─ ProfileRepository.cs    The only class that touches the DbContext
├─ Data/
│  └─ AuthDbContext.cs        DbSet<Profile> + unique index on Email
├─ Models/
│  └─ Profile.cs              The one entity → the "Profiles" table
├─ DTOs/
│  ├─ SignUpRequest.cs        email, password, confirmPassword (+ validation attributes)
│  ├─ SignInRequest.cs        email, password
│  └─ ApiResponse.cs          The shared envelope + UserResponse
├─ Middleware/
│  └─ ExceptionHandlingMiddleware.cs   Global catch → safe 500
├─ Migrations/                EF Core migration: InitialCreate
└─ Dockerfile                 SDK build stage → aspnet runtime stage
```

### Request flow

```
HTTP request
   │
   ├─ ExceptionHandlingMiddleware   (wraps everything below)
   ├─ Swagger UI                    (Development only)
   ├─ CORS: ReactAppCorsPolicy
   │
   └─ AuthController
        │  ModelState check → 400 with ApiResponse.Fail
        └─ IAuthService
             │  business rules, hashing, normalisation
             └─ IProfileRepository
                  └─ AuthDbContext → SQL Server
```

Each layer only knows the interface of the one below it, so the controller can
be tested with a fake `IAuthService` and the service with a fake
`IProfileRepository`.

---

## 4. Data model

`Profile` — one row per account, table `Profiles`:

| Column | Type | Notes |
|---|---|---|
| `Id` | int, identity | Primary key |
| `Email` | nvarchar(256), required | **Unique index** (`AuthDbContext.OnModelCreating`) |
| `PasswordHash` | nvarchar(512), required | PBKDF2 hash — the plain password is never stored |
| `CreatedAt` | datetime2 (UTC) | Set on insert |
| `ModifiedAt` | datetime2 (UTC) | Same as `CreatedAt` on insert |

Emails are normalised with `Trim().ToLowerInvariant()` before any lookup or
insert, so `"  Bob@Example.com "` and `bob@example.com` are the same account.

---

## 5. The response envelope

Every response — success or failure, 200 or 500 — has the same JSON shape
(camelCase on the wire):

```json
{
  "success": true,
  "message": "Account created successfully.",
  "user": { "id": 1, "email": "bob@example.com" },
  "errors": []
}
```

`UserResponse` has no password property of any kind, so a hash cannot be
returned by accident.

`[ApiController]`'s default validation format is switched off
(`SuppressModelStateInvalidFilter = true` in `Program.cs`) precisely so that a
400 uses this envelope too, letting the React client always read
`result.success` / `result.message` / `result.errors`.

---

## 6. Endpoint contracts

### POST /api/auth/signup

Request:

```json
{ "email": "bob@example.com", "password": "secret123", "confirmPassword": "secret123" }
```

Validation (`SignUpRequest` data annotations):

- `email` — required, must be a valid email address
- `password` — required, minimum 6 characters
- `confirmPassword` — required, must equal `password` (checked, then discarded — never stored)

Responses:

| Status | When |
|---|---|
| `201 Created` | Account created |
| `400 Bad Request` | A validation rule failed; messages in `errors[]` |
| `409 Conflict` | An account with that email already exists |

### POST /api/auth/signin

Request:

```json
{ "email": "bob@example.com", "password": "secret123" }
```

Validation: `email` required + valid format, `password` required. There is
deliberately **no** minimum length here — on sign in only the hash comparison
matters.

Responses:

| Status | When |
|---|---|
| `200 OK` | Credentials matched |
| `400 Bad Request` | A validation rule failed |
| `401 Unauthorized` | Unknown email **or** wrong password |

Both failure paths return the identical message `"Invalid email or password"`,
built in one place (`AuthService.InvalidCredentialsResult`) so they cannot drift
apart and let someone probe which emails are registered.

---

## 7. Configuration

Nothing is hardcoded — everything comes from configuration, so environment
variables can override it in a container.

[appsettings.json](appsettings.json):

| Key | Meaning |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Cors:AllowedOrigins` | Array of origins allowed to call the API — currently `http://localhost:3000`, `http://localhost`, `http://localhost:8080` |
| `Logging:LogLevel` | Standard ASP.NET Core log levels |

The connection string is required: `Program.cs` throws
`InvalidOperationException` at startup if it is missing, so a misconfigured
container fails loudly instead of at the first request.

Overriding from Docker uses the double-underscore convention:

```
-e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=AuthDB;User Id=sa;Password=StrongPassword@123;TrustServerCertificate=True;"
```

### Local ports

From [Properties/launchSettings.json](Properties/launchSettings.json):

- `http` profile — `http://localhost:5024`
- `https` profile — `https://localhost:7143` (plus 5024)
- Browser opens on `/swagger`

---

## 8. Running it

### Locally

```bash
dotnet restore
dotnet ef database update      # applies the InitialCreate migration
dotnet run
```

Then open http://localhost:5024/swagger.

Swagger is registered **only when `ASPNETCORE_ENVIRONMENT=Development`**, which
is why the Docker commands below pass that variable.

### In Docker

```bash
docker build -t auth-api:latest .

docker network create auth-net          # shared with auth-app

docker run -d --name auth-api --network auth-net -p 8081:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=AuthDB;User Id=sa;Password=StrongPassword@123;TrustServerCertificate=True;" \
  auth-api:latest
```

Swagger: http://localhost:8081/swagger/index.html

Notes on the image:

- **Two stages.** The SDK image (~800 MB) compiles; only `/app/publish` is
  copied into the `aspnet` runtime image (~220 MB), so no compiler or NuGet
  cache ships.
- **`COPY AuthApi.csproj` before `COPY . .`** — the `dotnet restore` layer is
  cached whenever the csproj is unchanged, so editing a `.cs` file skips the
  restore entirely.
- **`USER app`** — the base image's pre-created non-root user. That is why the
  port is 8080, not 80: non-root cannot bind ports under 1024.
- **`ENTRYPOINT` in JSON array form** — execs `dotnet` directly so Docker's
  stop signal reaches the process and shutdown is clean.
- `.dockerignore` keeps `bin/`, `obj/`, `.vs/` and `*.user` out of the build
  context.

---

## 9. Migrations

One migration exists: `20260828154926_InitialCreate`, which creates `Profiles`
and its unique index on `Email`.

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```

Migrations are **not** applied automatically at startup — run
`dotnet ef database update` (or generate a script) against the target database
before starting the API.

---

## 10. Security notes

- Passwords are hashed with PBKDF2 + a random per-password salt; the plaintext
  exists only in memory for the moment it takes to hash or verify.
- Nothing password-related is ever logged, returned, or serialised.
- Unhandled exceptions are logged in full but the caller only sees
  `"Something went wrong. Please try again later."` — internal details never
  reach the browser.
- CORS is an explicit origin allow-list, not `AllowAnyOrigin`.
- Sign-in failures are indistinguishable to the caller regardless of cause.

Known gaps, deliberate for this exercise: no rate limiting, no account lockout,
no email verification, no password reset, no auth token, and HTTP (not HTTPS)
inside the container.

---

## 11. Related

- [auth-app](../auth-app/) — the React front end that calls these two endpoints
- [README-DOCKER.md](README-DOCKER.md) — the Docker walkthrough
