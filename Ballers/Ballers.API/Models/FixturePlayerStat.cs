namespace Ballers.API.Models
{
    public class FixturePlayerStat
    {
        public int Id { get; set; } 
        public int FixtureId { get; set; }
        public Fixture? Fixture {  get; set; }   
        public int PlayerId { get; set; }
        public Player? Player { get; set; }

        public int Goals { get; set; }

        // Own goals are kept apart from Goals on purpose: they count towards the
        // opposing team's score but never towards this player's scoring record.
        public int OwnGoals { get; set; }

        public int Assists { get; set; }
        public bool YellowCards { get; set; }    
        public bool RedCard {  get; set; }   
        public bool ManOfTheMatch { get; set; } 

    }
}
