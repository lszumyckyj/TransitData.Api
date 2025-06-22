using TransitData.Api.Models.DTOs;
using TransitData.Api.Services.Interfaces;
using TransitData.Api.Repositories.Interfaces;

namespace TransitData.Api.Services
{
    public class TransitRealtimeDataCollectorService(
        IGtfsFeedService gtfsFeedService,
        IServiceProvider serviceProvider,
        ILogger<TransitRealtimeDataCollectorService> logger) : BackgroundService
    {
        private readonly IGtfsFeedService GtfsFeedService = gtfsFeedService;
        private readonly IServiceProvider ServiceProvider = serviceProvider;
        private readonly ILogger<TransitRealtimeDataCollectorService> Logger = logger;
        private readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("MTA Data Collector Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectAndStoreRealtimeData();
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in MTA data collection cycle");
                    // Continue running even if one cycle fails
                    await Task.Delay(Interval, stoppingToken);
                }
            }

            Logger.LogInformation("MTA Data Collector Service stopped");
        }

        private async Task CollectAndStoreRealtimeData()
        {
            DateTime startTime = DateTime.UtcNow;
            Logger.LogInformation("Starting MTA data collection at {Time}", startTime);

            try
            {
                AllRealtimeDataResponse data = await GtfsFeedService.GetAllTransitRealtimeDataAsync();

                using (var scope = ServiceProvider.CreateAsyncScope())
                {
                    var transitRealtimeDataRepository = scope.ServiceProvider.GetService<ITransitRealtimeDataRepository>()
                        ?? throw new InvalidOperationException("ITransitRealtimeDataRepository not registered.");
                    await transitRealtimeDataRepository.StoreAllTransitRealtimeDataAsync(data);
                    await transitRealtimeDataRepository.StoreTrainsByStationRealtimeAsync(data.Trains);
                    await transitRealtimeDataRepository.StoreStationsAsync(data.Stations);
                }

                TimeSpan duration = DateTime.UtcNow - startTime;
                Logger.LogInformation("Successfully collected and stored {TrainCount} trains and {StationCount} stations in {Duration}ms",
                    data.TotalTrains, data.TotalStations, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect and store MTA data");
                throw;
            }
        }
    }
}