#!/bin/bash
set -e

# Script to automatically capture Serveo URLs and update configuration

echo "Starting Serveo URL Detection..."

# More specific OS detection
IS_WSL=0
IS_MAC=0
IS_LINUX=0
OS_TYPE="unknown"

if grep -q Microsoft /proc/version 2>/dev/null || [ -d "/mnt/c" ]; then
    echo "Windows/WSL environment detected."
    IS_WSL=1
    OS_TYPE="windows"
elif [ "$(uname)" == "Darwin" ]; then
    echo "macOS environment detected."
    IS_MAC=1
    OS_TYPE="mac"
else
    echo "Linux environment detected."
    IS_LINUX=1
    OS_TYPE="linux"
fi

# Determine which docker compose command to use
if command -v docker-compose &> /dev/null; then
    DOCKER_COMPOSE="docker-compose"
else
    DOCKER_COMPOSE="docker compose"
fi

# Function to extract Serveo URL from container logs
extract_serveo_url() {
    container_name=$1
    max_attempts=30
    attempt=0
    
    echo "Waiting for $container_name to generate a URL..."
    
    while [ $attempt -lt $max_attempts ]; do
        # Get the last 50 lines of logs which should contain the serveo.net URL
        logs=$(docker logs $container_name 2>&1 | tail -50)
        
        # Improved regex pattern to more reliably extract the URL
        temp_url=$(echo "$logs" | grep -o 'https://[a-zA-Z0-9][a-zA-Z0-9-]*\.serveo\.net' | head -1)
        
        if [ -n "$temp_url" ]; then
            echo "Found URL for $container_name: $temp_url"
            # Clean the URL to ensure no extra characters
            clean_url=$(echo "$temp_url" | tr -d '\r\n\t ' | grep -o 'https://[a-zA-Z0-9][a-zA-Z0-9-]*\.serveo\.net')
            echo "$clean_url"
            return 0
        fi
        
        echo "Attempt $((attempt+1))/$max_attempts: No URL found yet for $container_name, waiting 2 seconds..."
        sleep 2
        attempt=$((attempt+1))
    done
    
    echo "Failed to extract URL for $container_name after $max_attempts attempts"
    return 1
}

# Make sure Serveo containers are running
echo "Ensuring Serveo containers are running..."

# We need to use grep pattern that works in both GNU and BSD grep
if ! docker ps | grep -E "cinemagia-serveo-tunneling-backend|cinemagia-serveo-tunnel" > /dev/null; then
    echo "Starting backend Serveo container..."
    $DOCKER_COMPOSE up -d serveo
fi

if ! docker ps | grep -E "cinemagia-serveo-tunneling-frontend|cinemagia-serveo-front" > /dev/null; then
    echo "Starting frontend Serveo container..."
    $DOCKER_COMPOSE up -d serveo-frontend
fi

# Extract URLs
echo "Extracting backend URL..."
backend_url=$(extract_serveo_url "cinemagia-serveo-tunneling-backend")
echo "Extracting frontend URL..."
frontend_url=$(extract_serveo_url "cinemagia-serveo-tunneling-frontend")

if [ -z "$backend_url" ] || [ -z "$frontend_url" ]; then
    echo "Failed to extract one or both URLs. Please check the Serveo containers."
    
    # Check container logs for debugging
    echo "Backend container logs (last 20 lines):"
    docker logs cinemagia-serveo-tunneling-backend 2>&1 | tail -20
    
    echo "Frontend container logs (last 20 lines):"
    docker logs cinemagia-serveo-tunneling-frontend 2>&1 | tail -20
    
    # On Windows/WSL, sometimes the container can't access the host
    if [ "$IS_WSL" -eq 1 ]; then
        echo "
WSL TROUBLESHOOTING:
If you're using WSL on Windows, make sure:
1. Docker Desktop is running 
2. 'host.docker.internal' is properly configured
3. Try adding the following to your /etc/wsl.conf file:

[wsl2]
localhostForwarding=true

"
    fi
    
    exit 1
fi

echo "Detected URLs:"
echo "Backend: $backend_url"
echo "Frontend: $frontend_url"

# Set environment variables for docker-compose
export SERVEO_BACKEND_URL="$backend_url"
export SERVEO_FRONTEND_URL="$frontend_url"

# Update the WebAPI container environment
echo "Updating WebAPI container environment..."

# Stop the existing WebAPI container if it's running
if docker ps | grep -q "cinemagia-webapi"; then
    echo "Stopping existing WebAPI container..."
    $DOCKER_COMPOSE stop webapi
fi

# Start the WebAPI container with updated environment variables
echo "Starting WebAPI container with updated Serveo URLs..."

# Pass environment variables directly to the docker-compose command
SERVEO_BACKEND_URL="$backend_url" SERVEO_FRONTEND_URL="$frontend_url" $DOCKER_COMPOSE up -d --remove-orphans webapi

# Verify the environment variables are correctly set
echo "Verifying environment variables in WebAPI container..."
docker exec cinemagia-webapi env | grep -E "Serveo|SERVEO" || echo "⚠️ WARNING: Serveo variables not found in WebAPI container!"

echo "Setup complete. The Mollie payment integration should now work automatically."
echo "Backend URL: $backend_url"
echo "Frontend URL: $frontend_url" 