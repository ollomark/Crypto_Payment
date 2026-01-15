using Crypto_Payment.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("invoices")]
public class InvoiceController : Controller
{
    // GET
    public IActionResult InvoiceList()
    {
        return View();
    }
    
    [HttpGet("invoice-add")]
    public IActionResult InvoiceAdd()
    {
        return View();
    }
    
    [HttpGet("invoice-detail")]
    public IActionResult InvoiceDetail()
    {
        return View();
    }
}