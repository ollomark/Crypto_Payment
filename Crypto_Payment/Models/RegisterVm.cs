using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Crypto_Payment.Models;

public class RegisterVm
{
    [Required]
    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [Required]
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "";

    [Required]
    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}