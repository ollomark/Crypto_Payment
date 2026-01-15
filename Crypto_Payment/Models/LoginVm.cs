using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Crypto_Payment.Models;

public class LoginVm
{
    [Required]
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
    
    [Required]
    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
    
    [Required]
    [JsonPropertyName("rememberMe")]
    public bool RememberMe { get; set; }
}