using TransitData.Api.Models.DTOs;

namespace TransitData.Api.Repositories.Interfaces
{
    public interface ITransitDataRepository
    {
        Task<MtaAllDataResponse?> GetAllTransitDataAsync();
        Task StoreAllTransitDataAsync(MtaAllDataResponse data);
        Task StoreTrainsByStationAsync(List<TrainInfo> trains);
        Task StoreStationsAsync(List<StationInfo> stations);
    }
}