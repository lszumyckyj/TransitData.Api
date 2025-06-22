using TransitData.Api.Models.DTOs;

namespace TransitData.Api.Repositories.Interfaces
{
    public interface ITransitRealtimeDataRepository
    {
        Task<AllRealtimeDataResponse?> GetAllTransitRealtimeDataAsync();
        Task StoreAllTransitRealtimeDataAsync(AllRealtimeDataResponse data);
        Task StoreTrainsByStationRealtimeAsync(List<Train> trains);
        Task StoreStationsAsync(List<Station> stations);
    }
}