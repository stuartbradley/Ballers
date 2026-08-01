namespace Ballers.Shared
{
    public class SaveCaptaincyRequest
    {
        public int? CaptainPlayerId { get; set; }
        public int? ViceCaptainPlayerId { get; set; }

        // Admin/referee only — picks the home or away side. Ignored for managers,
        // who always act for their own team.
        public int? TeamId { get; set; }
    }
}
