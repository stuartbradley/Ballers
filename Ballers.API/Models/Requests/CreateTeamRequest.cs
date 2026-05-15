namespace Ballers.API.Models.Requests
{
    public class CreateTeamRequest
    {
        public string TeamName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
