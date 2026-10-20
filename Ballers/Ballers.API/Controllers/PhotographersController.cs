using Ballers.API.Authorization;
using Ballers.API.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotographersController : ControllerBase
    {
        private readonly IPhotographerService _photographers;

        public PhotographersController(IPhotographerService photographers) => _photographers = photographers;

        // Public: the gallery page credits the photographer, so anyone viewing a
        // gallery needs to be able to read these.
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _photographers.GetAllAsync());

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPost]
        public async Task<IActionResult> Create(PhotographerDto request)
            => this.ToActionResult(await _photographers.CreateAsync(request), id => new { Id = id });

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, PhotographerDto request)
            => this.ToActionResult(await _photographers.UpdateAsync(id, request));

        [Authorize(Policy = Policies.SiteAdmin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
            => this.ToActionResult(await _photographers.DeleteAsync(id));
    }
}
