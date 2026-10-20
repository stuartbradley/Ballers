using Ballers.API.Authorization;
using Ballers.API.Services;
using Ballers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [ApiController]
    [Route("api/referees")]
    [Authorize]
    public class RefereesController : ControllerBase
    {
        private readonly IRefereeService _referees;

        public RefereesController(IRefereeService referees) => _referees = referees;

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _referees.GetAllAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SaveRefereeRequest request)
            => this.ToActionResult(await _referees.CreateAsync(request));

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SaveRefereeRequest request)
            => this.ToActionResult(await _referees.UpdateAsync(id, request));

        [HttpDelete("{id}")]
        [Authorize(Policy = Policies.SiteAdmin)]
        public async Task<IActionResult> Delete(int id)
            => this.ToActionResult(await _referees.DeleteAsync(id), NoContent);
    }
}
