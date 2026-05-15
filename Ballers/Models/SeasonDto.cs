namespace Ballers.Models
{
    public class SeasonDto
    {
        public int Id { get; set; }
        public int SeasonNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }

        public string DisplayName => $"Season {SeasonNumber} ({StartDate:dd MM yyyy} - {EndDate:dd MM yyyy})";
    }
}
