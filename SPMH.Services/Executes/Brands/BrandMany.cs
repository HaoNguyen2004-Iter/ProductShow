using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.DBContext.Entities;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Brands
{
    public class BrandMany
    {
        private readonly AppDbContext _db;
        public BrandMany(AppDbContext db) => _db = db;

        public async Task<List<BrandModel>> GetAllBrandAsync()
        {
            return await _db.Brands
                .AsNoTracking()
                .OrderBy(b => b.Name)
                .Select(b => new BrandModel { Name = b.Name })
                .ToListAsync();
        }
    }
}