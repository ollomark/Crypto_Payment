using Crypto_Payment.Models;
using Crypto_Payment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("invoices")]
public class InvoiceController : Controller
{
    private readonly IInvoiceService _invoiceService;
    public InvoiceController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    
    // GET
    [HttpGet]
    public async Task<IActionResult> InvoiceList()
    {
        var invoices = await _invoiceService.GetTotalInvoiceByStatusAsync();

        ViewBag.PaidInvoices      = invoices.Where(x => x.Status == InvoiceStatus.Completed).FirstOrDefault();
        ViewBag.UnpaidInvoices    = invoices.Where(x => x.Status == InvoiceStatus.Expired).FirstOrDefault();
        ViewBag.SendInvoices      = invoices.Where(x => x.Status == InvoiceStatus.New || x.Status == InvoiceStatus.Pending).FirstOrDefault();
        ViewBag.CancelledInvoices = invoices.Where(x => x.Status == InvoiceStatus.Cancelled).FirstOrDefault();
        return View();
    }
    
    [HttpGet("invoice-add")]
    public IActionResult InvoiceAdd()
    {
        return View();
    }
    
    [HttpGet("invoice-detail/{id}")]
    public async Task<IActionResult> InvoiceDetail(int id)
    {
        var result = await _invoiceService.GetByIdAsync(id);
        if (result == null)
            return RedirectToAction(nameof(InvoiceList));
        return View(result);
    }

    [HttpGet("invoice-edit/{id}")]
    public async Task<IActionResult> InvoiceEdit(int id)
    {
        var result = await _invoiceService.GetByIdAsync(id);
        if (result == null)
            return RedirectToAction(nameof(InvoiceList));
        return View(result);
    }
}