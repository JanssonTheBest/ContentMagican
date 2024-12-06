using ContentMagican.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ContentMagican.Database
{
    public class OrderLog
    {
            public int Id { get; set; }

            [Required]
            public int UserId { get; set; }

            [Required]
            [MaxLength(450)]
            public string SessionId { get; set; }

            [Required]
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;        
            public string Status { get; set; } = "Unconfirmed";        
            public string ProductId { get; set; }
    }
}
