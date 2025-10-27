using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Accounts
{
    public class AccountOne
    {
        private readonly AppDbContext _db;
        public AccountOne(AppDbContext db) => _db = db;

        public async Task<AccountModel?> Login(AccountModel account)
        {
            if (SqlGuard.IsSuspicious(account))
                throw new ArgumentException("Đầu vào đáng ngờ");

            var acc = await _db.Accounts.AsNoTracking()
                .Where(p => p.Username == account.Username && p.Password == account.Password)
                .Select(a => new AccountModel(a.Id, a.Username, a.Password))
                .FirstOrDefaultAsync();

            return acc;
        }
    }
}
