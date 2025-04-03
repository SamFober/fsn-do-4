# Cinemagia Application

This repository contains the Cinemagia movie ticket booking application with Mollie payment integration.

## Important: Proper Setup for Payment Integration

**Always** use the provided scripts or Makefile commands to start/manage the application to ensure correct Serveo URL configuration for Mollie payments.

❌ **DO NOT** use direct `docker-compose` commands as they will not set up the Serveo URLs properly, causing payment callbacks to fail with "example.serveo.net" errors.

## Quick Start

For convenience, a Makefile has been created with all common commands:

```bash
# Show available commands
make

# Start the application (with proper Serveo configuration)
make start

# Stop the application
make stop

# Restart the application (with updated Serveo URLs)
make restart

# Rebuild and start (after code changes)
make rebuild
```

## Environment Detection and Platform-Specific Instructions

The system automatically detects your operating environment and adjusts commands and settings accordingly:

### Windows/WSL
- Automatically detects Windows Subsystem for Linux
- Fixes script line endings using `dos2unix` (will install if available)
- Uses appropriate path handling for Windows/WSL environment

### macOS
- Automatically detects macOS systems
- Provides macOS-specific commands and workarounds
- Handles line ending conversions differently than WSL
- Recommends `brew install dos2unix` if needed for script fixes

### Linux
- Default environment when neither Windows nor macOS is detected
- Uses standard Unix paths and commands

### Getting Started on Your Platform

For all platforms, the simplest approach is to use:

```bash
make fix-scripts
make start
```

This will ensure scripts have proper permissions and line endings before starting the application with the appropriate settings for your environment.

### Troubleshooting WSL Issues

If you encounter "No such file or directory" errors even when files exist:
1. The script might have Windows line endings (CRLF)
2. The file might not have execute permissions

Our `make fix-scripts` command addresses both these issues automatically.

### WSL Network Configuration

For Docker networking to work properly in WSL:
1. Ensure Docker Desktop is running 
2. Create or edit `/etc/wsl.conf` with:
   ```
   [wsl2]
   localhostForwarding=true
   ```
3. Restart WSL with `wsl --shutdown` from a Windows command prompt

## Manual Setup

If you prefer not to use the Makefile, you can run the script directly:

```bash
# For Unix/Mac
chmod +x run-cinemagia.sh
./run-cinemagia.sh

# For Windows/WSL
bash run-cinemagia.sh
```

## How It Works

The application uses Serveo tunneling to make your local development environment accessible from the internet. This is necessary for Mollie to send webhook callbacks for payment status updates.

The `run-cinemagia.sh` script:
1. Starts the necessary containers
2. Automatically detects the dynamically-assigned Serveo URLs
3. Configures the WebAPI service with these URLs
4. Ensures the Mollie API key is properly set

## Troubleshooting

If you see payment redirect errors with URLs containing "example.serveo.net", it means:
1. The application wasn't started with `run-cinemagia.sh` or the Makefile
2. The Serveo URLs weren't properly detected or configured

Solution: Always use `make start` or `./run-cinemagia.sh` to start the application.

### Serveo Connection Issues (502 Bad Gateway)

If you encounter 502 errors when accessing Serveo URLs:
1. The Serveo tunnels might have expired or been regenerated - restart with `make restart`
2. Check logs with `docker logs cinemagia-serveo-tunneling-frontend`
3. If problems persist, consider using an alternative tunneling service like ngrok

## Accessing the Application

After starting the application properly, you can access:

- Web Frontend: http://localhost:5002
- Web API: http://localhost:5001
- Swagger UI: http://localhost:5001/swagger
- Email interface: http://localhost:8025