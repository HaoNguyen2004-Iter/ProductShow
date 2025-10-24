using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPMH.DBContext.Entities;
using SPMH.Services.Executes;
using SPMH.Services.Executes.Brands;
using SPMH.Services.Executes.Products;
using SPMH.Services.Executes.Storage;
using SPMH.Services.Models;

namespace SPMH.Webs.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ProductCommand _productCommand;
        private readonly ProductMany _productMany;
        private readonly ProductOne _productOne;
        private readonly BrandMany _brandMany;
        private readonly ImageStorage _imageStorage;

        public ProductController(ProductCommand productCommand, ProductMany productMany, ProductOne productOne, BrandMany brandMany, ImageStorage imageStorage)
        {
            _productCommand = productCommand;
            _productMany = productMany;
            _productOne = productOne;
            _brandMany = brandMany;
            _imageStorage = imageStorage;
        }
        public async Task<IActionResult> Index(ProductFilter? filter, int page = 1, int pageSize = 5)
        {

            try
            {
                var products = await _productMany.GetPagedAsync(page, pageSize, filter);

                if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                {
                    ViewBag.Brands = await _brandMany.GetAllBrandAsync();
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView("~/Views/Shared/Product/_ProductTable.cshtml", products);
                } 
                return View(products);

            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Form(int? id)
        {
            try
            {
                ProductModel model;
                if (id.HasValue && id.Value > 0)
                {
                    model = await _productOne.GetProductByIdAsync(id.Value);
                }
                else
                {
                    model = new ProductModel();
                }

                ViewBag.Brands = await _brandMany.GetAllBrandAsync();
                return PartialView("~/Views/Shared/Product/_ProductForm.cshtml", model);
            }
            catch (Exception ex)
            {
                return BadRequest( ex.Message );
            }
        }

        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            try
            {
                var product = await _productOne.GetProductByIdAsync(id);
                return PartialView("~/Views/Shared/Product/_ProductView.cshtml", product);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductModel product)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out var uid))
                {
                    product.CreateBy = uid;
                    product.UpdateBy = uid;
                }
                string productName = await _productCommand.CreateAsync(product);
                return Ok(new { ok = true, message = "Tạo thành công sản phẩm " + productName });
                
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] ProductModel product)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out var uid))
                {
                    product.UpdateBy = uid;
                }

                var isSuccess = await _productCommand.EditProductByIdAsync(product);
                if (isSuccess)
                    return Ok(new { ok = true, message = "Cập nhật sản phẩm thành công." });

                return BadRequest(new { ok = false, error = "Đã xảy ra lỗi khi cập nhật sản phẩm." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var isSuccess = await _productCommand.DeleteProductByIdAsync(id);
                if (isSuccess)
                    return Ok(new { ok = true, message = "Xóa sản phẩm thành công." });

                return BadRequest(new { ok = false, error = "Đã xảy ra lỗi khi xóa sản phẩm." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromForm] int[] ids)
        {
            if (ids == null || ids.Length == 0)
            {
                return BadRequest(new { ok = false, error = "Chựa chọn sản phẩm để xóa" });
            }

            var distinctIds = ids.Distinct().ToList();
            var success = 0;

            foreach (var id in distinctIds)
            {
                try
                {
                    if (await _productCommand.DeleteProductByIdAsync(id))
                        success++;
                }
                catch
                {
                    return BadRequest(new { ok = false, error = "Lỗi khi xóa nhiều sản phẩm" });
                }
            }

            if (success > 0)
                return Ok(new { ok = true, message = $"Đã xóa {success} sản phẩm thành công." });
            else
                return BadRequest(new { ok = false, error = "Lỗi khi xóa nhiều sản phẩm" });

        }

        [HttpPost]
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { ok = false, error = "Không thấy tệp để tải lên. " });
                using var stream = file.OpenReadStream();
                var saved = await _imageStorage.SaveProductImageAsync(stream, file.FileName);
                return Ok(new { ok = true, url = saved.Url });

            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }
    }
}
