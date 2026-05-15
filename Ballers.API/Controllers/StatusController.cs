using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ballers.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        [HttpGet]
        public IActionResult status()
        {
            return Ok("Im working");
        }
    }
}
