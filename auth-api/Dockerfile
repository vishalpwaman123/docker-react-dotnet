# =====================================================================
# STAGE 1 - BUILD
# Uses the .NET 10 SDK image (~800 MB) which has the compilers and
# NuGet. This whole stage is discarded at the end; only the files we
# explicitly copy out of it in stage 2 survive.
# =====================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Working directory inside the container. Created if absent.
# Linux forward slashes, even though the host is Windows.

WORKDIR /src

# Copy ONLY the project file first - not the source yet.
# This is deliberate; see the note on the restore step below.

COPY AuthApi.csproj ./

# Download the NuGet packages listed in the csproj.
# This layer's only input is AuthApi.csproj, so Docker reuses it from
# cache whenever that file is unchanged - meaning editing a .cs file
# skips this 16-second download entirely. That is the entire reason
# the COPY above was split from the COPY below.

RUN dotnet restore AuthApi.csproj

# Now bring in everything else: Program.cs, Controllers, Services,
# Models, Migrations. Filtered by .dockerignore, which is what keeps
# the 110 MB bin/ folder out of the image.

COPY . ./

# Compile and lay out a ready-to-run app.
#   --configuration Release  optimised, no debug symbols (default is Debug)
#   --no-restore             the RUN above already restored
#   --output /app/publish    fixed path, outside /src, that stage 2 reads
# Trailing \ continues the line; nothing may follow it, not even a space.

RUN dotnet publish AuthApi.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish


# =====================================================================
# STAGE 2 - RUNTIME
# A second FROM starts a brand-new image. The aspnet image (~220 MB)
# has the runtime but no compilers or NuGet, so none of the build
# toolchain ships to production.
# =====================================================================

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

# The .NET base images ship a pre-created non-root user called "app".
# Running as non-root means a compromised process cannot write outside
# its own files. This is also why the port below is 8080 and not 80:
# non-root users cannot bind ports under 1024.

USER app

# Documents the listening port for whoever runs this image. It does
# NOT publish the port by itself - that is -p on `docker run`.
# No ENV is needed here: the aspnet base image already sets
# ASPNETCORE_HTTP_PORTS=8080. Setting ASPNETCORE_URLS as well only
# produces an "Overriding HTTP_PORTS" warning at startup.
EXPOSE 8080

# The multi-stage payoff: reach back into the stage named `build` and
# take only the publish output. The SDK, the NuGet cache and the
# intermediate obj/ files are all left behind.

COPY --from=build /app/publish ./

# Runs when a CONTAINER STARTS (unlike RUN, which runs at build time).
# JSON array form on purpose - it execs dotnet directly instead of
# wrapping it in a shell, so Docker's stop signal reaches the process
# and the app shuts down cleanly instead of being killed after 10s.

ENTRYPOINT ["dotnet", "AuthApi.dll"]


# TERMINAL COMMAND

# docker build -t auth-api:latest .
# docker images auth-api:latest
# docker rm -f auth-api
# docker run -d --name auth-api -p 8081:8080 -e ASPNETCORE_ENVIRONMENT=Development auth-api:latest

# SWAGGER
# http://localhost:8081/swagger/index.html

# Add Database connection with docker run

# docker run -d `
#   --name auth-api `
#   --network auth-net `
#   -p 8081:8080 `
#   -e ASPNETCORE_ENVIRONMENT=Development `
#   -e "ConnectionStrings__DefaultConnection=Server=host.docker.internal,1433;Database=AuthDB;User Id=sa;Password=StrongPassword@123;TrustServerCertificate=True;" `
#   auth-api:latest