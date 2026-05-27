using Microsoft.AspNetCore.Identity;

namespace Ballers.API.Models
{
    public class ApplicationUser:IdentityUser
    {
        public int? TeamId { get; set; }
        public Team? Team { get; set; }
        public bool IsAdmin { get; set; }

    }
}
