namespace Ballers.API.Models
{
    public class FixturePlayer
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public int PlayerId { get; set; }
        public Player? Player { get; set; }
        public Fixture? Fixture { get; set; }
    }
}
