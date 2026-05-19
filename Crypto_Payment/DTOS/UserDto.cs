namespace Crypto_Payment.DTOS;

using System.ComponentModel.DataAnnotations;

public class UserDto
{
    [Required(ErrorMessage = "Kullanıcı Id boş olamaz.")]
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    [MaxLength(50, ErrorMessage = "Ad Soyad 50 karakter olabilir.")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Telefon numarası zorunludur.")]
    [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "Kullanıcı durumu zorunludur.")]
    public bool IsActive { get; set; }
}
