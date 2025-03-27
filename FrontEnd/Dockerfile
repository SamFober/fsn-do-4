FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["FrontEnd.csproj", "./"]
RUN dotnet restore

# Copy the rest of the code
COPY . .
RUN dotnet build -c Debug -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish -c Debug -o /app/publish

# Runtime image
FROM nginx:stable-alpine AS final
WORKDIR /usr/share/nginx/html
# Copy the published app content directly to nginx html directory
COPY --from=publish /app/publish/wwwroot .
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Important fix: nginx must run in the foreground (not as daemon)
# otherwise the container stops immediately
CMD ["nginx", "-g", "daemon off;"]

EXPOSE 80