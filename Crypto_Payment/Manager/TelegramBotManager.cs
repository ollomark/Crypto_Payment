using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crypto_Payment.Data;
using Crypto_Payment.Services;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Manager;

public class TelegramBotManager : ITelegramBotService
{
    private readonly HttpClient _http;
    private readonly ILogger<TelegramBotManager> _logger;
    private readonly string _botToken;
    private readonly long _defaultChatId;

    public TelegramBotManager(HttpClient http, ILogger<TelegramBotManager> logger)
    {
        _http = http; _logger = logger;
        _botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? "";
        long.TryParse(Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID") ?? "0", out _defaultChatId);
    }

    private async Task ApiCallAsync(string method, JsonObject p)
    {
        if (string.IsNullOrEmpty(_botToken)) { _logger.LogWarning("Telegram: BOT_TOKEN bos, {M} atildi", method); return; }
        try {
            var json = p.ToJsonString();
            _logger.LogInformation("Telegram {M} gonderiliyor. JSON: {J}", method, json[..Math.Min(200, json.Length)]);
            var resp = await _http.PostAsync($"https://api.telegram.org/bot{_botToken}/{method}",
                new StringContent(json, Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Telegram {M} HATA {S}: {B}", method, resp.StatusCode, body[..Math.Min(300,body.Length)]);
            else
                _logger.LogInformation("Telegram {M} OK: {B}", method, body[..Math.Min(100,body.Length)]);
        } catch (Exception ex) { _logger.LogError(ex, "Telegram {M} exception", method); }
    }

    public Task SendMessageAsync(string msg) => SendMessageAsync(_defaultChatId, msg);
    public async Task SendMessageAsync(long chatId, string msg) {
        if (chatId == 0) return;
        await ApiCallAsync("sendMessage", new JsonObject {
            ["chat_id"]=JsonValue.Create(chatId), ["text"]=msg, ["parse_mode"]="HTML", ["disable_web_page_preview"]=JsonValue.Create(true) });
    }
    public async Task SendMessageWithKeyboardAsync(long chatId, string text, object kb) {
        if (chatId == 0) return;
        var p = new JsonObject { ["chat_id"]=JsonValue.Create(chatId), ["text"]=text, ["parse_mode"]="HTML",
            ["disable_web_page_preview"]=JsonValue.Create(true), ["reply_markup"]=JsonSerializer.SerializeToNode(kb) };
        await ApiCallAsync("sendMessage", p);
    }
    public async Task AnswerCallbackQueryAsync(string id, string? text=null) =>
        await ApiCallAsync("answerCallbackQuery", new JsonObject { ["callback_query_id"]=id, ["text"]=text??"" });
    public async Task RegisterWebhookAsync(string url) {
        await ApiCallAsync("setWebhook", new JsonObject { ["url"]=url });
        _logger.LogInformation("Webhook: {U}", url);
    }

    private static JsonObject Btn(string t, string cd) => new() { ["text"]=t, ["callback_data"]=cd };
    private static JsonObject BtnUrl(string t, string u) => new() { ["text"]=t, ["url"]=u };
    private static JsonArray Row(params JsonObject[] bs) { var a=new JsonArray(); foreach(var b in bs) a.Add(b); return a; }
    private static JsonObject Kb(params JsonArray[] rows) {
        var g=new JsonArray(); foreach(var r in rows) g.Add(r);
        return new JsonObject { ["inline_keyboard"]=g };
    }
    private async Task SendMsgKb(long chatId, string text, JsonObject kb) {
        if (chatId == 0) return;
        var p = new JsonObject { ["chat_id"]=JsonValue.Create(chatId), ["text"]=text, ["parse_mode"]="HTML",
            ["disable_web_page_preview"]=JsonValue.Create(true), ["reply_markup"]=kb };
        await ApiCallAsync("sendMessage", p);
    }

    public async Task HandleUpdateAsync(JsonObject update, AppDbContext db) {
        try {
            if (update["callback_query"] is JsonObject cbq) {
                var cbqId=cbq["id"]?.GetValue<string>()??"";
                var chatId=cbq["message"]?["chat"]?["id"]?.GetValue<long>()??0;
                var msgId=cbq["message"]?["message_id"]?.GetValue<int>()??0;
                var data=cbq["data"]?.GetValue<string>()??"";
                await AnswerCallbackQueryAsync(cbqId);
                await HandleCb(chatId, msgId, data, db); return;
            }
            if (update["message"] is JsonObject msg) {
                var chatId=msg["chat"]?["id"]?.GetValue<long>()??0;
                var text=msg["text"]?.GetValue<string>()?.Trim()??"";
                if (chatId==0) return;
                await HandleCmd(chatId, text.Split(" ")[0].Split("@")[0].ToLowerInvariant(), db);
            }
        } catch (Exception ex) { _logger.LogError(ex, "Update error"); }
    }

    private async Task HandleCmd(long c, string cmd, AppDbContext db) {
        switch(cmd) {
            case "/start": case "/menu": await Menu(c); break;
            case "/odenen_faturalar": case "/odenmis": await InvList(c,db,"completed"); break;
            case "/bekleyen_faturalar": case "/bekleyen": await InvList(c,db,"pending"); break;
            case "/gecikmis_faturalar": case "/gecikmis": await OverdueList(c,db); break;
            case "/suresi_dolan": case "/expired": await InvList(c,db,"expired"); break;
            case "/iptal_faturalar": case "/iptal": await InvList(c,db,"cancelled"); break;
            case "/otomatik_faturalar": case "/otomatik": await RecList(c,db); break;
            case "/istatistik": case "/stats": await Stats(c,db); break;
            case "/musteriler": await CustList(c,db); break;
            case "/son_faturalar": await Recent(c,db); break;
            default: await Menu(c); break;
        }
    }

    private async Task HandleCb(long c, int m, string data, AppDbContext db) {
        var invParts = data.StartsWith("inv_detail:") ? data.Split(":") : null;
        if (invParts != null && invParts.Length >= 2 && int.TryParse(invParts[1], out var i1)) {
            var fromOverdue = invParts.Length >= 3 && invParts[2] == "overdue";
            await InvDetail(c, i1, db, fromOverdue);
            return;
        }
        else if (data.StartsWith("inv_list:")) await InvList(c,db,data.Split(":")[1]);
        else if (data == "overdue_list") await OverdueList(c,db);
        else if (data.StartsWith("cust_detail:") && int.TryParse(data.Split(":")[1], out var i3)) await CustDetail(c,i3,db);
        else switch(data) {
            case "menu": await Menu(c); break;
            case "stats": await Stats(c,db); break;
            case "recurring_list": await RecList(c,db); break;
            case "customers": await CustList(c,db); break;
            case "recent": await Recent(c,db); break;
        }
    }

    private async Task Menu(long c) {
        var kb = Kb(
            Row(Btn("✅ Ödenmiş Faturalar","inv_list:completed"), Btn("⏳ Bekleyen Faturalar","inv_list:pending")),
            Row(Btn("⚠️ Gecikmiş Faturalar","overdue_list"),       Btn("❌ Süresi Dolan","inv_list:expired")),
            Row(Btn("🚫 İptal Edilenler","inv_list:cancelled"),   Btn("🔄 Yinelenen Faturalar","recurring_list")),
            Row(Btn("👥 Müşteriler","customers"), Btn("📊 İstatistikler","stats")),
            Row(Btn("🕐 Son Faturalar","recent"))
        );
        await SendMsgKb(c, "🏠 <b>Crypto Payment Yönetim Paneli</b>\n\nAşağıdaki menüden işlem seçin:", kb);
    }

    private async Task InvList(long c, AppDbContext db, string status) {
        var (label,emoji) = SL(status);
        var list = await db.Invoices.Where(i=>i.RegistrationStatus && i.Status==status)
            .Include(i=>i.Customer).OrderByDescending(i=>i.CreatedDate).Take(10).ToListAsync();
        var bkb = Kb(Row(Btn("🏠 Ana Menü","menu")));
        if (!list.Any()) { await SendMsgKb(c,$"{emoji} <b>{label}</b>\n\n📭 Kayıt bulunamadı.",bkb); return; }
        var sb = new StringBuilder();
        sb.AppendLine($"{emoji} <b>{label}</b>\n<i>Son {list.Count} kayıt</i>\n");
        var rows = new List<JsonArray>();
        foreach (var inv in list) {
            var sym = CS(inv.SourceCurrency);
            var name = inv.Customer!=null ? $"{inv.Customer.FirstName} {inv.Customer.LastName}" : inv.Email;
            sb.AppendLine($"• <b>{H(inv.OrderName)}</b> — {sym}{inv.SourceAmount:N2}");
            sb.AppendLine($"  👤 {H(name)} | 📅 {inv.CreatedDate:dd.MM.yyyy}");
            rows.Add(Row(Btn($"#{inv.Id} {T(inv.OrderName,22)}",$"inv_detail:{inv.Id}")));
        }
        rows.Add(Row(Btn("🏠 Ana Menü","menu")));
        await SendMsgKb(c, sb.ToString(), Kb(rows.ToArray()));
    }

    private async Task InvDetail(long c, int id, AppDbContext db, bool fromOverdue = false) {
        var inv = await db.Invoices.Include(i=>i.Customer).Include(i=>i.InvoiceItems).FirstOrDefaultAsync(i=>i.Id==id);
        if (inv==null) { await SendMessageAsync(c,"❌ Fatura bulunamadı."); return; }
        var sym = CS(inv.SourceCurrency);
        var (sl,se) = SL(inv.Status??"");
        var cname = inv.Customer!=null ? $"{inv.Customer.FirstName} {inv.Customer.LastName}" : inv.Email;
        var sb = new StringBuilder();
        sb.AppendLine($"🧾 <b>Fatura #{inv.Id}</b>\n━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📋 <b>Ad:</b> {H(inv.OrderName)}");
        sb.AppendLine($"🔢 <b>Sipariş No:</b> <code>{inv.OrderNumber}</code>");
        sb.AppendLine($"👤 <b>Müşteri:</b> {H(cname)}");
        if (!string.IsNullOrEmpty(inv.Customer?.CompanyName)) sb.AppendLine($"🏢 <b>Şirket:</b> {H(inv.Customer.CompanyName)}");
        sb.AppendLine($"📧 <b>E-posta:</b> {inv.Email}\n━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"💰 <b>Tutar:</b> {sym}{inv.SourceAmount:N2} {inv.SourceCurrency}");
        sb.AppendLine($"🪙 <b>Kripto:</b> {inv.Currency}");
        sb.AppendLine($"{se} <b>Durum:</b> {sl}");
        sb.AppendLine($"📅 <b>Tarih:</b> {inv.CreatedDate:dd.MM.yyyy HH:mm}");
        if (inv.InvoiceItems.Any()) {
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━\n📦 <b>Kalemler:</b>");
            foreach (var item in inv.InvoiceItems)
                sb.AppendLine($"  • {H(item.ServiceName)} × {item.Quantity} = {sym}{item.Total:N2}");
        }
        if (!string.IsNullOrEmpty(inv.TxnId)) sb.AppendLine($"\n🔑 <b>TxnId:</b> <code>{inv.TxnId}</code>");
        var baseUrl = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "").TrimEnd('/');
        var rows = new List<JsonArray>();
        if (!string.IsNullOrEmpty(inv.TxnId) && !string.IsNullOrEmpty(baseUrl))
            rows.Add(Row(BtnUrl("🔗 Ödeme Sayfası",$"{baseUrl}/pay/{inv.Id}?txnId={Uri.EscapeDataString(inv.TxnId)}")));
        var backCb = fromOverdue ? "overdue_list" : $"inv_list:{inv.Status??"pending"}";
        rows.Add(Row(Btn("◀ Listeye Dön", backCb), Btn("🏠 Menü","menu")));
        await SendMsgKb(c, sb.ToString(), Kb(rows.ToArray()));
    }

    /// <summary>Yinelenen faturalarda bu ay ödenmemiş ve vadesi geçmiş olanları listeler (OverdueInvoices ile aynı mantık).</summary>
    private async Task OverdueList(long c, AppDbContext db) {
        var now = DateTime.UtcNow;
        var y = now.Year; var m = now.Month;
        var monthStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var today = now.Day;
        var isCurrentMonth = (y == now.Year && m == now.Month);
        var isPastMonth = (y < now.Year || (y == now.Year && m < now.Month));

        var recurring = await db.Invoices.Include(i => i.Customer)
            .Where(x => x.RegistrationStatus && x.IsRecurring && x.CreatedDate < monthEnd)
            .Select(x => new { x.Id, x.OrderName, x.SourceAmount, x.SourceCurrency, x.RecurringDay, x.CreatedDate, x.Status, x.Customer })
            .ToListAsync();

        var paidThisMonth = await db.PaymentLinks
            .Where(p => (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && ((p.PaidForMonth == m && p.PaidForYear == y)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        var cancelledForMonth = await db.PaymentLinks
            .Where(p => p.Status == "cancelled"
                && ((p.PaidForMonth == m && p.PaidForYear == y)
                    || (p.PaidForMonth == null && (p.PaidDate ?? p.CreatedDate) >= monthStart && (p.PaidDate ?? p.CreatedDate) < monthEnd)))
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();

        var paidViaInvoice = recurring
            .Where(r => !paidThisMonth.Contains(r.Id) && !cancelledForMonth.Contains(r.Id)
                && (r.Status == "completed" || r.Status == "mismatch")
                && r.CreatedDate >= monthStart && r.CreatedDate < monthEnd)
            .Select(r => r.Id)
            .ToList();
        var allPaidIds = paidThisMonth.Union(paidViaInvoice).Distinct().ToList();

        var unpaid = recurring.Where(r => !allPaidIds.Contains(r.Id)).ToList();
        var overdue = unpaid.Where(r => {
            var dueDay = r.RecurringDay ?? 1;
            return isPastMonth || (isCurrentMonth && dueDay < today);
        }).Take(10).ToList();

        var bkb = Kb(Row(Btn("🏠 Ana Menü","menu")));
        if (!overdue.Any()) { await SendMsgKb(c,"⚠️ <b>Gecikmiş Faturalar</b>\n\n✅ Bu ay için gecikmiş fatura yok.",bkb); return; }
        var sb = new StringBuilder();
        sb.AppendLine($"⚠️ <b>Gecikmiş Yinelenen Faturalar</b>\n<i>{monthStart:MMMM yyyy} — Son {overdue.Count} kayıt</i>\n");
        var rows = new List<JsonArray>();
        foreach (var r in overdue) {
            var sym = CS(r.SourceCurrency);
            var cust = r.Customer != null ? $"{r.Customer.FirstName} {r.Customer.LastName}" : "—";
            sb.AppendLine($"• <b>{H(r.OrderName)}</b> — {sym}{r.SourceAmount:N2}");
            sb.AppendLine($"  👤 {H(cust)}");
            rows.Add(Row(Btn($"#{r.Id} {T(r.OrderName,22)}",$"inv_detail:{r.Id}:overdue")));
        }
        rows.Add(Row(Btn("🏠 Ana Menü","menu")));
        await SendMsgKb(c, sb.ToString(), Kb(rows.ToArray()));
    }

    private async Task RecList(long c, AppDbContext db) {
        var items = await db.Invoices.Where(i=>i.RegistrationStatus && i.IsRecurring && i.RecurringDay.HasValue)
            .Include(i=>i.Customer).OrderByDescending(i=>i.CreatedDate).Take(10).ToListAsync();
        var bkb = Kb(Row(Btn("🏠 Ana Menü","menu")));
        if (!items.Any()) { await SendMsgKb(c,"🔄 <b>Yinelenen Faturalar</b>\n\n📭 Henüz yinelenen fatura yok.",bkb); return; }
        var sb = new StringBuilder();
        sb.AppendLine($"🔄 <b>Yinelenen Faturalar</b>\n<i>Son {items.Count} fatura</i>\n");
        var rows = new List<JsonArray>();
        foreach (var r in items) {
            var sym = CS(r.SourceCurrency);
            var cust = r.Customer!=null ? $"{r.Customer.FirstName} {r.Customer.LastName}" : "—";
            sb.AppendLine($"🟢 <b>{H(r.OrderName)}</b> — {sym}{r.SourceAmount:N2}");
            sb.AppendLine($"  👤 {H(cust)} | Her ayın {r.RecurringDay}. günü");
            rows.Add(Row(Btn($"#{r.Id} {T(r.OrderName,22)}",$"inv_detail:{r.Id}")));
        }
        rows.Add(Row(Btn("🏠 Ana Menü","menu")));
        await SendMsgKb(c, sb.ToString(), Kb(rows.ToArray()));
    }

    private async Task CustList(long c, AppDbContext db) {
        var list = await db.Customers.OrderByDescending(x=>x.Id).Take(10).ToListAsync();
        var bkb = Kb(Row(Btn("🏠 Ana Menü","menu")));
        if (!list.Any()) { await SendMsgKb(c,"👥 <b>Müşteriler</b>\n\n📭 Müşteri bulunamadı.",bkb); return; }
        var sb = new StringBuilder();
        sb.AppendLine($"👥 <b>Müşteriler</b>\n<i>Son {list.Count} müşteri</i>\n");
        var rows = new List<JsonArray>();
        foreach (var x in list) {
            var name = $"{x.FirstName} {x.LastName}";
            sb.AppendLine($"• <b>{H(name)}</b>{(!string.IsNullOrEmpty(x.CompanyName)?$" — {H(x.CompanyName)}":"")}");
            sb.AppendLine($"  📧 {x.Email??"—"}");
            rows.Add(Row(Btn($"👤 {T(name,25)}",$"cust_detail:{x.Id}")));
        }
        rows.Add(Row(Btn("🏠 Ana Menü","menu")));
        await SendMsgKb(c, sb.ToString(), Kb(rows.ToArray()));
    }

    private async Task CustDetail(long c, int id, AppDbContext db) {
        var x = await db.Customers.FindAsync(id);
        if (x==null) { await SendMessageAsync(c,"❌ Müşteri bulunamadı."); return; }
        var inv   = await db.Invoices.Where(i=>i.RegistrationStatus && i.CustomerId==id).CountAsync();
        var paid  = await db.Invoices.Where(i=>i.RegistrationStatus && i.CustomerId==id && (i.Status=="completed"||i.Status=="mismatch")).CountAsync();
        var spent = await db.Invoices.Where(i=>i.RegistrationStatus && i.CustomerId==id && (i.Status=="completed"||i.Status=="mismatch")).SumAsync(i=>(decimal?)i.SourceAmount)??0;
        var rec   = await db.Invoices.Where(i=>i.RegistrationStatus && i.IsRecurring && i.RecurringDay.HasValue && i.CustomerId==id).CountAsync();
        var sb = new StringBuilder();
        sb.AppendLine($"👤 <b>Müşteri #{x.Id}</b>\n━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"👤 <b>Ad Soyad:</b> {H(x.FirstName)} {H(x.LastName)}");
        if (!string.IsNullOrEmpty(x.CompanyName)) sb.AppendLine($"🏢 <b>Şirket:</b> {H(x.CompanyName)}");
        sb.AppendLine($"📧 <b>E-posta:</b> {x.Email??"—"}");
        if (!string.IsNullOrEmpty(x.Phone))    sb.AppendLine($"📞 <b>Telefon:</b> {x.Phone}");
        if (!string.IsNullOrEmpty(x.Telegram)) sb.AppendLine($"✈️ <b>Telegram:</b> @{x.Telegram}");
        sb.AppendLine($"━━━━━━━━━━━━━━━━━━━━\n📋 <b>Toplam Fatura:</b> {inv}");
        sb.AppendLine($"✅ <b>Ödenmiş:</b> {paid}");
        sb.AppendLine($"💰 <b>Toplam Harcama:</b> ${spent:N2}");
        sb.AppendLine($"🔄 <b>Yinelenen Fatura:</b> {rec}");
        await SendMsgKb(c, sb.ToString(), Kb(Row(Btn("◀ Müşterilere Dön","customers"), Btn("🏠 Menü","menu"))));
    }

    private async Task Stats(long c, AppDbContext db) {
        var total   = await db.Invoices.Where(i=>i.RegistrationStatus).CountAsync();
        var paid    = await db.Invoices.Where(i=>i.RegistrationStatus&&(i.Status=="completed"||i.Status=="mismatch")).CountAsync();
        var pend    = await db.Invoices.Where(i=>i.RegistrationStatus&&i.Status=="pending").CountAsync();
        var exp     = await db.Invoices.Where(i=>i.RegistrationStatus&&i.Status=="expired").CountAsync();
        var can     = await db.Invoices.Where(i=>i.RegistrationStatus&&i.Status=="cancelled").CountAsync();
        var rev     = await db.Invoices.Where(i=>i.RegistrationStatus&&(i.Status=="completed"||i.Status=="mismatch")).SumAsync(i=>(decimal?)i.SourceAmount)??0;
        var arec    = await db.Invoices.Where(i=>i.RegistrationStatus && i.IsRecurring && i.RecurringDay.HasValue).CountAsync();
        var custs   = await db.Customers.CountAsync();
        var today   = DateTime.UtcNow.Date;
        var todayEnd = today.AddDays(1);
        var tpaidIds = await db.PaymentLinks
            .Where(p => (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && (p.PaidDate ?? p.CreatedDate) >= today && (p.PaidDate ?? p.CreatedDate) < todayEnd)
            .Select(p => p.InvoiceId)
            .Distinct()
            .CountAsync();
        var paidTodayViaLink = await db.PaymentLinks
            .Where(p => (p.Status == "completed" || p.Status == "mismatch" || p.Status == "manual")
                && (p.PaidDate ?? p.CreatedDate) >= today && (p.PaidDate ?? p.CreatedDate) < todayEnd)
            .Select(p => p.InvoiceId)
            .Distinct()
            .ToListAsync();
        var tpaidInv = paidTodayViaLink.Count == 0
            ? await db.Invoices.CountAsync(i => i.RegistrationStatus && (i.Status == "completed" || i.Status == "mismatch") && i.CreatedDate >= today)
            : await db.Invoices.CountAsync(i => i.RegistrationStatus && (i.Status == "completed" || i.Status == "mismatch") && i.CreatedDate >= today && !paidTodayViaLink.Contains(i.Id));
        var tpaid = tpaidIds + tpaidInv;
        var sb = new StringBuilder();
        sb.AppendLine($"📊 <b>Sistem İstatistikleri</b>\n<i>{DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC</i>\n━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📋 <b>Toplam Fatura:</b> {total}");
        sb.AppendLine($"✅ <b>Ödenmiş:</b> {paid}");
        sb.AppendLine($"⏳ <b>Bekleyen:</b> {pend}");
        sb.AppendLine($"❌ <b>Süresi Dolan:</b> {exp}");
        sb.AppendLine($"🚫 <b>İptal:</b> {can}\n━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"💰 <b>Toplam Gelir:</b> ${rev:N2}");
        sb.AppendLine($"📅 <b>Bugün Ödenen:</b> {tpaid}\n━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"🔄 <b>Yinelenen Fatura:</b> {arec}");
        sb.AppendLine($"👥 <b>Toplam Müşteri:</b> {custs}");
        var kb = Kb(Row(Btn("✅ Ödenmiş","inv_list:completed"), Btn("⏳ Bekleyen","inv_list:pending")), Row(Btn("🏠 Ana Menü","menu")));
        await SendMsgKb(c, sb.ToString(), kb);
    }

    private async Task Recent(long c, AppDbContext db) {
        var list = await db.Invoices.Where(i=>i.RegistrationStatus).Include(i=>i.Customer)
            .OrderByDescending(i=>i.CreatedDate).Take(8).ToListAsync();
        var bkb = Kb(Row(Btn("🏠 Ana Menü","menu")));
        if (!list.Any()) { await SendMsgKb(c,"🕐 <b>Son Faturalar</b>\n\n📭 Kayıt yok.",bkb); return; }
        var sb = new StringBuilder();
        sb.AppendLine("🕐 <b>Son Faturalar</b>\n");
        var rows = new List<JsonArray>();
        foreach (var inv in list) {
            var sym = CS(inv.SourceCurrency);
            var (_,em) = SL(inv.Status??"");
            var name = inv.Customer!=null ? $"{inv.Customer.FirstName} {inv.Customer.LastName}" : inv.Email;
            sb.AppendLine($"{em} <b>{H(inv.OrderName)}</b> — {sym}{inv.SourceAmount:N2}");
            sb.AppendLine($"  👤 {H(name)} | 📅 {inv.CreatedDate:dd.MM.yyyy HH:mm}");
            rows.Add(Row(Btn($"#{inv.Id} {T(inv.OrderName,22)}",$"inv_detail:{inv.Id}")));
        }
        rows.Add(Row(Btn("🏠 Ana Menü","menu")));
        await SendMsgKb(c, sb.ToString(), Kb(rows.ToArray()));
    }

    private static (string l, string e) SL(string s) => s switch {
        "completed" => ("Ödenmiş","✅"), "mismatch" => ("Fazla Ödendi","✅"),
        "pending"   => ("Bekliyor","⏳"), "new"      => ("Yeni","🆕"),
        "expired"   => ("Süresi Doldu","❌"), "cancelled" => ("İptal","🚫"),
        _ => ("Bilinmiyor","❓")
    };
    private static string CS(string? s) => (s??"").ToUpper() switch {
        "USD"=>"$", "EURO"=>"€", "EUR"=>"€", "TL"=>"₺", "TRY"=>"₺", _=>""
    };
    private static string PB(int p) {
        var f=(int)Math.Round(p/10.0);
        return new string(Enumerable.Repeat("█"[0],f).Concat(Enumerable.Repeat("░"[0],10-f)).ToArray())+" "+p+"%";
    }
    private static string T(string? s, int m) => string.IsNullOrEmpty(s)?"":(s.Length<=m?s:s[..m]+"…");
    private static string H(string? s) => string.IsNullOrEmpty(s)?"":s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
}
