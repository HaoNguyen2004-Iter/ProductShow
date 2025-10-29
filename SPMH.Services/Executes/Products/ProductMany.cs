// Replace the existing file content with this full file (includes stock range support)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using SPMH.DBContext;
using SPMH.DBContext.Entities;
using SPMH.Services.Models;
using SPMH.Services.Utils;

namespace SPMH.Services.Executes.Products
{
    public class ProductMany
    {
        private readonly AppDbContext _db;
        public ProductMany(AppDbContext db) => _db = db;

        public async Task<PagedResult<ProductModel>> GetPagedAsync(int page, int pageSize, ProductModel? filter)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 5;

            if (filter != null && SqlGuard.IsSuspicious(filter))
                throw new ArgumentException("Đầu vào không hợp lệ");

            var useKeyword = !string.IsNullOrWhiteSpace(filter?.Keyword);
            var query = BuildQuery(filter);

            var hasPriceFilter = filter != null && (
                (filter.PriceFrom ?? 0) > 0 ||
                (filter.PriceTo ?? 0) > 0 ||
                ((filter.PriceFrom ?? 0) == 0 && (filter.PriceTo ?? 0) == 0 && filter.PriceVnd > 0)
            );

            var ordered = hasPriceFilter
                ? query.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                : query.OrderByDescending(p => p.Id);

            var total = await query.CountAsync();
            var items = await ordered
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
                    p.Url ?? string.Empty,
                    p.CreateBy,
                    p.CreateDate,
                    p.UpdateBy,
                    p.LastUpdateDay,
                    p.Keyword))
                .ToListAsync();

            return new PagedResult<ProductModel>(items, total, page, pageSize);
        }

        public async Task<List<ProductModel>> GetAllAsync(ProductModel? filter)
        {
            if (filter != null && SqlGuard.IsSuspicious(filter))
                throw new ArgumentException("Đầu vào không hợp lệ");

            var useKeyword = !string.IsNullOrWhiteSpace(filter?.Keyword);
            var query = BuildQuery(filter);

            var hasPriceFilter = filter != null && (
                (filter.PriceFrom ?? 0) > 0 ||
                (filter.PriceTo ?? 0) > 0 ||
                ((filter.PriceFrom ?? 0) == 0 && (filter.PriceTo ?? 0) == 0 && filter.PriceVnd > 0)
            );

            var ordered = hasPriceFilter
                ? query.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                : query.OrderByDescending(p => p.Id);

            return await ordered
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
                    p.Url ?? string.Empty,
                    p.CreateBy,
                    p.CreateDate,
                    p.UpdateBy,
                    p.LastUpdateDay,
                    p.Keyword))
                .ToListAsync();
        }

        private IQueryable<Product> BuildQuery(ProductModel? filter)
        {
            var query = _db.Products
                .AsNoTracking()
                .Where(p => p.Status >= 0);

            if (filter == null)
                return query;

            if (!string.IsNullOrWhiteSpace(filter.BrandName))
            {
                var brandPattern = $"%{filter.BrandName.Trim()}%";
                query = query.Where(p =>
                    _db.Brands.Any(b => b.Id == p.BrandId &&
                        EF.Functions.Like(EF.Functions.Collate(b.Name, "Vietnamese_100_CI_AI"), brandPattern)));
            }

            var priceFrom = filter.PriceFrom ?? 0m;
            var priceTo = filter.PriceTo ?? 0m;
            var hasExactPrice = filter.PriceVnd > 0 && priceFrom == 0 && priceTo == 0;

            if (priceFrom > 0 && priceTo > 0 && priceFrom > priceTo)
            {
                (priceFrom, priceTo) = (priceTo, priceFrom);
            }

            if (priceFrom > 0)
                query = query.Where(p => p.PriceVnd >= priceFrom);
            if (priceTo > 0)
                query = query.Where(p => p.PriceVnd <= priceTo);
            if (hasExactPrice)
                query = query.Where(p => p.PriceVnd <= filter.PriceVnd);

            var stockFrom = filter.StockFrom ?? 0;
            var stockTo = filter.StockTo ?? 0;
            var hasExactStock = filter.Stock > 0 && stockFrom == 0 && stockTo == 0;

            if (stockFrom > 0 && stockTo > 0 && stockFrom > stockTo)
            {
                (stockFrom, stockTo) = (stockTo, stockFrom);
            }

            if (stockFrom > 0)
                query = query.Where(p => p.Stock >= stockFrom);
            if (stockTo > 0)
                query = query.Where(p => p.Stock <= stockTo);
            if (hasExactStock)
                query = query.Where(p => p.Stock == filter.Stock);

            if (filter.Status == 0 || filter.Status == 1)
                query = query.Where(p => p.Status == filter.Status);

            // Keyword search
            if (string.IsNullOrWhiteSpace(filter.Keyword))
                return query;

            var term = filter.Keyword.Trim();
            var termNorm = TextNormalizer.ToAsciiKeyword(term);
            var pattern = $"%{termNorm}%";

            return query.Where(p =>
                //EF.Functions.Like(EF.Functions.Collate(p.Keyword ?? string.Empty, "Vietnamese_100_CI_AI"), pattern) ||
                EF.Functions.Like(
                    EF.Functions.Collate(
                        ((p.Code ?? string.Empty) + " " +
                         (p.Name ?? string.Empty) + " " +
                         (p.Brand.Name ?? string.Empty) + " " +
                         (p.Description ?? string.Empty)),
                        "Vietnamese_100_CI_AI"),
                    pattern));
        }
    }
}