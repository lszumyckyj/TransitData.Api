using Microsoft.AspNetCore.Mvc;
using TransitData.Api.Models.DTOs;
using TransitData.Api.Repositories.Interfaces;

namespace TransitData.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class TransitRealtimeDataController(ITransitRealtimeDataRepository transitRealtimeDataRepository) : ControllerBase
    {
        private readonly ITransitRealtimeDataRepository TransitRealtimeDataRepository = transitRealtimeDataRepository;

        // GET: api/v1/transitrealtimedata
        [HttpGet]
        public async Task<ActionResult<AllRealtimeDataResponse>> GetAllTransitRealtimeDataAsync()
        {
            AllRealtimeDataResponse? transitData = await TransitRealtimeDataRepository.GetAllTransitRealtimeDataAsync();
            if (transitData == null)
            {
                return NotFound();
            }
            return Ok(transitData);
        }
    }
}