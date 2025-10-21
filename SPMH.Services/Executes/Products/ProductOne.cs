using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Products
{
    public class ProductOne
    {
        private readonly AppDbContext _db;
        public ProductOne(AppDbContext db) => _db = db;

        public async Task<ProductModel> GetProductByIdAsync(int productId)
        {
            var product = await _db.Products.AsNoTracking()
                .Where(p => p.Status >= 0 && p.Id == productId)
                .Select(p => new ProductModel(
                    p.Id,
                    p.Code,
                    p.Name,
                    p.BrandId,
                    p.Brand.Name,
                    p.PriceVnd,
                    p.Stock,
                    p.Status,
                    p.Description,
                    p.Url ?? string.Empty))
                .FirstOrDefaultAsync();

            if (product == null)
                throw new ArgumentException("Sản phẩm không tồn tại");
            return product;
        }
    }
}