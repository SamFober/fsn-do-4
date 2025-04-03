#!/bin/bash
set -e

# Script to run Cinemagia with Mollie Payment Integration
# This script will:
# 1. Start all containers
# 2. Detect Serveo URLs
# 3. Configure the WebAPI service

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

# First, remove any existing .env file to avoid issues
rm -f .env 2>/dev/null || true

# Check if docker and docker-compose are installed
if ! command -v docker &> /dev/null; then
    echo "Docker is not installed. Please install Docker first."
    exit 1
fi

if ! (command -v docker-compose &> /dev/null || docker compose version &> /dev/null); then
    echo "Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Determine which docker compose command to use
if command -v docker-compose &> /dev/null; then
    DOCKER_COMPOSE="docker-compose"
else
    DOCKER_COMPOSE="docker compose"
fi

# Make sure the serveo-init.sh script is executable
chmod +x serveo-init.sh 2>/dev/null || true

echo "================================================================================"
echo "Cinemagia - Automatic Mollie Payment Setup"
echo "================================================================================"
echo ""
echo "This script will configure the Cinemagia application with Mollie payment integration."
echo ""

# Check if the Mollie API key is set
MOLLIE_API_KEY=""

# Check if WebApi directory exists
if [ ! -d "WebApi" ]; then
    echo "⚠️  WebApi directory not found. Creating it..."
    mkdir -p WebApi
fi

# Check if the Mollie API key is in appsettings.json
if [ -f "WebApi/appsettings.json" ]; then
    if grep -q "\"ApiKey\"" "WebApi/appsettings.json"; then
        echo "✅ Mollie API key found in appsettings.json"
    else
        echo "⚠️  Mollie API key not found in appsettings.json"
        echo ""
        echo "Please enter your Mollie API key (or press Enter to skip):"
        read -p "> " MOLLIE_API_KEY
        
        if [ -n "$MOLLIE_API_KEY" ]; then
            # Safely update the JSON file with jq if available, or inform the user to do it manually
            if command -v jq &> /dev/null; then
                echo "Updating appsettings.json with your Mollie API key..."
                # Create a temporary file
                tmp_file=$(mktemp)
                
                # Read the current file
                content=$(cat "WebApi/appsettings.json")
                
                # If Mollie section doesn't exist, create it
                if ! echo "$content" | jq -e '.Mollie' &> /dev/null; then
                    echo "$content" | jq '. + {"Mollie": {"ApiKey": "'"$MOLLIE_API_KEY"'"}}' > "$tmp_file"
                else
                    # If it exists, update it
                    echo "$content" | jq '.Mollie.ApiKey = "'"$MOLLIE_API_KEY"'"' > "$tmp_file"
                fi
                
                # Replace the original file
                cat "$tmp_file" > "WebApi/appsettings.json"
                rm "$tmp_file"
                
                echo "✅ Updated appsettings.json with your Mollie API key"
            else
                echo "⚠️  jq command not found."
                echo "Please manually add the following to WebApi/appsettings.json:"
                echo ""
                echo '"Mollie": {'
                echo '  "ApiKey": "'"$MOLLIE_API_KEY"'"'
                echo '}'
                echo ""
                read -p "Press Enter when you've updated the file..."
            fi
        else
            echo "⚠️  No Mollie API key provided. You'll need to add it to appsettings.json manually."
        fi
    fi
else
    echo "⚠️  WebApi/appsettings.json not found. Please create it with your Mollie API key."
    
    # Create a minimal appsettings.json if it doesn't exist
    echo "Creating a minimal appsettings.json file..."
    mkdir -p WebApi
    echo '{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "*"
}' > WebApi/appsettings.json
    
    echo "Please enter your Mollie API key (or press Enter to skip):"
    read -p "> " MOLLIE_API_KEY
    
    if [ -n "$MOLLIE_API_KEY" ]; then
        # Safely update the JSON file with jq if available, or inform the user to do it manually
        if command -v jq &> /dev/null; then
            echo "Updating appsettings.json with your Mollie API key..."
            # Create a temporary file
            tmp_file=$(mktemp)
            
            # Read the current file
            content=$(cat "WebApi/appsettings.json")
            
            # Add Mollie section
            echo "$content" | jq '. + {"Mollie": {"ApiKey": "'"$MOLLIE_API_KEY"'"}}' > "$tmp_file"
            
            # Replace the original file
            cat "$tmp_file" > "WebApi/appsettings.json"
            rm "$tmp_file"
            
            echo "✅ Updated appsettings.json with your Mollie API key"
        else
            echo "⚠️  jq command not found."
            echo "Please manually add the following to WebApi/appsettings.json:"
            echo ""
            echo '"Mollie": {'
            echo '  "ApiKey": "'"$MOLLIE_API_KEY"'"'
            echo '}'
            echo ""
            read -p "Press Enter when you've updated the file..."
        fi
    else
        echo "⚠️  No Mollie API key provided. You'll need to add it to appsettings.json manually."
    fi
fi

echo ""
echo "Starting Docker containers..."
echo ""

# Start the containers (without using a .env file initially)
$DOCKER_COMPOSE up -d db mailpit serveo serveo-frontend frontend

echo ""
echo "Configuring Serveo URLs..."
echo ""

# Run the serveo-init script to detect and configure Serveo URLs
bash serveo-init.sh

# Extract and store the Serveo URLs for display
if [ -z "${SERVEO_BACKEND_URL}" ] || [ -z "${SERVEO_FRONTEND_URL}" ]; then
    # Try to extract URLs from docker logs directly as a backup
    echo "Trying to extract Serveo URLs directly from container logs..."
    backend_url=$(docker logs cinemagia-serveo-tunneling-backend 2>&1 | grep -o 'https://[a-zA-Z0-9][a-zA-Z0-9-]*\.serveo\.net' | head -1)
    frontend_url=$(docker logs cinemagia-serveo-tunneling-frontend 2>&1 | grep -o 'https://[a-zA-Z0-9][a-zA-Z0-9-]*\.serveo\.net' | head -1)
    
    if [ -z "$backend_url" ] || [ -z "$frontend_url" ]; then
        echo "⚠️  WARNING: Could not detect Serveo URLs. Payment integration may not work correctly."
        backend_url="<detection failed>"
        frontend_url="<detection failed>"
    else
        # Export the values we found as a fallback
        export SERVEO_BACKEND_URL="$backend_url"
        export SERVEO_FRONTEND_URL="$frontend_url"
        
        # Restart the WebAPI with these values
        echo "Restarting WebAPI with detected Serveo URLs..."
        SERVEO_BACKEND_URL="$backend_url" SERVEO_FRONTEND_URL="$frontend_url" $DOCKER_COMPOSE up -d --remove-orphans webapi
    fi
else
    # Use the environment variables that were already set
    backend_url="${SERVEO_BACKEND_URL}"
    frontend_url="${SERVEO_FRONTEND_URL}"
fi

echo ""
echo "================================================================================"
echo "Setup complete! Your Cinemagia application with Mollie payment is ready."
echo "================================================================================"
echo ""
echo "Web Frontend: http://localhost:5002"
echo "Web API: http://localhost:5001"
echo "Swagger UI: http://localhost:5001/swagger"
echo "Email interface: http://localhost:8025"
echo ""
echo "Serveo Tunnel URLs (for payment webhooks):"
echo "  Backend URL: $backend_url"
echo "  Frontend URL: $frontend_url"
echo ""
echo "To view logs:"
echo "  Frontend: docker logs -f cinemagia-frontend"
echo "  Backend: docker logs -f cinemagia-webapi"
echo ""
echo "To stop the application:"
echo "  $DOCKER_COMPOSE down"

# For Windows/WSL users, add an extra note about potential issues
if [ "$IS_WSL" -eq 1 ]; then
    echo ""
    echo "Note for Windows/WSL users:"
    echo "  If you encounter any script issues, run 'make fix-scripts' first"
    echo "  If you have Docker connection issues, ensure Docker Desktop is running"
    echo "  For networking issues, try restarting Docker Desktop"
fi 