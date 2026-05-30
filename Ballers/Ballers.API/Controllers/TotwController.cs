using Ballers.API.Services;
using Microsoft.AspNetCore.Authorization;
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

        [HttpPost("generate/{matchNumber:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Generate(int matchNumber)
        {
            var dto = await _svc.GenerateAsync(matchNumber);
            return dto == null
                ? BadRequest("No played fixtures for this match number in the active season.")
                : Ok(dto);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _svc.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}
