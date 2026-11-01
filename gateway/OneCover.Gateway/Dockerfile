FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY gateway/OneCover.Gateway/OneCover.Gateway.csproj gateway/OneCover.Gateway/
RUN dotnet restore gateway/OneCover.Gateway/OneCover.Gateway.csproj

COPY gateway/OneCover.Gateway/ gateway/OneCover.Gateway/
RUN dotnet publish gateway/OneCover.Gateway/OneCover.Gateway.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OneCover.Gateway.dll"]
