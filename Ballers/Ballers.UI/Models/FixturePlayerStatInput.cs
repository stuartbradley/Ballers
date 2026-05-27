namespace Ballers.Models
{
    public class FixturePlayerStatInput
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public string Position { get; set; } = "";
        public int Goals { get; set; }
        public int Assists { get; set; }
        public bool IsManOfTheMatch { get; set; }
        public bool HadYellowCard { get; set; }
        public bool HadRedCard { get; set; }
    }
}
