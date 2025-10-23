using Microsoft.AspNetCore.Mvc;

namespace SPMH.Webs.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
}
