using TransitData.Api.Models.DTOs;

namespace TransitData.Api.Services.Interfaces
{
    public interface IGtfsFeedService
    {
        Task<AllRealtimeDataResponse> GetAllTransitRealtimeDataAsync();
    }
}