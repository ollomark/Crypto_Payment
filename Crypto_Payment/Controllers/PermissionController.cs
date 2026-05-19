using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("permissions")]
public class PermissionController : Controller
{
    [HttpGet]
    public IActionResult PermissionList()
    {
        return View();
    }
}