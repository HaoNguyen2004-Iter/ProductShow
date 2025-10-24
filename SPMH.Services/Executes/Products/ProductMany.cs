using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.DBContext.Entities;
using SPMH.Services.Models;


namespace SPMH.Services.Executes.Products
{
    public class ProductMany
    {
        private readonly AppDbContext _db;
        public ProductMany(AppDbContext db) => _db = db;

        public async Task<PagedResult<ProductModel>> GetPagedAsync(int page, int pageSize, ProductFilter? filter)
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);

            ValidateFilter(filter);

            var query = _db.Products.AsNoTracking().Where(p => p.Status >= 0);
            query = ApplyFilterWithoutKeyword(query, filter);

            string? term = filter?.Name?.Trim() ?? filter?.Code?.Trim();
            if (string.IsNullOrWhiteSpace(term))
                return await GetPagedResultAsync(query, page, pageSize, filter?.Price.HasValue);

            var pattern = $"%{term}%";

            var nameQuery = query.Where(p => EF.Functions.Like(p.Name, pattern));
            var codeQuery = query.Where(p => EF.Functions.Like(p.Code, pattern) && !EF.Functions.Like(p.Name, pattern));

            var nameCount = await nameQuery.CountAsync();
            var codeCount = await codeQuery.CountAsync();
            var total = nameCount + codeCount;
            var skip = (page - 1) * pageSize;

            var items = new List<ProductModel>();

            // Lấy từ Name
            if (skip < nameCount)
            {
                var take = Math.Min(pageSize, nameCount - skip);
                items.AddRange(await GetOrdered(nameQuery, filter?.Price.HasValue)
                    .Skip(skip).Take(take)
                    .Select(ToModel).ToListAsync());
            }

            // Lấy từ Code nếu còn chỗ
            var remaining = pageSize - items.Count;
            if (remaining > 0 && skip < total)
            {
                var skipCode = Math.Max(0, skip - nameCount);
                items.AddRange(await GetOrdered(codeQuery, filter?.Price.HasValue)
                    .Skip(skipCode).Take(remaining)
                    .Select(ToModel).ToListAsync());
            }

            return new PagedResult<ProductModel>(items, total, page, pageSize);
        }

        private static IQueryable<Product> ApplyFilterWithoutKeyword(IQueryable<Product> q, ProductFilter f)
        {
            if (!string.IsNullOrWhiteSpace(f.Brand))
                q = q.Where(p => EF.Functions.Like(p.Brand.Name, $"%{f.Brand.Trim()}%"));

            if (f.Price.HasValue) q = q.Where(p => p.PriceVnd <= f.Price.Value);
            if (f.Stock.HasValue) q = q.Where(p => p.Stock == f.Stock.Value);
            if (f.Status.HasValue) q = q.Where(p => p.Status == f.Status.Value);

            return q;
        }

        private static IQueryable<Product> GetOrdered(IQueryable<Product> q, bool? sortByPrice)
            => sortByPrice == true
                ? q.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                : q.OrderByDescending(p => p.Id);

        private static readonly Expression<Func<Product, ProductModel>> ToModel = p => new(
            p.Id, p.Code, p.Name, p.BrandId, p.Brand.Name, p.PriceVnd, p.Stock, p.Status,
            p.Description, p.Url ?? string.Empty, p.CreateBy, p.CreateDate, p.UpdateBy, p.LastUpdateDay);

        private async Task<PagedResult<ProductModel>> GetPagedResultAsync(
            IQueryable<Product> query, int page, int pageSize, bool? sortByPrice)
        {
            var total = await query.CountAsync();
            var items = await GetOrdered(query, sortByPrice)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToModel)
                .ToListAsync();

            return new PagedResult<ProductModel>(items, total, page, pageSize);
        }

        private static void ValidateFilter(ProductFilter? f)
        {
            if (f == null) return;
            if (!string.IsNullOrEmpty(f.Name) && BadInput.hasBadInput(f.Name)) throw new ArgumentException("Tên không hợp lệ");
            if (!string.IsNullOrEmpty(f.Brand) && BadInput.hasBadInput(f.Brand)) throw new ArgumentException("Thương hiệu không hợp lệ");
            if (!string.IsNullOrEmpty(f.Code) && BadInput.hasBadInput(f.Code)) throw new ArgumentException("Mã không hợp lệ");
        }
    }
}