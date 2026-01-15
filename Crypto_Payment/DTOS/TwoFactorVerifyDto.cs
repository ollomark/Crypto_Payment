namespace Crypto_Payment.DTOS;

public class TwoFactorVerifyDto
{
    public string Code { get; set; } = default!;
    public bool RememberMe { get; set; }
    public bool RememberClient { get; set; } // "bu cihazda bir daha sorma"
}