namespace TransitData.Api.Models.DTOs
{
    public record AllRealtimeDataResponse
    {
        public DateTime LastUpdated { get; init; }
        public List<Train> Trains { get; init; } = new();
        public List<Station> Stations { get; init; } = new();
        public int TotalTrains { get; init; }
        public int TotalStations { get; init; }
    }
}