using System.Text.Json.Nodes;
using Crypto_Payment.Data;

namespace Crypto_Payment.Services;

public interface ITelegramBotService
{
    Task SendMessageAsync(string message);
    Task SendMessageAsync(long chatId, string message);
    Task SendMessageWithKeyboardAsync(long chatId, string text, object replyMarkup);
    Task AnswerCallbackQueryAsync(string callbackQueryId, string? text = null);
    Task HandleUpdateAsync(JsonObject update, AppDbContext db);
    Task RegisterWebhookAsync(string webhookUrl);
}
