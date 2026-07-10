using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/motd")]
    public class MotdController : ControllerBase
    {
        private readonly IMotdService _motd;

        public MotdController(IMotdService motd) => _motd = motd;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetList() => Ok(await _motd.GetListAsync());

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var post = await _motd.GetByIdAsync(id);
            return post == null ? NotFound() : Ok(post);
        }

        [HttpGet("fixture-options")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetFixtureOptions() => Ok(await _motd.GetFixtureOptionsAsync());

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] SaveMotdRequest request)
        {
            if (request.FixtureId <= 0) return BadRequest("A match must be selected.");
            if (string.IsNullOrWhiteSpace(request.CoverImageBase64)) return BadRequest("A cover photo is required.");

            var id = await _motd.CreateAsync(request);
            return Ok(new { id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveMotdRequest request)
        {
            if (request.FixtureId <= 0) return BadRequest("A match must be selected.");
            if (string.IsNullOrWhiteSpace(request.CoverImageBase64)) return BadRequest("A cover photo is required.");

            return await _motd.UpdateAsync(id, request) ? Ok() : NotFound();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
            => await _motd.DeleteAsync(id) ? Ok() : NotFound();
    }
}
