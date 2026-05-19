using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("customers")]
public class CustomerController : Controller
{
    // GET
    [HttpGet]
    public IActionResult CustomerList()
    {
        return View();
    }
}