using Microsoft.AspNetCore.Identity;

namespace Crypto_Payment.Models;

public class User : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}