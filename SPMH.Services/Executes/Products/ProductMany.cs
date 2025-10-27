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

            if (filter != null)
                if (SqlGuard.IsSuspicious(filter))
                    throw new ArgumentException("Đầu vào không hợp lệ");

            var baseQuery = _db.Products
                .AsNoTracking()
                .Where(p => p.Status >= 0);

            if (filter is not null)
                baseQuery = ApplyFilterWithoutKeyword(baseQuery, filter);

            string? term = null;
            if (!string.IsNullOrWhiteSpace(filter?.Keyword))
                term = filter!.Keyword.Trim();

            var hasPriceFilter = (filter?.PriceVnd ?? 0) > 0;

            if (string.IsNullOrEmpty(term))
            {
                var total0 = await baseQuery.CountAsync();
                var ordered = hasPriceFilter
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
                        p.Url ?? string.Empty,
                        p.CreateBy,
                        p.CreateDate,
                        p.UpdateBy,
                        p.LastUpdateDay,
                        p.Keyword))
                    .ToListAsync();

                return new PagedResult<ProductModel>(items0, total0, page, pageSize);
            }

            var termNorm = TextNormalizer.ToAsciiKeyword(term);
            var pattern = $"%{termNorm}%";

            var kwQuery = baseQuery.Where(p =>
                EF.Functions.Like(EF.Functions.Collate(p.Keyword ?? string.Empty, "Vietnamese_100_CI_AI"), pattern) ||
                EF.Functions.Like(
                    EF.Functions.Collate(
                        ((p.Code ?? string.Empty) + " " +
                         (p.Name ?? string.Empty) + " " +
                         (p.Brand.Name ?? string.Empty) + " " +
                         (p.Description ?? string.Empty)),
                        "Vietnamese_100_CI_AI"),
                    pattern)
            );

            var total = await kwQuery.CountAsync();
            var orderedKw = hasPriceFilter
                ? kwQuery.OrderByDescending(p => p.PriceVnd).ThenByDescending(p => p.Id)
                : kwQuery.OrderByDescending(p => p.Id);

            var items = await orderedKw
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
            if (filter != null)
                if (SqlGuard.IsSuspicious(filter))
                    throw new ArgumentException("Đầu vào không hợp lệ");

            var query = _db.Products
                .AsNoTracking()
                .Where(p => p.Status >= 0);

            if (filter is not null)
                query = ApplyFilterWithoutKeyword(query, filter);

            string? term = null;
            if (!string.IsNullOrWhiteSpace(filter?.Keyword))
                term = filter!.Keyword.Trim();

            if (!string.IsNullOrEmpty(term))
            {
                var termNorm = TextNormalizer.ToAsciiKeyword(term);
                var pattern = $"%{termNorm}%";

                query = query.Where(p =>
                    EF.Functions.Like(EF.Functions.Collate(p.Keyword ?? string.Empty, "Vietnamese_100_CI_AI"), pattern) ||
                    EF.Functions.Like(
                        EF.Functions.Collate(
                            ((p.Code ?? string.Empty) + " " +
                             (p.Name ?? string.Empty) + " " +
                             (p.Brand.Name ?? string.Empty) + " " +
                             (p.Description ?? string.Empty)),
                            "Vietnamese_100_CI_AI"),
                        pattern)
                );
            }

            var hasPriceFilter = (filter?.PriceVnd ?? 0) > 0;
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

        private static IQueryable<Product> ApplyFilterWithoutKeyword(IQueryable<Product> query, ProductModel f)
        {
            if (!string.IsNullOrWhiteSpace(f.BrandName))
            {
                var brand = $"%{f.BrandName.Trim()}%";
                query = query.Where(p => EF.Functions.Like(EF.Functions.Collate(p.Brand.Name, "Vietnamese_100_CI_AI"), brand));
            }

            if (f.PriceVnd > 0)
                query = query.Where(p => p.PriceVnd <= f.PriceVnd);

            if (f.Stock > 0)
                query = query.Where(p => p.Stock == f.Stock);

            if (f.Status == 0 || f.Status == 1)
                query = query.Where(p => p.Status == f.Status);

            return query;
        }

        public async Task<ExportFileResult> ExportCsvAsync(ProductModel filter)
        {
            // === 1. Query dữ liệu (giống trên) ===
            filter ??= new ProductModel();
            var q = _db.Products.AsNoTracking().Where(p => p.Status >= 0);

            if (!string.IsNullOrWhiteSpace(filter.BrandName))
            {
                var bn = filter.BrandName.Trim().ToLower();
                q = q.Where(p => _db.Brands.Any(b => b.Id == p.BrandId && b.Name.ToLower() == bn));
            }
            if (filter.PriceVnd > 0) q = q.Where(p => p.PriceVnd == filter.PriceVnd);
            if (filter.Stock > 0) q = q.Where(p => p.Stock == filter.Stock);
            if (filter.Status != -999) q = q.Where(p => p.Status == filter.Status);

            var list = await q.Select(p => new
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
                BrandName = _db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault()
            }).ToListAsync();

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

            // === 2. Tạo Workbook + Sheet (giống trên) ===
            using var wb = new XSSFWorkbook();
            var sheet = wb.CreateSheet("Products");
            var header = sheet.CreateRow(0);
            var headers = new[] { "Id", "Code", "Name", "Brand", "PriceVnd", "Stock", "Status", "Description", "CreateDate", "LastUpdate" };
            for (int i = 0; i < headers.Length; i++) header.CreateCell(i).SetCellValue(headers[i]);

            var dataFormat = wb.CreateDataFormat();
            var dateStyle = wb.CreateCellStyle();
            dateStyle.DataFormat = dataFormat.GetFormat("yyyy-MM-dd HH:mm:ss");

            int r = 1;
            foreach (var p in products)
            {
                var row = sheet.CreateRow(r++);
                int c = 0;
                row.CreateCell(c++).SetCellValue(p.Id);
                row.CreateCell(c++).SetCellValue(p.Code ?? string.Empty);
                row.CreateCell(c++).SetCellValue(p.Name ?? string.Empty);
                row.CreateCell(c++).SetCellValue(p.BrandName ?? string.Empty);
                row.CreateCell(c++).SetCellValue((double)p.PriceVnd);
                row.CreateCell(c++).SetCellValue(p.Stock);
                row.CreateCell(c++).SetCellValue(p.Status);
                row.CreateCell(c++).SetCellValue(p.Description ?? string.Empty);

                var cellCreate = row.CreateCell(c++);
                if (p.CreateDate != default) { cellCreate.SetCellValue(p.CreateDate); cellCreate.CellStyle = dateStyle; }
                else cellCreate.SetCellValue(string.Empty);

                var cellUpdate = row.CreateCell(c++);
                if (p.LastUpdateDay != default) { cellUpdate.SetCellValue(p.LastUpdateDay); cellUpdate.CellStyle = dateStyle; }
                else cellUpdate.SetCellValue(string.Empty);
            }

            // === 3. Xuất CSV từ Sheet ===
            using var ms = new MemoryStream();
            await using var writer = new StreamWriter(ms, new UTF8Encoding(true), 1024, leaveOpen: true);

            for (int rowIdx = 0; rowIdx <= sheet.LastRowNum; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null)
                {
                    await writer.WriteLineAsync();
                    continue;
                }

                var values = new List<string>();
                for (int colIdx = 0; colIdx < row.LastCellNum; colIdx++)
                {
                    var cell = row.GetCell(colIdx);
                    string value = cell?.CellType switch
                    {
                        CellType.Boolean => cell.BooleanCellValue ? "TRUE" : "FALSE",
                        CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                            ? cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? ""
                            : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
                        CellType.String => cell.StringCellValue ?? "",
                        CellType.Formula => cell.CachedFormulaResultType switch
                        {
                            CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                                ? cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? ""
                                : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
                            CellType.String => cell.StringCellValue ?? "",
                            CellType.Boolean => cell.BooleanCellValue ? "TRUE" : "FALSE",
                            _ => ""
                        },
                        _ => ""
                    } ?? "";

                    values.Add(string.IsNullOrEmpty(value) ? "\"\"" : $"\"{value.Replace("\"", "\"\"")}\"");
                }
                await writer.WriteLineAsync(string.Join(",", values));
            }

            await writer.FlushAsync();
            ms.Position = 0;

            var fileName = $"products_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return new ExportFileResult(ms.ToArray(), "text/csv", fileName);
        }

    }
}