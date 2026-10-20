namespace Ballers.Models
{
    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? ProfileImageUrl { get; set; }
        public string? ManagerName { get; set; }
    }
}
