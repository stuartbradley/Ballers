namespace Ballers.Models
{
    public class RefereeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public List<RefereeFixtureDto> UpcomingFixtures { get; set; } = new();
    }

    public class RefereeFixtureDto
    {
        public int FixtureId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public DateTime? Kickoff { get; set; }
    }

    public class SaveRefereeRequest
    {
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }
}
