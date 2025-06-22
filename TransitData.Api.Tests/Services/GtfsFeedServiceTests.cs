using System.Net;
using Google.Protobuf;
using Moq;
using Moq.Protected;
using TransitData.Api.Services;
using TransitRealtime;
using Microsoft.Extensions.Logging;

namespace TransitData.Api.Tests.Services
{
    public class GtfsFeedServiceTests
    {
        public GtfsFeedServiceTests()
        {
            // Clear and repopulate the feed list for all tests in this class
            TransitData.Api.Data.Static.GtfsRealtimeFeedUrls.All.Clear();
            TransitData.Api.Data.Static.GtfsRealtimeFeedUrls.All.Add("TestFeed", "http://test");
        }

        private static FeedMessage CreateFeedMessageWithTripUpdate(
            string stopId = "123N",
            long? arrivalTime = null,
            long? departureTime = null,
            string routeId = "6",
            string tripId = "trip1")
        {
            var tripDescriptor = new TripDescriptor { RouteId = routeId, TripId = tripId };
            var stopTimeUpdate = new TripUpdate.Types.StopTimeUpdate
            {
                StopId = stopId
            };
            if (arrivalTime.HasValue)
                stopTimeUpdate.Arrival = new TripUpdate.Types.StopTimeEvent { Time = arrivalTime.Value };
            if (departureTime.HasValue)
                stopTimeUpdate.Departure = new TripUpdate.Types.StopTimeEvent { Time = departureTime.Value };
            var tripUpdate = new TripUpdate { Trip = tripDescriptor };
            tripUpdate.StopTimeUpdate.Add(stopTimeUpdate);
            var entity = new FeedEntity { Id = "entity1", TripUpdate = tripUpdate };
            var feed = new FeedMessage
            {
                Header = new FeedHeader { GtfsRealtimeVersion = "2.0" }
            };
            feed.Entity.Add(entity);
            return feed;
        }

        private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, byte[]? content = null, string? url = null)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => url == null || req.RequestUri!.ToString().Contains(url)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = content != null ? new ByteArrayContent(content) : new StringContent("error")
                })
                .Verifiable();
            return new HttpClient(handlerMock.Object);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_ReturnsTrainsAndStations_OnSuccess()
        {
            // Arrange
            var feed = CreateFeedMessageWithTripUpdate(arrivalTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var bytes = feed.ToByteArray();
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, bytes);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.Trains);
            Assert.Single(result.Stations);
            Assert.Equal(result.TotalTrains, result.Trains.Count);
            Assert.Equal(result.TotalStations, result.Stations.Count);
            Assert.Equal("6", result.Trains[0].RouteId);
            Assert.Equal("123N", result.Trains[0].StationId);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_SkipsFeed_OnHttpError()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Trains);
            Assert.Empty(result.Stations);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_SkipsFeed_OnException()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("fail"));
            var httpClient = new HttpClient(handlerMock.Object);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Trains);
            Assert.Empty(result.Stations);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_ParsesMultipleEntities()
        {
            // Arrange
            var feed = new FeedMessage { Header = new FeedHeader { GtfsRealtimeVersion = "2.0" } };
            for (int i = 0; i < 3; i++)
            {
                var tripDescriptor = new TripDescriptor { RouteId = "A", TripId = $"trip{i}" };
                var stopTimeUpdate = new TripUpdate.Types.StopTimeUpdate { StopId = $"S{i}N", Arrival = new TripUpdate.Types.StopTimeEvent { Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds() } };
                var tripUpdate = new TripUpdate { Trip = tripDescriptor };
                tripUpdate.StopTimeUpdate.Add(stopTimeUpdate);
                var entity = new FeedEntity { Id = $"entity{i}", TripUpdate = tripUpdate };
                feed.Entity.Add(entity);
            }
            var bytes = feed.ToByteArray();
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, bytes);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.Equal(3, result.Trains.Count);
            Assert.Equal(3, result.Stations.Count);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_DirectionFallbacksToStopId()
        {
            // Arrange
            var feed = CreateFeedMessageWithTripUpdate(stopId: "123S", arrivalTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var bytes = feed.ToByteArray();
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, bytes);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.Equal("South", result.Trains[0].Direction);
            Assert.Equal("South", result.Stations[0].Direction);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_StationNameLookup_Works()
        {
            // Arrange
            var feed = CreateFeedMessageWithTripUpdate(stopId: "127N", arrivalTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var bytes = feed.ToByteArray();
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, bytes);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.Contains("Times Sq-42 St", result.Stations[0].StationName);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_DoesNotAddTrain_IfNoArrivalOrDeparture()
        {
            // Arrange
            var tripDescriptor = new TripDescriptor { RouteId = "A", TripId = "trip1" };
            var stopTimeUpdate = new TripUpdate.Types.StopTimeUpdate { StopId = "S1N" }; // No arrival/departure
            var tripUpdate = new TripUpdate { Trip = tripDescriptor };
            tripUpdate.StopTimeUpdate.Add(stopTimeUpdate);
            var entity = new FeedEntity { Id = "entity1", TripUpdate = tripUpdate };
            var feed = new FeedMessage { Header = new FeedHeader { GtfsRealtimeVersion = "2.0" } };
            feed.Entity.Add(entity);
            var bytes = feed.ToByteArray();
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, bytes);
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.Empty(result.Trains);
            Assert.Single(result.Stations); // Station is still added
        }

        [Fact]
        public async Task GetAllTransitDataAsync_HandlesInvalidProtobuf()
        {
            // Arrange
            var httpClient = CreateMockHttpClient(HttpStatusCode.OK, new byte[] { 0x00, 0x01, 0x02 }); // Invalid protobuf
            var logger = new Mock<ILogger<GtfsFeedService>>();
            var service = new GtfsFeedService(httpClient, logger.Object);

            // Act
            var result = await service.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Trains);
            Assert.Empty(result.Stations);
        }
    }
}
