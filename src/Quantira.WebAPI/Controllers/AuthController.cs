using Microsoft.AspNetCore.Mvc;

namespace Quantira.WebAPI.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
