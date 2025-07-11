namespace TransitData.Api.Models.DTOs
{
    public record Station
    {
        public string StationId { get; init; } = string.Empty;
        public string StationName { get; init; } = string.Empty;
        public string Direction { get; init; } = string.Empty;
    }
}