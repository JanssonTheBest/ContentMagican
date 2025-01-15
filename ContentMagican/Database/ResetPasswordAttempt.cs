namespace ContentMagican.Database
{
    public class ResetPasswordAttempt
    {
        public int Id { get; set; } // Auto-increment ID
        public int UserId { get; set; } // User ID
        public string Identifier { get; set; } // Unique string identifier
    }

}
