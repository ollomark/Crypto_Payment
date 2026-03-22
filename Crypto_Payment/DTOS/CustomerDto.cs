using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class CustomerDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Phone is required.")]
    public string Phone { get; set; }

    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string? Email { get; set; }

    public string? CompanyName { get; set; }

    public string? Telegram { get; set; }

    public string? Skype { get; set; }
}
