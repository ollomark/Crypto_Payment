namespace Crypto_Payment.DTOS;

public class MailingRecipientPreviewDto
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class MailingPreviewDto
{
    public string Segment { get; set; } = "";
    public int Count { get; set; }
    public List<MailingRecipientPreviewDto> Recipients { get; set; } = new();
}

public class MailingSendRequestDto
{
    public string Segment { get; set; } = "";
    public string Subject { get; set; } = "";
    public string HtmlBody { get; set; } = "";
}

public class MailingSendResultDto
{
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int SkippedNoEmail { get; set; }
    public List<string> Errors { get; set; } = new();
}
