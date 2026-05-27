using System.ComponentModel.DataAnnotations;

namespace Ballers.Models
{
    public class CreateTeamViewModel
    {
        [Required]
        public string TeamName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$",
            ErrorMessage = "Password must contain upper, lower, number and special character.")]
        public string Password { get; set; } = "";
    }
}
