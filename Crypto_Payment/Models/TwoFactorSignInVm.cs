namespace Crypto_Payment.Models;

public class TwoFactorSignInVm
{
    public string Code { get; set; } = "";
    public bool RememberMe { get; set; }
    public bool RememberClient { get; set; }
}