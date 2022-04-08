using Microsoft.AspNetCore.Mvc;

namespace OAuth.Homework.Controllers
{
    public class ErrorController : Controller
    {
        public IActionResult Accessdenied()
        {
            return View();
        }
    }
}
