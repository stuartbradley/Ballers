using Ballers.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/totw")]
    public class TotwController : ControllerBase
    {
        private readonly ITotwService _svc;
        public TotwController(ITotwService svc) => _svc = svc;

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var dto = await _svc.GetCurrentAsync();
            return dto == null ? NotFound() : Ok(dto);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var list = await _svc.GetHistoryAsync();
            return Ok(list);
        }

        [HttpGet("weeks")]
        public async Task<IActionResult> GetWeeks()
        {
            var weeks = await _svc.GetWeeksAsync();
            return Ok(weeks);
        }

        [HttpGet("{matchNumber:int}")]
        public async Task<IActionResult> GetWeek(int matchNumber)
        {
            var dto = await _svc.GetWeekAsync(matchNumber);
            return dto == null ? NotFound() : Ok(dto);
        }
    }
}
