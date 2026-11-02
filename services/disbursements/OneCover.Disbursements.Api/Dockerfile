# Build context is the REPO ROOT (the API references contracts/, which sits outside this folder). Build with:
#   docker build -f services/disbursements/OneCover.Disbursements.Api/Dockerfile -t onecover-disbursements .

# ---- Stage 1: build. The SDK image has the compiler; it is big, so it is not what we ship. ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy ONLY the project files first and restore packages. Docker caches each step, so the slow package
# download is reused until a .csproj changes, instead of rerunning on every code edit.
COPY contracts/OneCover.Contracts/OneCover.Contracts.csproj contracts/OneCover.Contracts/
COPY services/disbursements/OneCover.Disbursements.Api/OneCover.Disbursements.Api.csproj services/disbursements/OneCover.Disbursements.Api/
RUN dotnet restore services/disbursements/OneCover.Disbursements.Api/OneCover.Disbursements.Api.csproj

# Now copy the source and publish a release build. /p:UseAppHost=false skips the native launcher we do not need.
COPY contracts/ contracts/
COPY services/disbursements/OneCover.Disbursements.Api/ services/disbursements/OneCover.Disbursements.Api/
RUN dotnet publish services/disbursements/OneCover.Disbursements.Api/OneCover.Disbursements.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Stage 2: run. The aspnet image only has the runtime, so the final image is small. ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listen on 8080 (the .NET images default to it; non-root users cannot bind port 80).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as the non-root "app" user built into the .NET images, so a bug in the app cannot act as root.
USER $APP_UID

# Secrets are NOT baked in. Pass them at run time as environment variables, e.g.
#   ConnectionStrings__DisbursementsDb, Jwt__Key, Jwt__Issuer, Jwt__Audience, InternalApiKey
ENTRYPOINT ["dotnet", "OneCover.Disbursements.Api.dll"]
