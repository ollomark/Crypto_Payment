namespace Crypto_Payment.Models;

public class Customer
{
    public int Id { get; set; }

    public string FirstName { get; set; }      // Ad
    public string LastName { get; set; }       // Soyad
    public string Phone { get; set; }          // Telefon
    public string Email { get; set; }          // E-posta

    public string CompanyName { get; set; }    // Şirket Adı

    public string Telegram { get; set; }       // Telegram adresi / kullanıcı adı
    public string Skype { get; set; }          // Skype adresi
}
