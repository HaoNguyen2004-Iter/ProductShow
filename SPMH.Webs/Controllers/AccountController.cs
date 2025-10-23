using System.Threading.Tasks;
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
        public IActionResult Login()
        {
            return View();
        }

        public async Task<IActionResult> Login(AccountModel account)
        {
            if (String.IsNullOrEmpty(account.Username) || String.IsNullOrEmpty(account.Password))
                return BadRequest("Username hoặc Password không được trống!");
            var hasAccount = await _accountOne.Login(account);
            if (!hasAccount)
                return BadRequest("Tài khoản hoặc mật khẩu không đúng. Vui lòng kiểm tra lại!");
            return RedirectToAction("Index", "Product");
        }
    }
}
