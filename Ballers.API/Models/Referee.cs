namespace Ballers.API.Models
{
    public class Referee
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public ICollection<Fixture> Fixtures { get; set; } = new List<Fixture>();
    }
}
