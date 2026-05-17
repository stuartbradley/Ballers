namespace Ballers.API.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? ManagerName { get; set; }
        public int? YearFormed { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
    }
}
