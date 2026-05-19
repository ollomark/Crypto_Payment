using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crypto_Payment.Controllers;

[Authorize]
public class ChatPageController : Controller
{
    [HttpGet("/chat")]
    public IActionResult Index([FromQuery] bool guide = false)
    {
        ViewBag.OnboardingMode = "chat";
        ViewBag.OpenGuideTab = guide;
        return View("~/Views/Chat/Index.cshtml");
    }
}
