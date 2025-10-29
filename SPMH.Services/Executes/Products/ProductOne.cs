using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Aspose.Words;
using Aspose.Words.Tables;
using Microsoft.EntityFrameworkCore;
using SPMH.DBContext;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Products
{
    public class ProductOne
    {
        private readonly AppDbContext _db;
        private readonly string _webRootPath;

        public ProductOne(AppDbContext db, string webRootPath)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _webRootPath = webRootPath ?? string.Empty;
        }

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
                    p.Url ?? string.Empty,
                    p.CreateBy,
                    p.CreateDate,
                    p.UpdateBy,
                    p.LastUpdateDay,
                    p.Keyword)
                {
                    CreateByName = p.CreateByAccount != null ? p.CreateByAccount.Username : string.Empty,
                    UpdateByName = p.UpdateByAccount != null ? p.UpdateByAccount.Username : string.Empty
                })
                .FirstOrDefaultAsync();

            if (product == null)
                throw new ArgumentException("Sản phẩm không tồn tại");
            return product;
        }

    }
}