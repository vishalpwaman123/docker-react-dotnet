# auth-app — Project Reference

A small React 19 single-page app with two screens, **Sign Up** and **Sign In**,
styled with Bootstrap 5 and talking to the `auth-api` .NET Web API.

> This file documents *what the project is and how it is built*.
> For a step-by-step "run it in Docker" walkthrough, see [README-DOCKER.md](README-DOCKER.md).

---

## 1. What it does

| Route | Screen | Fields |
|---|---|---|
| `/` | redirect → `/signin` | — |
| `/signin` | Sign In | email, password |
| `/signup` | Sign Up | email, password, confirm password |

Sign up posts to the API and, on success, navigates to `/signin` after 1.5s.
Sign in posts to the API and, on success, shows a success alert with the
confirmed email and clears the password box. The API returns no token, so there
is no session to store and no protected area to navigate to.

---

## 2. Technology

| Piece | Choice |
|---|---|
| Framework | React 19 |
| Toolchain | Create React App (`react-scripts` 5) |
| Routing | react-router-dom 7 (`BrowserRouter`) |
| HTTP | axios 1.x, one shared client instance |
| Styling | Bootstrap 5.3 via CDN link in `public/index.html` — no npm package, no CSS of our own beyond `index.css` |
| Testing | Testing Library + Jest (wired up via CRA; no test files written yet) |
| Container | Multi-stage Dockerfile: node build → nginx serve |

---

## 3. Layout

```
auth-app/
├─ public/
│  └─ index.html          Bootstrap CDN <link>, <div id="root">
├─ src/
│  ├─ index.js            React root, StrictMode, reportWebVitals
│  ├─ App.jsx             BrowserRouter + the three routes
│  ├─ index.css           Minimal global styles
│  ├─ pages/
│  │  ├─ SignIn.jsx       Form, local validation, calls signIn()
│  │  └─ SignUp.jsx       Form, local validation, calls signUp()
│  ├─ api/
│  │  ├─ axiosClient.js   The one axios instance (baseURL, headers, timeout)
│  │  ├─ authApi.js       signUp() / signIn() — the only two calls in the app
│  │  └─ apiError.js      Turns any axios throw into a normal AuthResponse
│  └─ models/
│     └─ auth.js          Request/response shapes + builder helpers (JSDoc typedefs)
├─ nginx.conf             SPA fallback + /api/ reverse proxy
├─ Dockerfile             node:24-alpine build → nginx:alpine
├─ .env                   REACT_APP_API_BASE_URL
└─ index.js               Standalone scratch script (see §8) — not part of the React app
```

### Data flow

```
SignIn.jsx / SignUp.jsx
   │  buildSignInRequest() / buildSignUpRequest()   ← models/auth.js
   ▼
authApi.signIn() / signUp()                          ← api/authApi.js
   │
   ▼
axiosClient  (baseURL from REACT_APP_API_BASE_URL, 10s timeout)
   │
   ├─ 2xx  → response.data  (AuthResponse)
   └─ throw → toAuthResponse(error) → AuthResponse    ← api/apiError.js
   │
   ▼
setErrorMessage / setErrorList / setSuccessMessage
```

The point of `apiError.js` is that the pages only ever handle **one** shape.
Whether the API returned 409, or the server was unreachable and axios threw
with no `error.response`, the page still reads `.success`, `.message` and
`.errors`.

---

## 4. The API contract

Defined as JSDoc typedefs in [src/models/auth.js](src/models/auth.js) and
mirrored by the DTOs in `auth-api`:

```js
SignUpRequest  { email, password, confirmPassword }
SignInRequest  { email, password }
UserResponse   { id, email }
AuthResponse   { success, message, user: UserResponse|null, errors: string[] }
```

Calls:

| Function | Request | Endpoint |
|---|---|---|
| `signUp(req)` | `SignUpRequest` | `POST /api/auth/signup` |
| `signIn(req)` | `SignInRequest` | `POST /api/auth/signin` |

Components never build object literals inline and never touch axios directly —
field names live in one file, the base URL in another.

---

## 5. Validation

Client-side checks run before any request is sent:

**Sign In** — all fields non-empty.

**Sign Up** — all fields non-empty, and `password === confirmPassword`.

The server is stricter: it also requires a **minimum password length of 6**
(`SignUpRequest.Password`). The React form does not check length today, so a
5-character password is rejected by the API with a 400 rather than caught in
the browser. Adding a matching `MinLength` check in `SignUp.jsx` would align the
two.

Server validation messages arrive in `errors[]` and are rendered as a bullet
list inside the red alert, under `message`.

Both forms disable the submit button while `isSubmitting` is true, and reset it
in a `finally` block so it can never stay stuck.

---

## 6. Configuration

One environment variable, read in [src/api/axiosClient.js](src/api/axiosClient.js):

| Variable | Meaning |
|---|---|
| `REACT_APP_API_BASE_URL` | Base URL for all API calls. **Empty** means relative URLs (`/api/...`). |

CRA inlines `REACT_APP_*` variables at **build** time — they are baked into the
bundle, so changing one requires a rebuild, and nothing secret may go in them.

Two working modes:

| Mode | Value | How requests reach the API |
|---|---|---|
| Local dev | `https://localhost:7143` (or leave empty and rely on the CRA `proxy`) | Direct, or via CRA's dev-server proxy |
| Docker | *empty* | Relative `/api/...` → nginx proxies to the `auth-api` container |

[package.json](package.json) also sets `"proxy": "http://localhost:5024"`, which
only affects `npm start` — the CRA dev server forwards unmatched requests there,
avoiding CORS during local development. It has no effect on a production build.

---

## 7. Running it

### Locally

```bash
npm install
npm start          # http://localhost:3000
```

`auth-api` must be running on `http://localhost:5024` (or update the `proxy` /
`REACT_APP_API_BASE_URL`). `http://localhost:3000` is already in the API's CORS
allow-list.

Other scripts: `npm run build` (production bundle into `build/`), `npm test`,
`npm run eject`.

### In Docker

```bash
docker network create auth-net          # shared with auth-api; the default
                                        # bridge network gives no name resolution

docker build -t auth-app .
docker run -d --name auth-app --network auth-net -p 8080:80 auth-app
```

Open http://localhost:8080/signin.

Both containers **must** be on the same user-defined network, otherwise nginx
cannot resolve the name `auth-api` and every `/api/` call returns 502.

Notes on the image:

- **`node:24-alpine`** for the build stage — npm 11 matches the version that
  wrote `package-lock.json`; npm 10 (node:20) rejects it as out of sync.
- **`npm ci`**, not `npm install` — installs exactly what the lock file pins and
  fails on any disagreement, so builds are reproducible.
- **`ENV REACT_APP_API_BASE_URL=`** in the Dockerfile deliberately overrides any
  copied `.env`, because dotenv never overwrites an already-set variable. The
  result is a bundle that uses relative URLs.
- The second stage is plain `nginx:alpine` serving the static `build/` output —
  node is not in the final image.

---

## 8. nginx

[nginx.conf](nginx.conf) does two jobs:

```nginx
location /api/ {
    resolver 127.0.0.11 valid=10s;      # Docker's embedded DNS
    set $auth_api http://auth-api:8080; # via variable, so nginx boots even
    proxy_pass $auth_api;               # if auth-api is not up yet
}

location / {
    root /usr/share/nginx/html;
    try_files $uri $uri/ /index.html;   # React Router owns /signin, /signup
}
```

- Putting the upstream in a **variable** makes nginx resolve the name per
  request instead of at startup — without it, nginx refuses to start when the
  `auth-api` container is not running yet.
- `proxy_pass` has **no trailing slash** after `8080`, so `/api/auth/signup`
  passes through to the API unchanged.
- `try_files … /index.html` is what stops a hard refresh on `/signup` from
  returning a 404.
- Forwarded headers (`Host`, `X-Real-IP`, `X-Forwarded-For`,
  `X-Forwarded-Proto`) are set so the API sees the original client details.

---

## 9. `index.js` at the project root

[index.js](index.js) in the `auth-app` folder is **not** part of the React app —
`src/index.js` is. It is a standalone Express scratch script that opens a Redis
connection and a Postgres pool, logs whether each connected, and serves a health
route on `PORT` (default 3000). It was added while experimenting with Docker
networking against those two services and is unrelated to the auth flow.
Its dependencies (`express`, `redis`, `pg`) are not in `package.json`.

---

## 10. Known gaps

- No tests written (the Testing Library packages are installed but unused).
- No session or token handling — a successful sign in just shows a message.
- Sign-up password length is validated only on the server.
- No loading skeletons, no "forgot password", no email verification.

---

## 11. Related

- [auth-api](../auth-api/) — the .NET Web API this app calls
- [README-DOCKER.md](README-DOCKER.md) — the Docker walkthrough
