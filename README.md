# RailcarTrips

A Blazor WebAssembly application for tracking and managing railcar trips across Canadian cities.

## Overview

RailcarTrips processes equipment event data to track railcar journeys from origin to destination. The system parses CSV files containing equipment events, calculates trip durations, and provides a web interface for viewing trip history.

## Tech Stack

- **Frontend**: Blazor WebAssembly (.NET 8)
- **Backend**: ASP.NET Core Web API (.NET 8)
- **Database**: MySQL with Entity Framework Core
- **Architecture**: Client-Server with shared DTOs

## Project Structure

```
RailcarTrips/
├── RailcarTrips.Client/          # Blazor WebAssembly frontend
│   ├── Pages/                    # Razor pages
│   ├── Services/                 # API client services
│   └── Layout/                   # UI layout components
├── RailcarTrips.Server/          # ASP.NET Core backend
│   ├── Controllers/              # API endpoints
│   ├── Data/                     # DbContext, entities, seeder
│   ├── Services/                 # Business logic
│   └── Migrations/               # EF Core migrations
├── RailcarTrips.Shared/          # Shared DTOs
├── RailcarTrips.Client.Tests/    # Client unit tests
└── RailcarTrips.Server.Tests/    # Server unit tests
```

## Features

- Upload and process equipment event CSV files
- Track railcar trips with origin/destination cities
- Calculate trip durations with timezone support
- View trip history and event details
- Support for Canadian cities with timezone handling

## Event Codes

| Code | Description |
|------|-------------|
| W    | Released (starts a trip) |
| Z    | Placed (ends a trip) |
| A    | Arrived |
| D    | Departed |

## Getting Started

### Prerequisites

- .NET 8 SDK
- MySQL Server

### Database Setup

1. Update the connection string in `RailcarTrips.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=railcar_trips;User=root;Password=your_password;"
  }
}
```

2. The database will be automatically migrated and seeded on first run.

### Running the Application

```bash
# Run the server (also hosts the client)
dotnet run --project RailcarTrips.Server

# Or run client standalone
dotnet run --project RailcarTrips.Client
```

The application will be available at:
- Server: http://localhost:5075
- Client (standalone): http://localhost:5072

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET    | `/api/trips` | Get all trips |
| GET    | `/api/trips/{id}` | Get trip details with events |
| POST   | `/api/trips/upload` | Upload and process CSV file |

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test RailcarTrips.Server.Tests
dotnet test RailcarTrips.Client.Tests
```

## License

MIT
