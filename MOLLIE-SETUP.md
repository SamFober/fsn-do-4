# Mollie Payment Integration Guide

This guide explains how to set up and use the Mollie payment integration in the Cinemagia application.

## Automatic Setup

We've created a script that handles the entire setup process automatically. Simply run:

```bash
./run-cinemagia.sh
```

This script will:

1. Check for and prompt for your Mollie API key if not already configured
2. Start all the Docker containers
3. Detect the Serveo URLs automatically
4. Configure the WebAPI service with the correct URLs
5. Start the application with all services properly connected

## Manual Setup (if needed)

If you prefer to set up manually, or if the automatic script encounters issues, follow these steps:

### 1. Configure Mollie API Key

Add your Mollie API key to `WebApi/appsettings.json`:

```json
{
  "Mollie": {
    "ApiKey": "YOUR_MOLLIE_API_KEY"
  }
}
```

### 2. Start the Serveo Tunneling Containers

```bash
docker-compose up -d serveo serveo-frontend
```

### 3. Get the Serveo URLs

```bash
# For backend
docker logs cinemagia-serveo-tunneling-backend | grep -o 'https://[a-zA-Z0-9]\+\.serveo\.net' | head -1

# For frontend
docker logs cinemagia-serveo-tunneling-frontend | grep -o 'https://[a-zA-Z0-9]\+\.serveo\.net' | head -1
```

### 4. Create or Update .env File

Create a file called `.env` with the following content (replacing the URLs with the ones you found):

```
SERVEO_BACKEND_URL=https://your-backend-url.serveo.net
SERVEO_FRONTEND_URL=https://your-frontend-url.serveo.net
```

### 5. Start or Restart the WebAPI Container

```bash
docker-compose up -d webapi
```

## How It Works

The system uses Serveo (a public URL tunneling service) to make your local development environment accessible from the internet. This is necessary for Mollie to send webhook callbacks when payment status changes.

- **Frontend URL**: Used for redirecting users after payment completion or cancellation
- **Backend URL**: Used for sending webhook notifications about payment status changes

## Troubleshooting

If payments are not working, check the following:

1. **Serveo URLs**: Make sure the Serveo tunnels are working. You can check the container logs with:
   ```
   docker logs cinemagia-serveo-tunneling-backend
   docker logs cinemagia-serveo-tunneling-frontend
   ```

2. **WebAPI Configuration**: Make sure the WebAPI container has the correct environment variables:
   ```
   docker exec cinemagia-webapi env | grep Serveo
   ```

3. **Mollie API Key**: Ensure your Mollie API key is correct and has the proper permissions.

4. **WebAPI Logs**: Check for any errors in the WebAPI logs:
   ```
   docker logs cinemagia-webapi
   ```

5. **Restart Setup**: If all else fails, run the automatic setup script again:
   ```
   ./run-cinemagia.sh
   ```

## Important Notes

- The Serveo URLs change if the containers are restarted, but not if they are just rebuilt. The automatic setup script handles this for you.
- For testing payments, you should use the Mollie test environment API key.
- The system is configured to work without HTTPS (using HTTP) on your local development environment, but in production, you would use proper HTTPS URLs. 