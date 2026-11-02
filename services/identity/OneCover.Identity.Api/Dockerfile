FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY contracts/OneCover.Contracts/OneCover.Contracts.csproj contracts/OneCover.Contracts/
COPY services/identity/OneCover.Identity.Api/OneCover.Identity.Api.csproj services/identity/OneCover.Identity.Api/
RUN dotnet restore services/identity/OneCover.Identity.Api/OneCover.Identity.Api.csproj

COPY contracts/ contracts/
COPY services/identity/OneCover.Identity.Api/ services/identity/OneCover.Identity.Api/
RUN dotnet publish services/identity/OneCover.Identity.Api/OneCover.Identity.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OneCover.Identity.Api.dll"]
