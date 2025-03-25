FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Kopieer csproj en herstel afhankelijkheden
COPY ["WebApi.csproj", "./"]
RUN dotnet restore

# Kopieer de rest van de code
COPY . .
RUN dotnet build -c Debug -o /app/build

# Publiceren
FROM build AS publish
RUN dotnet publish -c Debug -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Explicitly copy Resources directory to ensure images are included
COPY --from=build /src/Resources /app/Resources

# Zorg dat het een blijvend proces is
ENV ASPNETCORE_URLS=http://+:5001
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Maak de wachttijd op beëindiging langer om debugging te vergemakkelijken
ENV DOTNET_SHUTDOWN_TIMEOUT_SECONDS=120

EXPOSE 5001
ENTRYPOINT ["dotnet", "WebApi.dll"]