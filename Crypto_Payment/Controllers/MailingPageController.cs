using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize(Roles = "MasterAdmin,Admin")]
[Route("mailing")]
public class MailingPageController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
