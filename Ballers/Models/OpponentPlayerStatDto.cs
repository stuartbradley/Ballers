namespace Ballers.Models
{
    public class OpponentPlayerStatDto
    {
        public string Name { get; set; } = "";
        public int Goals { get; set; }
        public int Assists { get; set; }
        public bool IsMotm { get; set; }
        public bool YellowCard { get; set; }
        public bool RedCard { get; set; }
    }
}
