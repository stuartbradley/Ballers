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
    }

    public class UpdateTeamProfileRequest
    {
        public string Name { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? ManagerName { get; set; }
        public int? YearFormed { get; set; }
        public string? Bio { get; set; }
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
        public string? ImageUrl { get; set; }
    }
}
