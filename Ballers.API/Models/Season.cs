namespace Ballers.API.Models
{
    public class Season
    {
        public int Id { get; set; }
        public int SeasonNumber { get; set; }
        public DateTime StartDate { get; set; } 
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public ICollection<Fixture> Fixtures { get;set; }
    }
}
