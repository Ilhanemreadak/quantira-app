using Microsoft.AspNetCore.Mvc;

namespace Quantira.WebAPI.Controllers
{
    public class AssetsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
