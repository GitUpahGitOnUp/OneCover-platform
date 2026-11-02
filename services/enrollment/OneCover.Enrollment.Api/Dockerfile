FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY contracts/OneCover.Contracts/OneCover.Contracts.csproj contracts/OneCover.Contracts/
COPY services/enrollment/OneCover.Enrollment.Api/OneCover.Enrollment.Api.csproj services/enrollment/OneCover.Enrollment.Api/
RUN dotnet restore services/enrollment/OneCover.Enrollment.Api/OneCover.Enrollment.Api.csproj

COPY contracts/ contracts/
COPY services/enrollment/OneCover.Enrollment.Api/ services/enrollment/OneCover.Enrollment.Api/
RUN dotnet publish services/enrollment/OneCover.Enrollment.Api/OneCover.Enrollment.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OneCover.Enrollment.Api.dll"]
