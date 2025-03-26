# Movie Theater Ticket Booking API

A .NET Web API for booking movie theater tickets, supporting both individual and group bookings with automatic seat assignment.

## Getting Started

There are two ways to set up this project:
1. **Docker Setup (Recommended)** - Easiest method, no manual installations required
2. **Manual Setup** - For development without Docker, requires manual installation of dependencies

## Docker Setup (Recommended)

This application is fully containerized with Docker for easier setup and deployment.

### Prerequisites
- Docker and Docker Compose installed on your system
- No need for local .NET SDK or MySQL installation

### 1. Running with Docker Compose

1. Clone and navigate to the repository:
   ```bash
   git clone <repository-url>
   cd <repository-directory>
   ```

2. Start the containers:
   ```bash
   docker compose up -d
   ```
   This will:
   - Build all required containers (.NET, MySQL, frontend, mailpit)
   - Run database migrations automatically
   - Seed the database with sample data
   - Start all services

3. To rebuild containers after making changes:
   ```bash
   docker compose up -d --build
   ```

4. To stop the containers:
   ```bash
   docker compose down
   ```

5. To remove all containers and volumes (when you want to reset the database):
   ```bash
   docker compose down -v
   ```

### 2. Accessing Services

After starting the containers, you can access:

- **Frontend Application**: http://localhost:5002
- **API Swagger Documentation**: http://localhost:5001/swagger
- **API Health Check**: http://localhost:5001/health
- **Mailpit (Email Testing)**: http://localhost:8025

### 3. Environment Configuration

The Docker setup uses the following configuration:
- MySQL database with authentication (password: "password")
- Environment variables configured in docker-compose.yml
- Shared network between all services
- Email sent via Mailpit (view in the Mailpit UI)

### 4. Troubleshooting Docker Setup

If you encounter issues:

1. Check container status:
   ```bash
   docker compose ps
   ```

2. View container logs:
   ```bash
   docker compose logs webapi
   docker compose logs frontend
   docker compose logs db
   ```

3. Access the database directly:
   ```bash
   docker exec -it cinemagia-mysql mysql -uroot -ppassword
   ```

4. Restart individual services:
   ```bash
   docker restart cinemagia-webapi
   docker restart cinemagia-frontend
   ```

## Manual Setup (Alternative)

If you prefer not to use Docker, you can set up the project manually. This is more involved and requires installing dependencies directly on your system.

### Prerequisites
- .NET 9.0 SDK
- MySQL Server
- IDE (Visual Studio Code recommended)

### 1. Initial Setup
1. Clone and navigate:
   ```bash
   git clone <repository-url>
   cd WebApi
   ```

2. Install dependencies:
   ```bash
   dotnet restore
   ```

### 2. Database Setup
1. Install MySQL if not installed:
   ```bash
   # macOS
   brew install mysql
   brew services start mysql

   # Ubuntu
   sudo apt install mysql-server
   sudo service mysql start
   ```

2. Configure connection string in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=movietheater;User=root;Password=yourpassword;"
     }
   }
   ```

3. Run migrations:
   ```bash
   dotnet ef database update
   ```

### 3. Run the Application
1. Start the API:
   ```bash
   dotnet run
   ```
   The API will be available at http://localhost:5213

   On startup, the application automatically seeds:
    - 6 movie halls with different layouts
    - All seats for each hall
    - Sample movies and presentations

2. Run tests to verify setup:
   ```bash
   dotnet test
   ```

## Using the API

### Individual Ticket Booking Flow
1. Start individual booking to get seat assignment:
   ```http
   POST /api/Tickets/orders/start
   {
     "presentationId": 1,
     "numberOfSeats": 1
   }
   ```
   Response: Returns orderToken and assigned seat

2. Confirm booking:
   ```http
   POST /api/Tickets/orders/{orderToken}/confirm
   {
     "customerName": "John Doe",
     "customerEmail": "john@example.com"
   }
   ```

### Group Booking Flow
1. Start group booking to get seating options:
   ```http
   POST /api/Tickets/orders/start-group
   {
     "presentationId": 1,
     "numberOfSeats": 4
   }
   ```
   Response:
    - If consecutive seats available: Returns seat arrangement
    - If split seating needed: Returns multiple seating options

2. Select seating option:
   ```http
   POST /api/Tickets/orders/start-group/{option}
   {
     "orderToken": "guid-from-previous-response"
   }
   ```
   Where option is either "consecutive" or "split"

3. Confirm booking:
   ```http
   POST /api/Tickets/orders/{orderToken}/confirm
   {
     "customerName": "John Doe",
     "customerEmail": "john@example.com"
   }
   ```

## Key Features
- Individual and group ticket booking
- Automatic seat assignment
- Consecutive seating for groups when available
- Split seating options for larger groups
- Seat locking to prevent double bookings
- Automatic cleanup of expired orders

## API Reference

### Individual Bookings
- `POST /api/Tickets/orders/start` - Start individual booking
- `POST /api/Tickets/orders/{orderToken}/confirm` - Confirm individual booking

### Group Bookings
- `POST /api/Tickets/orders/start-group` - Initialize group booking
- `POST /api/Tickets/orders/start-group/{option}` - Select seating arrangement
- `POST /api/Tickets/orders/{orderToken}/confirm` - Confirm group booking

### Order Management
- `POST /api/Tickets/orders/{orderToken}/seats` - Modify seats in order
- `DELETE /api/Tickets/orders/{orderToken}/seats/{seatId}` - Remove seat from order

## Important Notes
- Orders expire after 10 minutes if not confirmed
- Seats are automatically locked during booking process
- Group bookings prioritize consecutive seats
- Split seating is offered when consecutive seats aren't available
- Maximum group size: 20 people

## Testing Coverage
- Individual booking flows
- Group booking scenarios
- Seat locking mechanism
- Order expiration
- Edge cases (full halls, split seating)

## Error Handling
The API uses standard HTTP status codes:
- 200: Success
- 400: Invalid request
- 404: Resource not found
- 500: Server error

Error responses include descriptive messages for troubleshooting.

## Seat Selection Algorithm

The system uses a sophisticated algorithm to select the best available seats for customers. The algorithm considers several factors:

### Single Seat Selection
When selecting a single seat, the algorithm:
1. Prioritizes seats about 2/3 back from the screen (optimal viewing distance)
2. Prefers center seats over side seats
3. Applies penalties for:
    - Front row seats (especially corners)
    - Side seats (increasing penalty towards edges)
    - Extreme viewing angles
4. Gives bonus points for seats in the ideal viewing zone

### Group Seat Selection
For multiple seats, the algorithm:
1. First attempts to find consecutive seats in the same row
2. Prioritizes rows in this order:
    - 2/3 back from screen (optimal)
    - Middle rows
    - Back rows
    - Front rows (least preferred)
3. If consecutive seats aren't available, finds best available split options:
    - Minimizes the number of splits
    - Keeps groups as close together as possible
    - Maintains same seating quality for all group members

### Scoring Factors
Each seat is scored based on:
- Distance from screen center (Euclidean distance)
- Row position relative to optimal viewing distance
- Side distance from center
- Viewing angle penalties
- Front row penalties
- Corner seat penalties

Lower scores are better, with penalties increasing the score and bonuses decreasing it.