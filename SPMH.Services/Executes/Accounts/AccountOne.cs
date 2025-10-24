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

            if (BadInput.hasBadInput(account.Username)) throw new ArgumentException("Đầu vào không hợp lệ");
            if (BadInput.hasBadInput(account.Password)) throw new ArgumentException("Đầu vào không hợp lệ");

            var hasAccount = await _db.Accounts.AsNoTracking()
                .Where(p => p.Username == account.Username && p.Password == account.Password)
                .AnyAsync();

            if (!hasAccount)
                return false;
            return true;
        }
    }
}
