namespace Ballers.API.Models.Requests
{
    public class CreatePlayerRequest
    {
        public string Position { get; set; } = "";
        public string Name { get; set; } = "";
        public int Number {  get; set; }    
    }
}
