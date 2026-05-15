namespace Ballers.API.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public int TeamId { get; set; }
        public Team? Team { get; set; }
        public bool IsActive { get; set; } = true;
        public string Position { get; set; } = "MID";

    }
}
