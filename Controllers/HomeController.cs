using Microsoft.AspNetCore.Mvc;
using OAuth.Homework.Models;
using System.Diagnostics;

namespace OAuth.Homework.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SubscriberContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HomeController(ILogger<HomeController> logger, IHttpContextAccessor httpContextAccessor, SubscriberContext db)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }


        public IActionResult Index()
        {
            var userId = _httpContextAccessor.HttpContext.User.Identity.Name;
            if (!string.IsNullOrEmpty(userId))
            {
                var profile = _db.Subscribers.FirstOrDefault(p => p.Id.ToString() == userId);
                if (profile is null)
                {
                    return RedirectToAction("SignOutLine", "LINELogin");
                }

                return View(profile);
            }
            else
            {
                return View();
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}