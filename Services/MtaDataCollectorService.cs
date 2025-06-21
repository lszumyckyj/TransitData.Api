using System.Text.Json;
using StackExchange.Redis;
using TransitData.Api.Models.DTOs;
using TransitData.Api.Services.Interfaces;

namespace TransitData.Api.Services
{
    public class MtaDataCollectorService : BackgroundService
    {
        private readonly IGtfsFeedService MtaService;
        private readonly IDatabase Redis;
        private readonly ILogger<MtaDataCollectorService> Logger;
        private readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        public MtaDataCollectorService(
            IGtfsFeedService mtaService,
            IConnectionMultiplexer redis,
            ILogger<MtaDataCollectorService> logger)
        {
            MtaService = mtaService;
            Redis = redis.GetDatabase();
            Logger = logger;
        }

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
            var startTime = DateTime.UtcNow;
            Logger.LogInformation("Starting MTA data collection at {Time}", startTime);

            try
            {
                var data = await MtaService.GetAllTrainDataAsync();

                var dataJson = JsonSerializer.Serialize(data);
                await Redis.StringSetAsync("mta:all_data", dataJson, TimeSpan.FromMinutes(2));

                // Store individual train arrivals by station for faster lookups
                await StoreTrainsByStation(data.Trains);

                // Store stations list
                var stationsJson = JsonSerializer.Serialize(data.Stations);
                await Redis.StringSetAsync("mta:stations", stationsJson, TimeSpan.FromHours(1));

                var duration = DateTime.UtcNow - startTime;
                Logger.LogInformation("Successfully collected and stored {TrainCount} trains and {StationCount} stations in {Duration}ms",
                    data.TotalTrains, data.TotalStations, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to collect and store MTA data");
                throw;
            }
        }

        private async Task StoreTrainsByStation(List<TrainInfo> trains)
        {
            // Group trains by station for faster station-specific lookups
            var trainsByStation = trains
                .Where(t => t.ArrivalTime.HasValue)
                .GroupBy(t => t.StationId)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.ArrivalTime).ToList());

            var tasks = trainsByStation.Select(async kvp =>
            {
                var key = $"mta:station:{kvp.Key}";
                var json = JsonSerializer.Serialize(kvp.Value);
                await Redis.StringSetAsync(key, json, TimeSpan.FromMinutes(2));
            });

            await Task.WhenAll(tasks);
        }
    }
}