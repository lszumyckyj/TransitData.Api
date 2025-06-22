using Moq;
using StackExchange.Redis;
using TransitData.Api.Models.DTOs;
using TransitData.Api.Repositories;

namespace TransitData.Api.Tests.Repositories
{
    public class TransitRealtimeDataRepositoryTests
    {
        [Fact]
        public async Task GetAllTransitDataAsync_ReturnsData_WhenDataExists()
        {
            // Arrange
            var mockDb = new Mock<IDatabase>();
            var expected = new AllRealtimeDataResponse
            {
                LastUpdated = DateTime.UtcNow,
                Trains = new List<Train>(),
                Stations = new List<Station>(),
                TotalTrains = 0,
                TotalStations = 0
            };
            var json = System.Text.Json.JsonSerializer.Serialize(expected);
            mockDb.Setup(db => db.StringGetAsync("mta:realtime:all_data", It.IsAny<CommandFlags>()))
                  .ReturnsAsync(json);

            var mockConn = new Mock<IConnectionMultiplexer>();
            mockConn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);

            var repo = new TransitRealtimeDataRepository(mockConn.Object);

            // Act
            var result = await repo.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expected.TotalTrains, result!.TotalTrains);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_ReturnsNull_WhenNoData()
        {
            // Arrange
            var mockDb = new Mock<IDatabase>();
            mockDb.Setup(db => db.StringGetAsync("mta:realtime:all_data", It.IsAny<CommandFlags>()))
                  .ReturnsAsync(RedisValue.Null);

            var mockConn = new Mock<IConnectionMultiplexer>();
            mockConn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);

            var repo = new TransitRealtimeDataRepository(mockConn.Object);

            // Act
            var result = await repo.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task StoreAllTransitDataAsync_StoresDataWithCorrectKeyAndExpiry()
        {
            // Arrange
            var mockDb = new Mock<IDatabase>();
            var mockConn = new Mock<IConnectionMultiplexer>();
            mockConn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
            var repo = new TransitRealtimeDataRepository(mockConn.Object);
            var data = new AllRealtimeDataResponse
            {
                LastUpdated = DateTime.UtcNow,
                Trains = new List<Train> { new Train { RouteId = "A", StationId = "123", ArrivalTime = DateTime.UtcNow.AddMinutes(5) } },
                Stations = new List<Station> { new Station { StationId = "123", StationName = "Test Station", Direction = "N" } },
                TotalTrains = 1,
                TotalStations = 1
            };
            string expectedJson = System.Text.Json.JsonSerializer.Serialize(data);

            // Act
            await repo.StoreAllTransitRealtimeDataAsync(data);

            // Assert
            mockDb.Verify(db => db.StringSetAsync(
                "mta:realtime:all_data",
                expectedJson,
                It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalMinutes == 2),
                false, When.Always, CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task StoreTrainsByStationAsync_StoresGroupedTrainsWithCorrectKeysAndExpiry()
        {
            // Arrange
            var mockDb = new Mock<IDatabase>();
            var mockConn = new Mock<IConnectionMultiplexer>();
            mockConn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
            var repo = new TransitRealtimeDataRepository(mockConn.Object);
            var now = DateTime.UtcNow;
            var trains = new List<Train>
            {
                new Train { RouteId = "A", StationId = "S1", ArrivalTime = now.AddMinutes(1) },
                new Train { RouteId = "B", StationId = "S1", ArrivalTime = now.AddMinutes(2) },
                new Train { RouteId = "C", StationId = "S2", ArrivalTime = now.AddMinutes(3) },
                new Train { RouteId = "D", StationId = "S3", ArrivalTime = null } // Should be ignored
            };
            // Act
            await repo.StoreTrainsByStationRealtimeAsync(trains);

            // Assert
            mockDb.Verify(db => db.StringSetAsync(
                "mta:realtime:station:S1",
                It.Is<RedisValue>(rv => rv.ToString().Contains("A") && rv.ToString().Contains("B")),
                It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalMinutes == 2),
                false, When.Always, CommandFlags.None), Times.Once);
            mockDb.Verify(db => db.StringSetAsync(
                "mta:realtime:station:S2",
                It.Is<RedisValue>(rv => rv.ToString().Contains("C")),
                It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalMinutes == 2),
                false, When.Always, CommandFlags.None), Times.Once);
            mockDb.Verify(db => db.StringSetAsync(
                It.Is<RedisKey>(rk => rk.ToString().StartsWith("mta:realtime:station:")),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                false, When.Always, CommandFlags.None), Times.Exactly(2));
        }

        [Fact]
        public async Task StoreStationsAsync_StoresStationsWithCorrectKeyAndExpiry()
        {
            // Arrange
            var mockDb = new Mock<IDatabase>();
            var mockConn = new Mock<IConnectionMultiplexer>();
            mockConn.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
            var repo = new TransitRealtimeDataRepository(mockConn.Object);
            var stations = new List<Station>
            {
                new Station { StationId = "S1", StationName = "Station 1", Direction = "N" },
                new Station { StationId = "S2", StationName = "Station 2", Direction = "S" }
            };
            string expectedJson = System.Text.Json.JsonSerializer.Serialize(stations);

            // Act
            await repo.StoreStationsAsync(stations);

            // Assert
            mockDb.Verify(db => db.StringSetAsync(
                "mta:stations",
                expectedJson,
                It.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalHours == 1),
                false, When.Always, CommandFlags.None), Times.Once);
        }
    }
}
