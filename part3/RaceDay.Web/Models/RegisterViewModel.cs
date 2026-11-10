using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models
{
    /// <p>
    /// View model representing the data transferred from the user registration form 
    /// to the Web application controller, enforcing validation rules before API submission.
    /// </p>
    public class RegisterViewModel
    {
        // Full legal name of the user registering an account
        [Required(ErrorMessage = "Full Name is required")]
        public string FullName { get; set; } = string.Empty;

        // Unique email address used for authentication and account identification
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        // Secure password string meeting minimum length constraints
        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string Password { get; set; } = string.Empty;

        // Confirmation matching field to verify password accuracy during entry
        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}