using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize(Roles = "MasterAdmin")]
[Route("api-clients")]
public class ApiClientPageController : Controller
{
    [HttpGet]
    public IActionResult Index() => View("~/Views/ApiClient/Index.cshtml");
}
