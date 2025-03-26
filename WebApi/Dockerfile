FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["WebApi.csproj", "./"]
RUN dotnet restore

# Copy the rest of the code
COPY . .
RUN dotnet build -c Debug -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Debug -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Explicitly copy Resources directory to ensure images are included
COPY --from=build /src/Resources /app/Resources

# Ensure it's a persistent process
ENV ASPNETCORE_URLS=http://+:5001
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Make shutdown timeout longer to facilitate debugging
ENV DOTNET_SHUTDOWN_TIMEOUT_SECONDS=120

EXPOSE 5001
ENTRYPOINT ["dotnet", "WebApi.dll"]