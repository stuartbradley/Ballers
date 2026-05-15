namespace Ballers.Models
{
    public class UserInfo
    {
        public string Email { get; set; } = "";
        public int? TeamId { get; set; }
        public bool IsAdmin { get; set; }
        public string? TeamName { get; set; }
    }
}
