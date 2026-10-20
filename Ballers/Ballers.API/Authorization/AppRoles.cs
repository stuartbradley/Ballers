namespace Ballers.API.Authorization
{
    /// <summary>
    /// The Identity roles an account can hold. Controllers and policies check
    /// the role. The IsAdmin and IsReferee columns on ApplicationUser are set
    /// alongside it and mirror it, for services that only have the account row
    /// (PlayerAccountService, UserService); never set one without the other.
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string Referee = "Referee";
        public const string Player = "Player";

        public static readonly string[] All = [Admin, Manager, Referee, Player];
    }
}
