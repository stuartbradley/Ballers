namespace Ballers.API.Models
{
    public class MatchOfTheDayPost
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public Fixture? Fixture { get; set; }
        public string CoverImageBase64 { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<MatchOfTheDayPhoto> Photos { get; set; } = new List<MatchOfTheDayPhoto>();
    }

    public class MatchOfTheDayPhoto
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public MatchOfTheDayPost? Post { get; set; }
        public string ImageBase64 { get; set; } = "";
        public int SortOrder { get; set; }
    }
}
