# node 24 ships npm 11, the same major version that writes package-lock.json
# on the host. npm 10 (node:20) rejects an npm 11 lock file as out of sync.
FROM node:24-alpine AS build

WORKDIR /app

COPY package.json package-lock.json ./

# npm clean install: installs the exact versions locked in package-lock.json
# and fails if the lock file and package.json disagree. Unlike `npm install`
# it never quietly updates the lock file, so every build is reproducible.
RUN npm ci

COPY . .

# The browser talks to nginx, and nginx proxies /api/ to the auth-api
# container (see nginx.conf). So the bundle must use RELATIVE urls, which
# is what an empty base url produces.
# This ENV deliberately overrides any .env file copied in above: dotenv
# never overwrites a variable that is already set.
ENV REACT_APP_API_BASE_URL=

RUN npm run build


FROM nginx:alpine

COPY --from=build /app/build /usr/share/nginx/html

COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# TERMINAL COMMAND

# Both containers must share a user-defined network, otherwise nginx
# cannot resolve the name "auth-api" and every /api/ call returns 502.
# The default bridge network does NOT provide name resolution.

# docker network create auth-net

# docker rm -f auth-api
# docker run -d --name auth-api --network auth-net -p 8081:8080 `
#   -e ASPNETCORE_ENVIRONMENT=Development `
#   -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=AuthDB;User Id=sa;Password=StrongPassword@123;TrustServerCertificate=True;" `
#   auth-api:latest

# docker build -t auth-app .
# docker run -d --name auth-app --network auth-net -p 8080:80 auth-app

# WEB UI URL
# http://localhost:8080/signin