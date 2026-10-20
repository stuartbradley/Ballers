namespace Ballers.Models;

public class ResetPasswordRequest
{
    public string Email { get; set; } = "";
    public string Token { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
