using System.ComponentModel.DataAnnotations;

namespace Ballers.Models
{
    public enum AccountType
    {
        Team,
        Referee,
        Admin
    }

    public class CreateTeamViewModel : IValidatableObject
    {
        public AccountType AccountType { get; set; } = AccountType.Team;

        public string TeamName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = "Password must contain upper, lower, number and special character.")]
        public string Password { get; set; } = "";

        public bool IsTeam => AccountType == AccountType.Team;
        public bool IsReferee => AccountType == AccountType.Referee;
        public bool IsAdmin => AccountType == AccountType.Admin;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsTeam && string.IsNullOrWhiteSpace(TeamName))
                yield return new ValidationResult("The TeamName field is required.", new[] { nameof(TeamName) });
        }
    }
}
