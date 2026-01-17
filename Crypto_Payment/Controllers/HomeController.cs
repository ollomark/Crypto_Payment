using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ICustomerService _customerService;
    private readonly IInvoiceService _invoiceService;

    public HomeController(
        ILogger<HomeController> logger,
        ICustomerService customerService,
        IInvoiceService invoiceService)
    {
        _logger = logger;
        _customerService = customerService;
        _invoiceService = invoiceService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var customers = await _customerService.GetAllAsync();
            var invoices = await _invoiceService.GetAllAsync();
            
            ViewBag.TotalCustomers = customers?.Count() ?? 0;
            ViewBag.TotalInvoices = invoices?.Count() ?? 0;
            ViewBag.TotalInvoiceAmount = invoices?.Sum(i => i.SourceAmount) ?? 0;
            ViewBag.RecentInvoices = invoices?.OrderByDescending(i => i.Id).Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard verileri yüklenirken hata oluştu");
            ViewBag.TotalCustomers = 0;
            ViewBag.TotalInvoices = 0;
            ViewBag.TotalInvoiceAmount = 0;
            ViewBag.RecentInvoices = new List<DTOS.InvoiceDto>();
        }
        
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
