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

## Manual Setup

If you prefer not to use the Makefile, you can run the script directly:

```bash
# Start the application with proper Serveo configuration
./run-cinemagia.sh
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

## Accessing the Application

After starting the application properly, you can access:

- Web Frontend: http://localhost:5002
- Web API: http://localhost:5001
- Swagger UI: http://localhost:5001/swagger
- Email interface: http://localhost:8025