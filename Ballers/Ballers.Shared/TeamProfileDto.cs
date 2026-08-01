namespace Ballers.Models
{
    public class TeamProfileDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? ManagerName { get; set; }
        public int? YearFormed { get; set; }
        public string? Bio { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? ManagerImageBase64 { get; set; }
        public string? HomeKitColour { get; set; }
        public string? AwayKitColour { get; set; }
        public int Wins { get; set; }
    }

    public class UpdateTeamProfileRequest
    {
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? ManagerName { get; set; }
        public int? YearFormed { get; set; }
        public string? Bio { get; set; }
        public string? HomeKitColour { get; set; }
        public string? AwayKitColour { get; set; }
    }

    public class UploadManagerImageRequest
    {
        public string? ImageBase64 { get; set; }
    }

    public class PlayerCardDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Position { get; set; } = "MID";
        public int Number { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Motm { get; set; }
        // Only counted for goalkeepers and defenders, who earn their card tier
        // from clean sheets rather than goals and assists. Always 0 for others.
        public int CleanSheets { get; set; }
        public string? ImageUrl { get; set; }
    }
}
