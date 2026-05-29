namespace Ballers.API.Models.Requests
{
    public class UpdatePlayerRequest
    {
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string Position { get; set; } = "";
    }
}
