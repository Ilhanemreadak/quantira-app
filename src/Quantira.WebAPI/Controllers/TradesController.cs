using Microsoft.AspNetCore.Mvc;

namespace Quantira.WebAPI.Controllers
{
    public class TradesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
