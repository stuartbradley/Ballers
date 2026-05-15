namespace Ballers.Models
{
    public class PlayerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string Position { get; set; } = "";
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int TeamId { get; set; }

    }
}
