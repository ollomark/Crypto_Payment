using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("role-claims")]
public class RoleClaimController : Controller
{
    // GET
    [HttpGet]
    public IActionResult RoleClaimList()
    {
        return View();
    }
}