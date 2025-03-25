FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Kopieer csproj en herstel afhankelijkheden
COPY ["FrontEnd.csproj", "./"]
RUN dotnet restore

# Kopieer de rest van de code
COPY . .
RUN dotnet build -c Debug -o /app/build

# Publiceren
FROM build AS publish
RUN dotnet publish -c Debug -o /app/publish

# Runtime image
FROM nginx:stable-alpine AS final
WORKDIR /usr/share/nginx/html
# Copy the published app content directly to nginx html directory
COPY --from=publish /app/publish/wwwroot .
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Belangrijke fix: nginx moet draaien in de voorgrond (niet als daemon)
# anders stopt de container direct
CMD ["nginx", "-g", "daemon off;"]

EXPOSE 80