using Microsoft.AspNetCore.Mvc;

namespace Road_Route_Helper.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
