using TransitData.Api.Models.DTOs;
using TransitData.Api.Services.Interfaces;
using TransitData.Api.Repositories.Interfaces;
using Google.Protobuf.WellKnownTypes;

namespace TransitData.Api.Services
{
    public class MtaDataCollectorService(
        IGtfsFeedService mtaService,
        IServiceProvider serviceProvider,
        ILogger<MtaDataCollectorService> logger) : BackgroundService
    {
        private readonly IGtfsFeedService MtaService = mtaService;
        private readonly IServiceProvider ServiceProvider = serviceProvider;
        private readonly ILogger<MtaDataCollectorService> Logger = logger;
        private readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("MTA Data Collector Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CollectAndStoreData();
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

        private async Task CollectAndStoreData()
        {
            DateTime startTime = DateTime.UtcNow;
            Logger.LogInformation("Starting MTA data collection at {Time}", startTime);

            try
            {
                MtaAllDataResponse data = await MtaService.GetAllTransitDataAsync();

                using (var scope = ServiceProvider.CreateAsyncScope())
                {
                    var transitDataRepository = scope.ServiceProvider.GetService<ITransitDataRepository>()
                        ?? throw new InvalidOperationException("ITransitDataRepository not registered.");
                    await transitDataRepository.StoreAllTransitDataAsync(data);
                    await transitDataRepository.StoreTrainsByStationAsync(data.Trains);
                    await transitDataRepository.StoreStationsAsync(data.Stations);
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