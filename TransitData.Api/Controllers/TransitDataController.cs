using Microsoft.AspNetCore.Mvc;
using TransitData.Api.Models.DTOs;
using TransitData.Api.Repositories.Interfaces;

namespace TransitData.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class TransitDataController(ITransitDataRepository transitDataRepository) : ControllerBase
    {
        private readonly ITransitDataRepository TransitDataRepository = transitDataRepository;

        // GET: api/v1/transitdata
        [HttpGet]
        public async Task<ActionResult<MtaAllDataResponse>> GetAllTransitDataAsync()
        {
            MtaAllDataResponse? transitData = await TransitDataRepository.GetAllTransitDataAsync();
            if (transitData == null)
            {
                return NotFound();
            }
            return Ok(transitData);
        }
    }
}