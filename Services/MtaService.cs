using TransitData.Api.Services.Interfaces;
using TransitRealtime;
using TransitData.Api.Models.DTOs;
using Google.Protobuf;
using TransitData.Api.Data.Static;

namespace TransitData.Api.Services
{
    public class MtaService : IMtaService
    {
        private readonly HttpClient HttpClient;
        private readonly ILogger<MtaService> Logger;
        private static readonly MessageParser<FeedMessage> Parser;

        static MtaService()
        {
            var registry = new ExtensionRegistry
            {
                NyctSubwayExtensions.NyctFeedHeader,
                NyctSubwayExtensions.NyctTripDescriptor,
                NyctSubwayExtensions.NyctStopTimeUpdate
            };
            Parser = FeedMessage.Parser.WithExtensionRegistry(registry);
        }

        private readonly Dictionary<string, string> FeedUrls = new()
        {
            ["1234567S"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs",
            ["ACE"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-ace",
            ["BDFM"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-bdfm",
            ["G"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-g",
            ["JZ"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-jz",
            ["L"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-l",
            ["NQRW"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-nqrw",
            ["SI"] = "Dataservice/mtagtfsfeeds/nyct%2Fgtfs-si"
        };

        public MtaService(HttpClient httpClient, ILogger<MtaService> logger)
        {
            HttpClient = httpClient;
            Logger = logger;

            HttpClient.BaseAddress = new Uri("https://api-endpoint.mta.info/");
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "TransitData.Api/1.0");
        }

        public async Task<MtaAllDataResponse> GetAllTrainDataAsync()
        {
            var allTrains = new List<TrainInfo>();
            var allStations = new HashSet<StationInfo>();

            var feedTasks = FeedUrls.Select(async feed =>
            {
                try
                {
                    Logger.LogInformation("Fetching data from {FeedName}", feed.Key);
                    HttpResponseMessage response = await HttpClient.GetAsync(feed.Value);

                    if (response.IsSuccessStatusCode)
                    {
                        byte[] protobufData = await response.Content.ReadAsByteArrayAsync();
                        FeedMessage? feedMessage = Parser.ParseFrom(protobufData);
                        return (FeedName: feed.Key, Feed: feedMessage);
                    }
                    else
                    {
                        Logger.LogWarning("Failed to fetch {FeedName}: {StatusCode}", feed.Key, response.StatusCode);
                        var responseBody = await response.Content.ReadAsStringAsync();
                        Logger.LogWarning("Response body: {ResponseBody}", responseBody);
                        return (FeedName: feed.Key, Feed: (FeedMessage?)null);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error fetching {FeedName}", feed.Key);
                    return (FeedName: feed.Key, Feed: (FeedMessage?)null);
                }
            });

            (string FeedName, FeedMessage? feed)[] feedResults = await Task.WhenAll(feedTasks);

            // Process each feed
            foreach (var (feedName, feed) in feedResults)
            {
                if (feed == null) continue;

                Logger.LogInformation("Processing {EntityCount} entities from {FeedName}", feed.Entity.Count, feedName);

                foreach (var entity in feed.Entity)
                {
                    // Process trip updates (train arrivals)
                    if (entity.TripUpdate != null)
                    {
                        ProcessTripUpdate(entity.TripUpdate, feedName, allTrains, allStations);
                    }
                }
            }

            return new MtaAllDataResponse
            {
                LastUpdated = DateTime.UtcNow,
                Trains = allTrains.OrderBy(t => t.ArrivalTime).ToList(),
                Stations = allStations.OrderBy(s => s.StationName).ToList(),
                TotalTrains = allTrains.Count,
                TotalStations = allStations.Count
            };
        }

        private void ProcessTripUpdate(TripUpdate tripUpdate, string feedName, List<TrainInfo> allTrains, HashSet<StationInfo> allStations)
        {
            var trip = tripUpdate.Trip;

            // NYC-specific data
            string? trainId = null;
            string? direction = null;
            bool? isAssigned = null;

            if (trip.HasExtension(NyctSubwayExtensions.NyctTripDescriptor))
            {
                var nyctTrip = trip.GetExtension(NyctSubwayExtensions.NyctTripDescriptor);

                trainId = nyctTrip.HasTrainId ? nyctTrip.TrainId : null;
                isAssigned = nyctTrip.HasIsAssigned ? nyctTrip.IsAssigned : null;

                if (nyctTrip.HasDirection)
                {
                    direction = nyctTrip.Direction switch
                    {
                        NyctTripDescriptor.Types.Direction.North => "North",
                        NyctTripDescriptor.Types.Direction.South => "South",
                        NyctTripDescriptor.Types.Direction.East => "East",
                        NyctTripDescriptor.Types.Direction.West => "West",
                        _ => "Unknown"
                    };
                }
            }

            foreach (var stopUpdate in tripUpdate.StopTimeUpdate)
            {
                // NYC-specific stop data
                string? scheduledTrack = null;
                string? actualTrack = null;

                if (stopUpdate.HasExtension(NyctSubwayExtensions.NyctStopTimeUpdate))
                {
                    var nyctStop = stopUpdate.GetExtension(NyctSubwayExtensions.NyctStopTimeUpdate);

                    scheduledTrack = nyctStop.HasScheduledTrack ? nyctStop.ScheduledTrack : null;
                    actualTrack = nyctStop.HasActualTrack ? nyctStop.ActualTrack : null;
                }

                // Determine direction. Prefer NYCT extension, fallback to stop ID parsing
                var finalDirection = direction ?? DetermineDirectionFromStopId(stopUpdate.StopId);

                // Only add trains with valid arrival or departure times
                if (stopUpdate.Arrival?.Time != null || stopUpdate.Departure?.Time != null)
                {
                    allTrains.Add(new TrainInfo
                    {
                        RouteId = trip.RouteId ?? string.Empty,
                        TripId = trip.TripId ?? string.Empty,
                        StationId = stopUpdate.StopId ?? string.Empty,
                        ArrivalTime = stopUpdate.Arrival?.Time != null
                            ? DateTimeOffset.FromUnixTimeSeconds(stopUpdate.Arrival.Time).DateTime
                            : null,
                        DepartureTime = stopUpdate.Departure?.Time != null
                            ? DateTimeOffset.FromUnixTimeSeconds(stopUpdate.Departure.Time).DateTime
                            : null,
                        Direction = finalDirection,
                        FeedSource = feedName,
                        TrainId = trainId,
                        ScheduledTrack = scheduledTrack,
                        ActualTrack = actualTrack
                    });
                }

                allStations.Add(new StationInfo
                {
                    StationId = stopUpdate.StopId ?? string.Empty,
                    StationName = GetStationName(stopUpdate.StopId ?? string.Empty),
                    Direction = finalDirection
                });
            }
        }

        private string DetermineDirectionFromStopId(string? stopId)
        {
            // MTA stop IDs end with N (northbound) or S (southbound)
            if (stopId == null)
                return "Unknown";
            else if (stopId.EndsWith('N'))
                return "North";
            else if (stopId.EndsWith('S'))
                return "South";
            else
                return "Unknown";
        }

        private string GetStationName(string stopId)
        {
            if (string.IsNullOrEmpty(stopId))
                return "Unknown Station";

            // Clean up the stop ID to remove direction suffix
            var cleanId = stopId.TrimEnd('N', 'S');

            return StationNames.All.TryGetValue(cleanId, out var name)
                ? name
                : $"Station {stopId}";
        }
    }
}