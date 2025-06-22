using TransitData.Api.Models.DTOs;
using TransitData.Api.Repositories.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace TransitData.Api.Repositories
{
    public class TransitRealtimeDataRepository(IConnectionMultiplexer redis) : ITransitRealtimeDataRepository
    {
        private readonly IDatabase Redis = redis.GetDatabase();

        public async Task<AllRealtimeDataResponse?> GetAllTransitRealtimeDataAsync()
        {
            RedisValue dataJson = await Redis.StringGetAsync("mta:realtime:all_data");
            if (dataJson.IsNullOrEmpty)
            {
                return null;
            }
            return JsonSerializer.Deserialize<AllRealtimeDataResponse>(dataJson.ToString());
        }

        public async Task StoreAllTransitRealtimeDataAsync(AllRealtimeDataResponse data)
        {
            string dataJson = JsonSerializer.Serialize(data);
            await Redis.StringSetAsync("mta:realtime:all_data", dataJson, TimeSpan.FromMinutes(2));
        }

        public async Task StoreTrainsByStationRealtimeAsync(List<Train> trains)
        {
            Dictionary<string, List<Train>> trainsByStation = trains
                .Where(t => t.ArrivalTime.HasValue)
                .GroupBy(t => t.StationId)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.ArrivalTime).ToList());

            IEnumerable<Task> tasks = trainsByStation.Select(async kvp =>
            {
                var key = $"mta:realtime:station:{kvp.Key}";
                var json = JsonSerializer.Serialize(kvp.Value);
                await Redis.StringSetAsync(key, json, TimeSpan.FromMinutes(2));
            });

            await Task.WhenAll(tasks);
        }

        public async Task StoreStationsAsync(List<Station> stations)
        {
            string stationsJson = JsonSerializer.Serialize(stations);
            await Redis.StringSetAsync("mta:stations", stationsJson, TimeSpan.FromHours(1));
        }
    }
}