# Cinemagia Application Management
# This Makefile ensures proper Serveo URL configuration for Mollie payments

# Determine the docker-compose command
DOCKER_COMPOSE := $(shell command -v docker-compose 2> /dev/null || echo "docker compose")

.PHONY: start stop restart rebuild logs status clean help

# Default target
help:
	@echo "Cinemagia Application Management"
	@echo "----------------------------------"
	@echo "Available commands:"
	@echo "  make start    - Start the application with proper Serveo configuration"
	@echo "  make stop     - Stop all containers"
	@echo "  make restart  - Restart containers with updated Serveo URLs"
	@echo "  make rebuild  - Rebuild and start containers with proper Serveo configuration"
	@echo "  make logs     - View logs from all containers"
	@echo "  make status   - Check container status"
	@echo "  make clean    - Remove all containers and volumes"
	@echo ""
	@echo "Always use these commands instead of direct docker-compose commands"
	@echo "to ensure proper Serveo URL configuration for payments."

# Start the application using run-cinemagia.sh script
start:
	@echo "Starting Cinemagia with proper Serveo URL configuration..."
	@chmod +x run-cinemagia.sh
	@./run-cinemagia.sh

# Stop all containers
stop:
	@echo "Stopping all containers..."
	@$(DOCKER_COMPOSE) down

# Restart with proper Serveo configuration
restart: stop start

# Rebuild and start containers
rebuild:
	@echo "Rebuilding containers with proper Serveo configuration..."
	@$(DOCKER_COMPOSE) down
	@$(DOCKER_COMPOSE) build
	@chmod +x run-cinemagia.sh
	@./run-cinemagia.sh

# View logs
logs:
	@echo "Viewing container logs (Ctrl+C to exit)..."
	@$(DOCKER_COMPOSE) logs -f

# Check container status
status:
	@echo "Container status:"
	@$(DOCKER_COMPOSE) ps

# Clean up everything
clean:
	@echo "Removing all containers and volumes..."
	@$(DOCKER_COMPOSE) down -v
	@echo "Cleanup complete." 