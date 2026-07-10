namespace Ballers.Models
{
    // List tile: cover photo + caption (teams, date, location).
    public class MotdListItemDto
    {
        public int Id { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public DateTime? Date { get; set; }
        public string? Location { get; set; }
        public string? Postcode { get; set; }
        public string CoverImageBase64 { get; set; } = "";
        public string Caption => $"{HomeTeam} vs {AwayTeam}";
    }

    public class MotdDetailDto
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public DateTime? Date { get; set; }
        public string? Location { get; set; }
        public string? Postcode { get; set; }
        public string CoverImageBase64 { get; set; } = "";
        public string Body { get; set; } = "";
        public List<string> Photos { get; set; } = new();
        public string Caption => $"{HomeTeam} vs {AwayTeam}";
    }

    // Played fixtures for the admin's "pick a match" dropdown.
    public class MotdFixtureOptionDto
    {
        public int FixtureId { get; set; }
        public string Label { get; set; } = "";
    }

    public class SaveMotdRequest
    {
        public int FixtureId { get; set; }
        public string CoverImageBase64 { get; set; } = "";
        public string Body { get; set; } = "";
        public List<string> Photos { get; set; } = new();
    }
}
