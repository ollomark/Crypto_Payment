using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("payment-methods")]
public class PaymentMethodPageController : Controller
{
    [HttpGet]
    public IActionResult Index() => View("~/Views/PaymentMethod/Index.cshtml");
}

[ApiController]
[Authorize]
[Route("/api/payment-methods")]
public class PaymentMethodsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentMethodsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.PaymentMethods
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Select(m => new {
                m.Id, m.Name, m.Type, m.Description, m.Icon,
                m.BankName, m.AccountHolder, m.Iban,
                m.WalletAddress, m.WalletNetwork, m.ExtraConfig,
                m.IsActive, m.IsDefault, m.SortOrder, m.CreatedDate
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var list = await _db.PaymentMethods
            .Where(m => m.IsActive)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Name)
            .Select(m => new {
                m.Id, m.Name, m.Type, m.Description, m.Icon,
                m.BankName, m.AccountHolder, m.Iban,
                m.WalletAddress, m.WalletNetwork,
                m.IsDefault
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var m = await _db.PaymentMethods.FindAsync(id);
        if (m == null) return NotFound();
        return Ok(m);
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PaymentMethodRequest req)
    {
        var pm = new PaymentMethod
        {
            Name = req.Name,
            Type = req.Type ?? "manual",
            Description = req.Description,
            Icon = req.Icon,
            BankName = req.BankName,
            AccountHolder = req.AccountHolder,
            Iban = req.Iban,
            WalletAddress = req.WalletAddress,
            WalletNetwork = req.WalletNetwork,
            ApiKey = req.ApiKey,
            ExtraConfig = req.ExtraConfig,
            IsActive = req.IsActive ?? true,
            IsDefault = req.IsDefault ?? false,
            SortOrder = req.SortOrder ?? 0
        };

        if (pm.IsDefault)
            await ClearDefaults();

        _db.PaymentMethods.Add(pm);
        await _db.SaveChangesAsync();
        return Ok(pm);
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PaymentMethodRequest req)
    {
        var pm = await _db.PaymentMethods.FindAsync(id);
        if (pm == null) return NotFound();

        pm.Name = req.Name ?? pm.Name;
        pm.Type = req.Type ?? pm.Type;
        pm.Description = req.Description ?? pm.Description;
        pm.Icon = req.Icon ?? pm.Icon;
        pm.BankName = req.BankName;
        pm.AccountHolder = req.AccountHolder;
        pm.Iban = req.Iban;
        pm.WalletAddress = req.WalletAddress;
        pm.WalletNetwork = req.WalletNetwork;
        if (req.ApiKey != null) pm.ApiKey = req.ApiKey;
        pm.ExtraConfig = req.ExtraConfig ?? pm.ExtraConfig;
        if (req.IsActive.HasValue) pm.IsActive = req.IsActive.Value;
        if (req.SortOrder.HasValue) pm.SortOrder = req.SortOrder.Value;

        if (req.IsDefault == true)
        {
            await ClearDefaults();
            pm.IsDefault = true;
        }
        else if (req.IsDefault == false)
        {
            pm.IsDefault = false;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Güncellendi." });
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var pm = await _db.PaymentMethods.FindAsync(id);
        if (pm == null) return NotFound();
        _db.PaymentMethods.Remove(pm);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Silindi." });
    }

    [Authorize(Roles = "MasterAdmin")]
    [HttpPost("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var pm = await _db.PaymentMethods.FindAsync(id);
        if (pm == null) return NotFound();
        pm.IsActive = !pm.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { isActive = pm.IsActive });
    }

    private async Task ClearDefaults()
    {
        var defaults = await _db.PaymentMethods.Where(m => m.IsDefault).ToListAsync();
        foreach (var d in defaults) d.IsDefault = false;
    }
}

public record PaymentMethodRequest(
    string? Name, string? Type, string? Description, string? Icon,
    string? BankName, string? AccountHolder, string? Iban,
    string? WalletAddress, string? WalletNetwork,
    string? ApiKey, string? ExtraConfig,
    bool? IsActive, bool? IsDefault, int? SortOrder
);
