namespace TransitData.Api.Models.DTOs
{
    public record MtaAllDataResponse
    {
        public DateTime LastUpdated { get; init; }
        public List<TrainInfo> Trains { get; init; } = new();
        public List<StationInfo> Stations { get; init; } = new();
        public int TotalTrains { get; init; }
        public int TotalStations { get; init; }
    }
}