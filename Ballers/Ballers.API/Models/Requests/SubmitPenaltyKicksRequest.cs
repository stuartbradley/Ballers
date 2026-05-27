namespace Ballers.API.Models.Requests
{
    public class SubmitPenaltyKicksRequest
    {
        public List<PenaltyKickEntry> Kicks { get; set; } = new();
    }

    public class PenaltyKickEntry
    {
        public int PlayerId { get; set; }
        public int Order { get; set; }
        public bool Scored { get; set; }
    }
}
