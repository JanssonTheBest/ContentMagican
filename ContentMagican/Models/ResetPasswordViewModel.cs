namespace ContentMagican.Models
{
    public class ResetPasswordViewModel
    {
        public string Identifier { get; set; }
        public string NewPassword { get; set; }

        // Optional: For displaying messages and errors
        public string Message { get; set; }
        public string Error { get; set; }
    }
}
