namespace Ballers.API.Models
{
    public class FairplayRating
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public Fixture? Fixture { get; set; }
        public int TeamId { get; set; }
        public Team? Team { get; set; }
        public int Rating { get; set; }
    }
}
