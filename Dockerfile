FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Tracker.slnx ./
COPY src ./src

RUN dotnet restore Tracker.slnx
RUN dotnet publish src/Tracker.Api/Tracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Health-ready container defaults:
# - bind to container port 8080
# - run in production mode
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

# SQLite data directory; bind a volume here in compose/prod.
RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "Tracker.Api.dll"]
