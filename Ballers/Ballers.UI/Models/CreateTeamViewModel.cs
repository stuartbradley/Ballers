using System.ComponentModel.DataAnnotations;

namespace Ballers.Models
{
    public class CreateTeamViewModel : IValidatableObject
    {
        public string TeamName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = "Password must contain upper, lower, number and special character.")]
        public string Password { get; set; } = "";

        public bool IsReferee { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!IsReferee && string.IsNullOrWhiteSpace(TeamName))
                yield return new ValidationResult("The TeamName field is required.", new[] { nameof(TeamName) });
        }
    }
}
