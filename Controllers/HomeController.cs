using Capableza.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CapablezaWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("admin")) return RedirectToAction("Index", "Admin");
                if (User.IsInRole("employee")) return RedirectToAction("Dashboard", "Employee");
            }
            return View();
        }

        public IActionResult About()
        {
            ViewData["Title"] = "About the NT Skills Portal";
            return View();
        }

        public IActionResult Help()
        {
            ViewData["Title"] = "Help & FAQ";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Title"] = "Contact Us";
            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy Policy";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => 
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}