using System.ComponentModel.DataAnnotations;

namespace ContentMagican.Database
{
    public class RegisterAtempt
    {
        public int Id { get; set; }
        public string AttemptId { get; set; }
        public string EncryptedIdentifier { get; set; }
    }
}
