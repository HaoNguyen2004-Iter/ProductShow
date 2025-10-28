using System.Text;
using Microsoft.EntityFrameworkCore;
using NPOI.XSSF.UserModel;
using SPMH.DBContext;
using SPMH.DBContext.Entities;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Products
{
    public class ProductCommand
    {
        private readonly AppDbContext _db;
        public ProductCommand(AppDbContext db) => _db = db;

        public async Task<string> CreateAsync(ProductModel product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));

            if (SqlGuard.IsSuspicious(product)) throw new ArgumentNullException("Đầu vào đáng ngờ");

            var code = (product.Code ?? string.Empty).Trim();
            var name = (product.Name ?? string.Empty).Trim();
            var brandName = (product.BrandName ?? string.Empty).Trim();
            var description = product.Description?.Trim();

            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Mã sản phẩm không được để trống.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tên sản phẩm không được để trống.");
            if (string.IsNullOrWhiteSpace(brandName)) throw new ArgumentException("Thương hiệu không được để trống.");
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Mô tả sản phẩm không được để trống.");
            if (product.PriceVnd < 0) throw new ArgumentException("Giá sản phẩm không hợp lệ");
            if (product.Stock < 0) throw new ArgumentException("Số lượng kho sản phẩm không hợp lệ");

            var normCode = code.ToLower();
            var normName = name.ToLower();

            var exists = await _db.Products.AnyAsync(p =>
                p.Status >= 0 &&
                (p.Code.ToLower() == normCode || p.Name.ToLower() == normName));
            if (exists) throw new InvalidOperationException("Sản phẩm đã tồn tại");

            var normBrand = brandName.ToLower();
            var brandId = await _db.Brands.AsNoTracking()
                .Where(b => b.Name.ToLower() == normBrand)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();
            if (brandId == 0) throw new InvalidOperationException("Thương hiệu không tồn tại");

            var anyAccountId = await _db.Accounts.AsNoTracking().Select(a => a.Id).FirstOrDefaultAsync();
            if (anyAccountId == 0) throw new InvalidOperationException("Không tìm thấy tài khoản hợp lệ.");

            var createBy = product.CreateBy > 0 &&
                           await _db.Accounts.AsNoTracking().AnyAsync(a => a.Id == product.CreateBy)
                           ? product.CreateBy : anyAccountId;

            var updateBy = product.UpdateBy > 0 &&
                           await _db.Accounts.AsNoTracking().AnyAsync(a => a.Id == product.UpdateBy)
                           ? product.UpdateBy : createBy;

            var now = DateTime.Now;
            var imageUrl = product.Url?.Trim();

            var entity = new Product
            {
                Code = code,
                Name = name,
                Description = description,
                PriceVnd = product.PriceVnd,
                Stock = product.Stock,
                BrandId = brandId,
                Status = product.Status == 0 ? 0 : 1,
                Url = imageUrl,
                CreateBy = 1,
                CreateDate = now,
                UpdateBy = 1,
                LastUpdateDay = now
            };

            _db.Products.Add(entity);
            await _db.SaveChangesAsync();
            return entity.Name;
        }

        public async Task<bool> DeleteProductByIdAsync(int productId)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (entity == null) return false;

            entity.Status = -1;
            entity.LastUpdateDay = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EditProductByIdAsync(ProductModel product)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));

            var code = (product.Code ?? string.Empty).Trim();
            var name = (product.Name ?? string.Empty).Trim();
            var brandName = (product.BrandName ?? string.Empty).Trim();
            var description = product.Description?.Trim();

            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Mã sản phẩm không được để trống.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tên sản phẩm không được để trống.");
            if (string.IsNullOrWhiteSpace(brandName)) throw new ArgumentException("Thương hiệu không được để trống.");
            if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Mô tả sản phẩm không được để trống.");
            if (product.PriceVnd < 0) throw new ArgumentException("Giá sản phẩm không hợp lệ");
            if (product.Stock < 0) throw new ArgumentException("Số lượng kho sản phẩm không hợp lệ");

            var normCode = code.ToLower();
            var normName = name.ToLower();

            var duplicate = await _db.Products.AnyAsync(p =>
                p.Status >= 0 &&
                p.Id == product.Id &&
                (p.Code.ToLower() == normCode || p.Name.ToLower() == normName));
            if (!duplicate) throw new InvalidOperationException("Mã hoặc tên sản phẩm không tồn tại");

            var normBrand = brandName.ToLower();
            var brandId = await _db.Brands.AsNoTracking()
                .Where(b => b.Name.ToLower() == normBrand)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();
            if (brandId == 0) throw new InvalidOperationException("Thương hiệu không tồn tại");

            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
            if (entity == null) throw new InvalidOperationException("Sản phẩm không tồn tại");

            entity.Code = code;
            entity.Name = name;
            entity.Description = description;
            entity.PriceVnd = product.PriceVnd;
            entity.Stock = product.Stock;
            entity.BrandId = brandId;
            entity.Status = product.Status;

            if (!string.IsNullOrWhiteSpace(product.Url))
                entity.Url = product.Url.Trim();

            if (product.UpdateBy > 0 &&
                await _db.Accounts.AsNoTracking().AnyAsync(a => a.Id == product.UpdateBy))
            {
                entity.UpdateBy = product.UpdateBy;
            }
            entity.LastUpdateDay = DateTime.Now;

            await _db.SaveChangesAsync();
            return true;
        }

        
    }
}