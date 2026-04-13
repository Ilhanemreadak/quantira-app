using Microsoft.AspNetCore.Mvc;

namespace Quantira.WebAPI.Controllers
{
    public class MarketDataController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
