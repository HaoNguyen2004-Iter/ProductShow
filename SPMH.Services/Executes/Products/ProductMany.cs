using System.Linq;
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
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 5;

            var baseQuery = _db.Products
                .AsNoTracking()
                .Where(p => p.Status >= 0);

            if (filter is not null)
            {
                baseQuery = ApplyFilterWithoutKeyword(baseQuery, filter);
            }

            string? term = null;
            if (!string.IsNullOrWhiteSpace(filter?.Name)) term = filter!.Name.Trim();
            else if (!string.IsNullOrWhiteSpace(filter?.Code)) term = filter!.Code.Trim();

            if (string.IsNullOrEmpty(term))
            {
                var total0 = await baseQuery.CountAsync();

                var ordered = (filter?.Price.HasValue == true)
                    ? baseQuery.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                    : baseQuery.OrderByDescending(p => p.Id);

                var items0 = await ordered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
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
                    .ToListAsync();

                return new PagedResult<ProductModel>(items0, total0, page, pageSize);
            }

            var pattern = $"%{term}%";
            var nameQuery = baseQuery.Where(p => EF.Functions.Like(p.Name, pattern));
            var codeQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Code, pattern) &&
               !EF.Functions.Like(p.Name, pattern));

            var nameCount = await nameQuery.CountAsync();
            var codeCount = await codeQuery.CountAsync();
            var total = nameCount + codeCount;

            var skip = (page - 1) * pageSize;

            var orderedName = (filter?.Price.HasValue == true)
                ? nameQuery.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                : nameQuery.OrderByDescending(p => p.Id);

            var takeFromName = 0;
            List<ProductModel> namePart = new();
            if (skip < nameCount)
            {
                takeFromName = Math.Min(pageSize, nameCount - skip);
                namePart = await orderedName
                    .Skip(skip)
                    .Take(takeFromName)
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
                    .ToListAsync();
            }

            var remaining = pageSize - takeFromName;
            List<ProductModel> codePart = new();
            if (remaining > 0)
            {
                var skipCode = Math.Max(0, skip - nameCount);

                var orderedCode = (filter?.Price.HasValue == true)
                    ? codeQuery.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                    : codeQuery.OrderByDescending(p => p.Id);

                codePart = await orderedCode
                    .Skip(skipCode)
                    .Take(remaining)
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
                    .ToListAsync();
            }

            var items = namePart.Concat(codePart).ToList();
            return new PagedResult<ProductModel>(items, total, page, pageSize);
        }

        private static IQueryable<Product> ApplyFilterWithoutKeyword(IQueryable<Product> query, ProductFilter f)
        {
            if (!string.IsNullOrWhiteSpace(f.Brand))
            {
                var brand = f.Brand.Trim();
                query = query.Where(p => EF.Functions.Like(p.Brand.Name, brand));
            }

            if (f.Price.HasValue)
                query = query.Where(p => p.PriceVnd <= f.Price.Value);

            if (f.Stock.HasValue)
                query = query.Where(p => p.Stock == f.Stock.Value);

            if (f.Status.HasValue)
                query = query.Where(p => p.Status == f.Status.Value);

            return query;
        }
    }
}