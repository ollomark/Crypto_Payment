using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Crypto_Payment.Data;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Crypto_Payment.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ICustomerService _customerService;
    private readonly IInvoiceService _invoiceService;
    private readonly IExpenseService _expenseService;
    private readonly UserManager<User> _userManager;
    private readonly AppDbContext _db;

    public HomeController(
        ILogger<HomeController> logger,
        ICustomerService customerService,
        IInvoiceService invoiceService,
        IExpenseService expenseService,
        UserManager<User> userManager,
        AppDbContext db)
    {
        _logger = logger;
        _customerService = customerService;
        _invoiceService = invoiceService;
        _expenseService = expenseService;
        _userManager = userManager;
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var isMaster = await _userManager.IsInRoleAsync(user, "MasterAdmin");
        var now = DateTime.UtcNow;

        if (isMaster)
        {
            try
            {
                var stats = await _invoiceService.GetMonthlyStatsAsync(now.Year, now.Month);
                var monthlyExpenses = await _expenseService.GetTotalApprovedAsync(now.Year, now.Month);

                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);

                // Ay Sonu Karlılık: ReportTotalsHelper (Finance Report ile aynı, çift sayım yok)
                var (reportTahsil, reportGider) = await ReportTotalsHelper.GetTotalsAsync(_db, _invoiceService, now.Year, now.Month);

                var todayDay = now.Day;

                var todayDueInvoices = await _db.Invoices
                    .Include(i => i.Customer)
                    .Where(i => i.RegistrationStatus && i.IsRecurring && i.RecurringDay == todayDay)
                    .Select(i => new { i.Id, i.OrderName, i.SourceAmount, i.SourceCurrency, i.RecurringDay,
                        CustomerName = i.Customer != null
                            ? (i.Customer.CompanyName ?? (i.Customer.FirstName + " " + i.Customer.LastName))
                            : i.Email })
                    .ToListAsync();

                var todayDuePaidIds = await _db.PaymentLinks
                    .Where(p => (p.Status == "completed" || p.Status == "mismatch")
                        && p.CreatedDate >= monthStart && p.CreatedDate < monthEnd)
                    .Select(p => p.InvoiceId)
                    .Distinct()
                    .ToListAsync();

                var todayDueList = todayDueInvoices.Select(i => new TodayDueInvoice
                {
                    InvoiceId = i.Id,
                    OrderName = i.OrderName,
                    CustomerName = i.CustomerName ?? "",
                    Amount = i.SourceAmount,
                    Currency = i.SourceCurrency,
                    RecurringDay = i.RecurringDay ?? todayDay,
                    IsPaidThisMonth = todayDuePaidIds.Contains(i.Id)
                }).ToList();

                var pendingApprovals = await _db.ApprovalRequests
                    .Where(a => a.Status == "Pending")
                    .OrderByDescending(a => a.CreatedDate)
                    .Take(10)
                    .ToListAsync();

                var pendingExpenses = await _db.Expenses
                    .Where(e => e.Status == "Pending")
                    .ToListAsync();

                var vm = new MasterDashboardViewModel
                {
                    TotalCustomers     = await _customerService.GetTotalCountAsync(),
                    TotalInvoices      = stats.TotalInvoices,
                    TotalAmount        = stats.TotalAmount,
                    PaidAmount         = stats.PaidAmount,
                    PaidCount          = stats.PaidCount,
                    PendingAmount      = stats.PendingAmount,
                    PendingCount       = stats.PendingCount,
                    OverdueAmount      = stats.OverdueAmount,
                    OverdueCount       = stats.OverdueCount,
                    TotalExpenses      = monthlyExpenses,
                    NetKasa            = reportTahsil - reportGider,
                    NetKasaTahsil      = reportTahsil,
                    NetKasaGider       = reportGider,
                    RecurringCount     = stats.RecurringCount,
                    RecurringAmount    = stats.RecurringAmount,
                    CollectionRate     = stats.CollectionRate,
                    UpcomingCount      = stats.UpcomingCount,
                    UpcomingAmount     = stats.UpcomingAmount,
                    CurrentMonth       = now.Month,
                    CurrentYear        = now.Year,
                    RecentInvoices     = await _invoiceService.GetRecentInvoicesAsync(),
                    TodayDueInvoices   = todayDueList,
                    PendingApprovals   = pendingApprovals,
                    PendingExpenseCount = pendingExpenses.Count,
                    PendingExpenseAmount = pendingExpenses.Sum(e => e.Amount)
                };
                return View("MasterDashboard", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MasterAdmin dashboard yüklenirken hata");
                return View("MasterDashboard", new MasterDashboardViewModel());
            }
        }
        else
        {
            try
            {
                var stats = await _invoiceService.GetMonthlyStatsAsync(now.Year, now.Month);
                var vm = new AdminDashboardViewModel
                {
                    TotalInvoices  = stats.TotalInvoices,
                    PendingCount   = stats.PendingCount,
                    TotalCustomers = await _customerService.GetTotalCountAsync(),
                    CurrentMonth   = now.Month,
                    CurrentYear    = now.Year,
                    RecentInvoices = await _invoiceService.GetRecentInvoicesAsync()
                };
                return View("AdminDashboard", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin dashboard yüklenirken hata");
                return View("AdminDashboard", new AdminDashboardViewModel());
            }
        }
    }

    [HttpGet("privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet("error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
