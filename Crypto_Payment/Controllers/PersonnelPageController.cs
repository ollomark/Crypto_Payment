using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize(Roles = "MasterAdmin")]
[Route("")]
public class PersonnelPageController : Controller
{
    [HttpGet("personnel")]
    public IActionResult Index() => View();
}
