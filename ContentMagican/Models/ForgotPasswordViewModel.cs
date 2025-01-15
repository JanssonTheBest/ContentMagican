using System.ComponentModel.DataAnnotations;

namespace ContentMagican.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        public string Email { get; set; }

        // To display success or error messages
        public string Message { get; set; }
        public string Error { get; set; }
    }
}
