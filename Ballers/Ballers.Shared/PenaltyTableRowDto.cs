namespace Ballers.Models
{
    public class PenaltyTableRowDto
    {
        public int Position { get; set; }
        public string Team { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int PenaltiesFor { get; set; }
        public int PenaltiesAgainst { get; set; }
        public int Points => Won * 3 + Drawn;
        public int PenaltyDifference => PenaltiesFor - PenaltiesAgainst;
    }
}
