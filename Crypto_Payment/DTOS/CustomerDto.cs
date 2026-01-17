using System.ComponentModel.DataAnnotations;

namespace Crypto_Payment.DTOS;

public class CustomerDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Ad boş geçilemez.")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Soyad boş geçilemez.")]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Telefon boş geçilemez.")]
    public string Phone { get; set; }

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Şirket adı boş geçilemez.")]
    public string CompanyName { get; set; }

    [Required(ErrorMessage = "Telegram boş geçilemez.")]
    public string Telegram { get; set; }

    [Required(ErrorMessage = "Skype boş geçilemez.")]
    public string Skype { get; set; }
}
