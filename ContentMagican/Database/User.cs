using System.Numerics;

namespace ContentMagican.Database
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PlanId { get; set; } = "1";
        public string? CustomerId { get; set; }
    }
}
