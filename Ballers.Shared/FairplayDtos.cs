namespace Ballers.Models
{
    public class FairplayFixtureDto
    {
        public int? HomeRating { get; set; }
        public int? AwayRating { get; set; }
    }

    public class SubmitFairplayRequest
    {
        public int HomeRating { get; set; }
        public int AwayRating { get; set; }
    }

    public class FairplayTableRowDto
    {
        public int Position { get; set; }
        public string Team { get; set; } = "";
        public int Rated { get; set; }
        public int TotalRating { get; set; }
        public double AverageRating { get; set; }
    }
}
