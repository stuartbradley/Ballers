namespace Ballers.Models
{
    public class PenaltyShootoutDto
    {
        public List<PenaltyKickResultDto> HomeKicks { get; set; } = new();
        public List<PenaltyKickResultDto> AwayKicks { get; set; } = new();
    }

    public class PenaltyKickResultDto
    {
        public int Order { get; set; }
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public bool Scored { get; set; }
    }

    public class PenaltyKickInput
    {
        public int PlayerId { get; set; }
        public int Order { get; set; }
        public bool Scored { get; set; }
    }
}
