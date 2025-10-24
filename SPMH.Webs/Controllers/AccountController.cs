using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SPMH.DBContext.Entities;
using SPMH.Services.Executes;
using SPMH.Services.Executes.Accounts;
using SPMH.Services.Executes.Brands;
using SPMH.Services.Executes.Products;
using SPMH.Services.Executes.Storage;
using SPMH.Services.Models;

namespace SPMH.Webs.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountOne _accountOne;
        public AccountController(AccountOne accountOne)
        {
            _accountOne = accountOne;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromForm] AccountModel account)
        {
            if (string.IsNullOrEmpty(account.Username) || string.IsNullOrEmpty(account.Password))
                return BadRequest("Username hoặc Password không được trống!");

            var acc = await _accountOne.Login(account);
            if (acc == null)
                return BadRequest("Tài khoản hoặc mật khẩu không đúng. Vui lòng kiểm tra lại!");

            // Build claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, acc.Id.ToString()),
                new Claim(ClaimTypes.Name, acc.Username)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest");
            if (isAjax)
                return Ok(new { ok = true, redirect = Url.Action("Index", "Product") ?? "/Product/Index" });

            return RedirectToAction("Index", "Product");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
