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

        public async Task<ExportFileResult> ExportCsvAsync(ProductModel filter)
        {
            filter ??= new ProductModel();

            if (SqlGuard.IsSuspicious(filter))
                throw new ArgumentException("Đầu vào không hợp lệ");

            var query = BuildQuery(filter);

            var list = await query
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    p.PriceVnd,
                    p.Stock,
                    p.Status,
                    p.Description,
                    p.CreateDate,
                    p.LastUpdateDay,
                    BrandName = p.Brand.Name
                })
                .ToListAsync();

            var products = list.Select(x => new ProductModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                BrandName = x.BrandName ?? string.Empty,
                PriceVnd = x.PriceVnd,
                Stock = x.Stock,
                Status = x.Status,
                Description = x.Description,
                CreateDate = x.CreateDate,
                LastUpdateDay = x.LastUpdateDay ?? default
            }).ToList();

            using var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("Products");
            var header = sheet.CreateRow(0);
            var headers = new[] { "Id", "Code", "Name", "Brand", "PriceVnd", "Stock", "Status", "Description", "CreateDate", "LastUpdate" };
            for (int i = 0; i < headers.Length; i++)
                header.CreateCell(i).SetCellValue(headers[i]);

            var dateStyle = wb.CreateCellStyle();
            dateStyle.DataFormat = wb.CreateDataFormat().GetFormat("yyyy-MM-dd HH:mm:ss");

            int rowIndex = 1;
            foreach (var p in products)
            {
                var row = sheet.CreateRow(rowIndex++);
                int col = 0;
                row.CreateCell(col++).SetCellValue(p.Id);
                row.CreateCell(col++).SetCellValue(p.Code ?? string.Empty);
                row.CreateCell(col++).SetCellValue(p.Name ?? string.Empty);
                row.CreateCell(col++).SetCellValue(p.BrandName);
                row.CreateCell(col++).SetCellValue((double)p.PriceVnd);
                row.CreateCell(col++).SetCellValue(p.Stock);
                row.CreateCell(col++).SetCellValue(p.Status);
                row.CreateCell(col++).SetCellValue(p.Description ?? string.Empty);

                var cellCreate = row.CreateCell(col++);
                if (p.CreateDate != default)
                {
                    cellCreate.SetCellValue(p.CreateDate);
                    cellCreate.CellStyle = dateStyle;
                }
                else
                {
                    cellCreate.SetCellValue(string.Empty);
                }

                var cellUpdate = row.CreateCell(col++);
                if (p.LastUpdateDay != default)
                {
                    cellUpdate.SetCellValue(p.LastUpdateDay);
                    cellUpdate.CellStyle = dateStyle;
                }
                else
                {
                    cellUpdate.SetCellValue(string.Empty);
                }
            }

            using var ms = new MemoryStream();
            await using var writer = new StreamWriter(ms, new UTF8Encoding(true), 1024, leaveOpen: true);

            for (int r = 0; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null)
                {
                    await writer.WriteLineAsync();
                    continue;
                }

                var values = new List<string>();
                for (int c = 0; c < row.LastCellNum; c++)
                {
                    var cell = row.GetCell(c);
                    string value = cell?.ToString() ?? "";
                    values.Add($"\"{value.Replace("\"", "\"\"")}\"");
                }
                await writer.WriteLineAsync(string.Join(",", values));
            }

            await writer.FlushAsync();
            ms.Position = 0;

            var fileName = $"products_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return new ExportFileResult(ms.ToArray(), "text/csv", fileName);
        }

        private IQueryable<Product> BuildQuery(ProductModel? filter)
        {
            var query = _db.Products
                .AsNoTracking()
                .Where(p => p.Status >= 0);

            if (filter == null)
                return query;

            // Brand filter (robust via Brands table)
            if (!string.IsNullOrWhiteSpace(filter.BrandName))
            {
                var brandPattern = $"%{filter.BrandName.Trim()}%";
                query = query.Where(p =>
                    _db.Brands.Any(b => b.Id == p.BrandId &&
                        EF.Functions.Like(EF.Functions.Collate(b.Name, "Vietnamese_100_CI_AI"), brandPattern)));
            }

            // Price range (existing)
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