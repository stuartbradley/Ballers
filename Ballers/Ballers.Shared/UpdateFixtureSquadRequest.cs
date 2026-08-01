namespace Ballers.Shared
{
    public class UpdateFixtureSquadRequest
    {
        public List<int> PlayerIds { get; set; } = new();

        // Which side of the fixture is being saved. Only admins and referees may
        // set this, to pick between the home and away teams; a manager's team is
        // always taken from their own account.
        public int? TeamId { get; set; }
    }
}
