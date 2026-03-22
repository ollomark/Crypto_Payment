namespace Crypto_Payment.DTOS;

using System.ComponentModel.DataAnnotations;

public class UserDto
{
    [Required(ErrorMessage = "User ID is required.")]
    public string Id { get; set; }

    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; set; }

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(50, ErrorMessage = "Full name can be at most 50 characters.")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    public string PhoneNumber { get; set; }

    [Required(ErrorMessage = "User status is required.")]
    public bool IsActive { get; set; }
}
