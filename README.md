# Movie Theater Ticket Booking API

A .NET Web API for booking movie theater tickets, supporting both individual and group bookings with automatic seat assignment.

## Getting Started

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
