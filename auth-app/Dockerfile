# node 24 ships npm 11, the same major version that writes package-lock.json
# on the host. npm 10 (node:20) rejects an npm 11 lock file as out of sync.
FROM node:24-alpine AS build

WORKDIR /src

# Baked into the bundle at build time, because a React build is static: there
# is nothing left to read an environment variable at container start.
# The default points at the deployed Azure API. Pass an empty value to build
# for the local docker-compose setup instead, where nginx proxies /api/ to the
# auth-api container and the bundle therefore needs RELATIVE urls:
#   docker build --build-arg REACT_APP_API_BASE_URL= -t auth-app .
ARG REACT_APP_API_BASE_URL="https://auth-api-dev-awgfe0gaezf6hne6.centralindia-01.azurewebsites.net"

# This deliberately overrides any .env file copied in below: dotenv never
# overwrites a variable that is already set.
ENV REACT_APP_API_BASE_URL=$REACT_APP_API_BASE_URL

COPY package*.json ./

# npm clean install: installs the exact versions locked in package-lock.json
# and fails if the lock file and package.json disagree. Unlike `npm install`
# it never quietly updates the lock file, so every build is reproducible.
RUN npm ci

COPY . .

RUN npm run build


FROM nginx:alpine

COPY --from=build /src/build /usr/share/nginx/html

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