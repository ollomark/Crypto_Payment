using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
[Route("roles")]
public class RoleController : Controller
{
    // GET
    [HttpGet]
    public IActionResult RoleList()
    {
        return View();
    }
}