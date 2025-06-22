using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TransitData.Api.Models.DTOs;
using TransitData.Api.Repositories.Interfaces;
using TransitData.Api.Services;
using TransitData.Api.Services.Interfaces;

namespace TransitData.Api.Tests.Services
{
    public class TransitRealtimeDataCollectorServiceTests
    {
        private static AllRealtimeDataResponse CreateSampleData()
        {
            return new AllRealtimeDataResponse
            {
                LastUpdated = DateTime.UtcNow,
                Trains = new List<Train> { new Train { RouteId = "A", TripId = "T1", StationId = "S1" } },
                Stations = new List<Station> { new Station { StationId = "S1", StationName = "Station 1", Direction = "North" } },
                TotalTrains = 1,
                TotalStations = 1
            };
        }

        private static Mock<IServiceScope> CreateScopeWithRepo(Mock<ITransitRealtimeDataRepository> repoMock)
        {
            var scopeMock = new Mock<IServiceScope>();
            var providerMock = new Mock<IServiceProvider>();
            providerMock.Setup(x => x.GetService(typeof(ITransitRealtimeDataRepository))).Returns(repoMock.Object);
            scopeMock.Setup(x => x.ServiceProvider).Returns(providerMock.Object);
            return scopeMock;
        }

        private static Mock<IServiceProvider> CreateServiceProviderWithScope(Mock<IServiceScope> scopeMock)
        {
            var spMock = new Mock<IServiceProvider>();
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
            spMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);
            return spMock;
        }

        [Fact]
        public async Task CollectAndStoreRealtimeData_CallsAllRepositoryMethods()
        {
            // Arrange
            var data = CreateSampleData();
            var gtfsMock = new Mock<IGtfsFeedService>();
            gtfsMock.Setup(x => x.GetAllTransitRealtimeDataAsync()).ReturnsAsync(data);

            var repoMock = new Mock<ITransitRealtimeDataRepository>();
            var scopeMock = CreateScopeWithRepo(repoMock);
            var spMock = CreateServiceProviderWithScope(scopeMock);
            var loggerMock = new Mock<ILogger<TransitRealtimeDataCollectorService>>();

            var service = new TransitRealtimeDataCollectorService(gtfsMock.Object, spMock.Object, loggerMock.Object);
            var method = service.GetType().GetMethod("CollectAndStoreRealtimeData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var task = (Task)method!.Invoke(service, null)!;
            await task;

            // Assert
            repoMock.Verify(x => x.StoreAllTransitRealtimeDataAsync(data), Times.Once);
            repoMock.Verify(x => x.StoreTrainsByStationRealtimeAsync(data.Trains), Times.Once);
            repoMock.Verify(x => x.StoreStationsAsync(data.Stations), Times.Once);
        }

        [Fact]
        public async Task CollectAndStoreRealtimeData_LogsAndThrows_OnRepositoryException()
        {
            // Arrange
            var data = CreateSampleData();
            var gtfsMock = new Mock<IGtfsFeedService>();
            gtfsMock.Setup(x => x.GetAllTransitRealtimeDataAsync()).ReturnsAsync(data);

            var repoMock = new Mock<ITransitRealtimeDataRepository>();
            repoMock.Setup(x => x.StoreAllTransitRealtimeDataAsync(It.IsAny<AllRealtimeDataResponse>())).ThrowsAsync(new Exception("repo fail"));
            var scopeMock = CreateScopeWithRepo(repoMock);
            var spMock = CreateServiceProviderWithScope(scopeMock);
            var loggerMock = new Mock<ILogger<TransitRealtimeDataCollectorService>>();

            var service = new TransitRealtimeDataCollectorService(gtfsMock.Object, spMock.Object, loggerMock.Object);
            var method = service.GetType().GetMethod("CollectAndStoreRealtimeData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => (Task)method!.Invoke(service, null)!);
            Assert.Contains("repo fail", ex.Message);
            loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to collect and store MTA data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!), Times.Once);
        }

        [Fact]
        public async Task CollectAndStoreRealtimeData_LogsAndThrows_OnGtfsException()
        {
            // Arrange
            var gtfsMock = new Mock<IGtfsFeedService>();
            gtfsMock.Setup(x => x.GetAllTransitRealtimeDataAsync()).ThrowsAsync(new Exception("gtfs fail"));

            var repoMock = new Mock<ITransitRealtimeDataRepository>();
            var scopeMock = CreateScopeWithRepo(repoMock);
            var spMock = CreateServiceProviderWithScope(scopeMock);
            var loggerMock = new Mock<ILogger<TransitRealtimeDataCollectorService>>();

            var service = new TransitRealtimeDataCollectorService(gtfsMock.Object, spMock.Object, loggerMock.Object);
            var method = service.GetType().GetMethod("CollectAndStoreRealtimeData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => (Task)method!.Invoke(service, null)!);
            Assert.Contains("gtfs fail", ex.Message);
            loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to collect and store MTA data")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_LogsStartAndStop()
        {
            // Arrange
            var gtfsMock = new Mock<IGtfsFeedService>();
            gtfsMock.Setup(x => x.GetAllTransitRealtimeDataAsync()).ReturnsAsync(CreateSampleData());

            var repoMock = new Mock<ITransitRealtimeDataRepository>();
            var scopeMock = CreateScopeWithRepo(repoMock);
            var spMock = CreateServiceProviderWithScope(scopeMock);
            var loggerMock = new Mock<ILogger<TransitRealtimeDataCollectorService>>();

            var service = new TransitRealtimeDataCollectorService(gtfsMock.Object, spMock.Object, loggerMock.Object);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(100); // Cancel quickly

            // Act
            await service.StartAsync(cts.Token);
            await service.StopAsync(CancellationToken.None);

            // Assert
            loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MTA Data Collector Service started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!), Times.AtLeastOnce);
            loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("MTA Data Collector Service stopped")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_ContinuesAfterHandledException()
        {
            // Arrange
            var gtfsMock = new Mock<IGtfsFeedService>();
            gtfsMock.SetupSequence(x => x.GetAllTransitRealtimeDataAsync())
                .ThrowsAsync(new Exception("fail once"))
                .ReturnsAsync(CreateSampleData());

            var repoMock = new Mock<ITransitRealtimeDataRepository>();
            var scopeMock = CreateScopeWithRepo(repoMock);
            var spMock = CreateServiceProviderWithScope(scopeMock);
            var loggerMock = new Mock<ILogger<TransitRealtimeDataCollectorService>>();

            var service = new TransitRealtimeDataCollectorService(gtfsMock.Object, spMock.Object, loggerMock.Object);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(200); // Allow two iterations

            // Act
            await service.StartAsync(cts.Token);
            await service.StopAsync(CancellationToken.None);

            // Assert
            loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error in MTA data collection cycle")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!), Times.AtLeastOnce);
        }
    }
}
