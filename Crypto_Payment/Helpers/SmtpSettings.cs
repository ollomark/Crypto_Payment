namespace Crypto_Payment.Helpers;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string User { get; set; } = "";
    public string Pass { get; set; } = "";
    public string From { get; set; } = "";
    public string DisplayName { get; set; } = "";
}