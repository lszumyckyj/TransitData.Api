using TransitData.Api.Models.DTOs;

namespace TransitData.Api.Services.Interfaces
{
    public interface IMtaService
    {
        Task<MtaAllDataResponse> GetAllTrainDataAsync();
    }
}