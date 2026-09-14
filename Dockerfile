FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY PocketRankingsPlayerProfile.slnx ./
COPY src/PocketRankingsPlayerProfile/PocketRankingsPlayerProfile.csproj src/PocketRankingsPlayerProfile/
RUN dotnet restore src/PocketRankingsPlayerProfile/PocketRankingsPlayerProfile.csproj
COPY src/PocketRankingsPlayerProfile/ src/PocketRankingsPlayerProfile/
RUN dotnet publish src/PocketRankingsPlayerProfile/PocketRankingsPlayerProfile.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PocketRankingsPlayerProfile.dll"]
