namespace Ballers.API.Models.Requests
{
    public class GenerateFixturesRequest
    {
        public DateTime StartDate { get; set; } 
        public List<int> TeamIds { get; set; } = new();
    }
}
