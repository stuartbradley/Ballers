namespace Ballers.API.Models
{
    public class PenaltyShootout
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public Fixture? Fixture { get; set; }
        public ICollection<PenaltyKick> Kicks { get; set; } = new List<PenaltyKick>();
    }
}
