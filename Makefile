# Cinemagia Application Management
# This Makefile ensures proper Serveo URL configuration for Mollie payments

# Determine the docker-compose command
DOCKER_COMPOSE := $(shell command -v docker-compose 2> /dev/null || echo "docker compose")

# Detect OS
IS_WSL := 0
IS_MAC := 0
IS_LINUX := 0
OS_TYPE := unknown

ifeq ($(shell grep -q Microsoft /proc/version 2>/dev/null || [ -d "/mnt/c" ] && echo 1 || echo 0),1)
    IS_WSL := 1
    OS_TYPE := windows
else ifeq ($(shell uname),Darwin)
    IS_MAC := 1
    OS_TYPE := mac
else
    IS_LINUX := 1
    OS_TYPE := linux
endif

# Display detected environment
$(info OS Environment: $(OS_TYPE))

.PHONY: start stop restart rebuild logs status clean help fix-permissions fix-scripts

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
	@echo "  make fix-scripts - Fix script permissions and line endings (for Windows/WSL users)"
	@echo ""
	@echo "Always use these commands instead of direct docker-compose commands"
	@echo "to ensure proper Serveo URL configuration for payments."

# Fix script permissions and line endings (especially important for Windows/WSL users)
fix-scripts:
	@echo "Fixing script permissions and line endings..."
	@if [ $(IS_WSL) -eq 1 ]; then \
		echo "WSL environment detected, fixing line endings..."; \
		if command -v dos2unix > /dev/null; then \
			dos2unix run-cinemagia.sh serveo-init.sh; \
		else \
			echo "Warning: dos2unix not found. Please install it with: sudo apt-get install dos2unix"; \
			echo "Attempting alternate fix..."; \
			sed -i 's/\r$$//' run-cinemagia.sh serveo-init.sh; \
		fi; \
	elif [ $(IS_MAC) -eq 1 ]; then \
		echo "macOS environment detected, ensuring proper line endings..."; \
		if command -v dos2unix > /dev/null; then \
			dos2unix run-cinemagia.sh serveo-init.sh; \
		else \
			echo "Tip: Install dos2unix with: brew install dos2unix"; \
			echo "Using sed as alternative..."; \
			sed -i '' 's/\r$$//' run-cinemagia.sh serveo-init.sh 2>/dev/null || true; \
		fi; \
	fi
	@chmod +x run-cinemagia.sh serveo-init.sh
	@echo "Scripts fixed and ready to use with $(OS_TYPE) environment."

# Start the application using run-cinemagia.sh script
start: fix-scripts
	@echo "Starting Cinemagia with proper Serveo URL configuration..."
	@bash run-cinemagia.sh

# Stop all containers
stop:
	@echo "Stopping all containers..."
	@$(DOCKER_COMPOSE) down

# Restart with proper Serveo configuration
restart: stop start

# Rebuild and start containers
rebuild: fix-scripts
	@echo "Rebuilding containers with proper Serveo configuration..."
	@$(DOCKER_COMPOSE) down
	@$(DOCKER_COMPOSE) build
	@bash run-cinemagia.sh

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