#!/bin/bash
set -e

# Script to automatically capture Serveo URLs and update configuration

echo "Starting Serveo URL Detection..."

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

if ! docker ps | grep -q "cinemagia-serveo-tunneling-backend"; then
    echo "Starting backend Serveo container..."
    $DOCKER_COMPOSE up -d serveo
fi

if ! docker ps | grep -q "cinemagia-serveo-tunneling-frontend"; then
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

echo "Setup complete. The Mollie payment integration should now work automatically."
echo "Backend URL: $backend_url"
echo "Frontend URL: $frontend_url" 