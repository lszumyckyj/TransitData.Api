# TransitData.Api
A .NET 9 Web API that collects and serves real-time MTA (Metropolitan Transportation Authority) subway data from GTFS (General Transit Feed Specification) feeds.

##  Overview
This API provides real-time information about NYC subway trains and stations by consuming MTA's GTFS real-time feeds. It processes protobuf data to deliver structured JSON responses with train locations, station information, and service updates.

## Architecture
- **.NET 9** Web API with Entity Framework Core
- **PostgreSQL**
- **Redis** for caching and real-time data storage
- **GTFS** protobuf feeds from MTA
- **Background Services** for continuous data collection to Redis

## Prerequisites
- .NET 9 SDK
- PostgreSQL database
- Redis server

## Configuration
Update `appsettings.json` with your database and Redis connection strings:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Host=your-host;Database=your-db;Username=your-user;Password=your-pass"
  },
  "Redis": {
    "ConnectionString": "your-redis-host:port"
  }
}
```

## Installation
1. **Clone the repository**
   ```bash
   git clone https://github.com/lszumyckyj/TransitData.Api.git
   cd TransitData.Api
   ```

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Set up the database**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

## Running in Development
1. Start PostgreSQL and Redis.
2. Create `appsettings.Development.json` and update connection strings.
3. Run the application: `dotnet run`

## Related Links
- [MTA GTFS Feeds](https://api.mta.info/)
- [GTFS Realtime Specification](https://github.com/google/transit/tree/master/gtfs-realtime)
- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)
