namespace Ballers.API.Models
{
    public class PenaltyKick
    {
        public int Id { get; set; }
        public int ShootoutId { get; set; }
        public PenaltyShootout? Shootout { get; set; }
        public int PlayerId { get; set; }
        public Player? Player { get; set; }
        public int TeamId { get; set; }
        public Team? Team { get; set; }
        public int Order { get; set; }
        public bool Scored { get; set; }
    }
}
