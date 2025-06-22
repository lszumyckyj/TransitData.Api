using Microsoft.AspNetCore.Mvc;
using Moq;
using TransitData.Api.Controllers;
using TransitData.Api.Models.DTOs;
using TransitData.Api.Repositories.Interfaces;

namespace TransitData.Api.Tests.Controllers
{
    public class TransitRealtimeDataControllerTests
    {
        [Fact]
        public async Task GetAllTransitDataAsync_ReturnsOk_WhenDataExists()
        {
            // Arrange
            var mockRepo = new Mock<ITransitRealtimeDataRepository>();
            var expectedData = new AllRealtimeDataResponse
            {
                LastUpdated = DateTime.UtcNow,
                Trains = new List<Train> { new Train { RouteId = "6", TripId = "trip1" } },
                Stations = new List<Station> { new Station { StationId = "123", StationName = "Test Station" } },
                TotalTrains = 1,
                TotalStations = 1
            };
            mockRepo.Setup(r => r.GetAllTransitRealtimeDataAsync()).ReturnsAsync(expectedData);
            var controller = new TransitRealtimeDataController(mockRepo.Object);

            // Act
            var result = await controller.GetAllTransitRealtimeDataAsync();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<AllRealtimeDataResponse>(okResult.Value);
            Assert.Equal(expectedData.TotalTrains, returnValue.TotalTrains);
            Assert.Equal(expectedData.TotalStations, returnValue.TotalStations);
        }

        [Fact]
        public async Task GetAllTransitDataAsync_ReturnsNotFound_WhenDataIsNull()
        {
            // Arrange
            var mockRepo = new Mock<ITransitRealtimeDataRepository>();
            mockRepo.Setup(r => r.GetAllTransitRealtimeDataAsync()).ReturnsAsync((AllRealtimeDataResponse?)null);
            var controller = new TransitRealtimeDataController(mockRepo.Object);

            // Act
            var result = await controller.GetAllTransitRealtimeDataAsync();

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
