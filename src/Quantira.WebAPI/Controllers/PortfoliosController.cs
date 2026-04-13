using Microsoft.AspNetCore.Mvc;

namespace Quantira.WebAPI.Controllers
{
    public class PortfoliosController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
