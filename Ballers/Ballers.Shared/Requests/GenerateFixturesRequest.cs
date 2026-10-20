namespace Ballers.Models
{
    public class GenerateFixturesRequest
    {
        public DateTime StartDate { get; set; } 
        public List<int> TeamIds { get; set; } = new();
    }
}
