namespace TransitData.Api.Models.DTOs
{
    public record TrainInfo
    {
        public string RouteId { get; init; } = string.Empty; // "6", "N", "Q", etc.
        public string TripId { get; init; } = string.Empty;
        public string StationId { get; init; } = string.Empty; // "626N", "R20S", etc.
        public DateTime? ArrivalTime { get; init; }
        public DateTime? DepartureTime { get; init; }
        public string Direction { get; init; } = string.Empty; // North or South (East and West are not currently used)
        public string FeedSource { get; init; } = string.Empty; // Which feed this came from
        public string? TrainId { get; init; } // NYC-specific
        public string? ScheduledTrack { get; init; } // NYC-specific
        public string? ActualTrack { get; init; } // NYC-specific

        // Calculated each time it's accessed
        public int? MinutesAway => ArrivalTime?.Subtract(DateTime.Now).Minutes;
    }
}