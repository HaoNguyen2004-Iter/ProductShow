using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Accounts
{
    public class AccountOne
    {
        private readonly AppDbContext _db;
        public AccountOne(AppDbContext db) => _db = db;

        public async Task<bool> Login(AccountModel account)
        {

            BadInput.EnsureSafe(account.Username);
            BadInput.EnsureSafe(account.Password);

            var hasAccount = await _db.Accounts.AsNoTracking()
                .Where(p => p.Username == account.Username && p.Password == account.Password)
                .AnyAsync();

            if (!hasAccount)
                throw new ArgumentException("Tài khoản không tồn tại");
            return true;
        }
    }
}
