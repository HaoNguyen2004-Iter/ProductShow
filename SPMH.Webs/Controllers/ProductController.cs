using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
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

        public async Task<IActionResult> Index(
            [FromQuery] ProductModel? filter,
            int page = 1,
            int pageSize = 5)
        {
            try
            {
                filter ??= new ProductModel();

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
                return BadRequest(ex.Message);
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
                    return Ok(new { ok = true, message = "Cập nhật sản phẩm thành công" });

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
        public async Task<IActionResult> UploadChunk([FromForm] IFormFile chunk, [FromForm] string fileCode, [FromForm] int chunkIndex)
        {
            if (chunk == null || chunk.Length == 0) return BadRequest(new { ok = false, error = "Không có chunk" });
            try
            {
                fileCode = fileCode?.Replace("\"", string.Empty) ?? string.Empty;
                using var stream = chunk.OpenReadStream();
                await _imageStorage.SaveChunkAsync(stream, fileCode, chunkIndex);
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CompleteUpload([FromForm] string fileCode, [FromForm] string fileName, [FromForm] int totalChunks = 0)
        {
            if (string.IsNullOrWhiteSpace(fileCode) || string.IsNullOrWhiteSpace(fileName))
                return BadRequest(new { ok = false, error = "Thiếu fileCode hoặc fileName." });

            try
            {
                var saved = await _imageStorage.MergeChunksAsync(fileCode, fileName, totalChunks);
                return Ok(new { ok = true, url = saved.Url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { ok = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Import([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { ok = false, error = "Không thấy tệp để tải lên." });

            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int userId = 0;
                if (!int.TryParse(userIdStr, out userId)) userId = 1;

                var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
                var errors = new List<string>();
                var success = 0;
                var total = 0;

                using var stream = file.OpenReadStream();

                if (ext == ".xlsx")
                {
                    IWorkbook wb = new XSSFWorkbook(stream);
                    var sheet = wb.GetSheetAt(0);
                    var startRow = sheet.FirstRowNum + 1; 
                    var lastRow = sheet.LastRowNum;
                    for (int r = startRow; r <= lastRow; r++)
                    {
                        var row = sheet.GetRow(r);
                        if (row == null) continue;
                        total++;

                        string GetCellString(ICell? c) => c == null ? string.Empty : c.ToString().Trim();

                        var code = GetCellString(row.GetCell(0));
                        var name = GetCellString(row.GetCell(1));
                        var brand = GetCellString(row.GetCell(2));
                        var priceRaw = GetCellString(row.GetCell(3));
                        var stockRaw = GetCellString(row.GetCell(4));
                        var desc = GetCellString(row.GetCell(5));
                        var statusRaw = GetCellString(row.GetCell(6));

                        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand))
                        {
                            errors.Add($"Row {r + 1}: missing Code/Name/Brand");
                            continue;
                        }

                        if (!decimal.TryParse(priceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                            price = 0m;
                        if (!int.TryParse(stockRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
                            stock = 0;
                        int status = 1;
                        if (int.TryParse(statusRaw, out var st) && (st == 0 || st == 1)) status = st;

                        var product = new ProductModel
                        {
                            Code = code,
                            Name = name,
                            BrandName = brand,
                            PriceVnd = price,
                            Stock = stock,
                            Description = string.IsNullOrWhiteSpace(desc) ? null : desc,
                            Status = status,
                            CreateBy = userId,
                            UpdateBy = userId
                        };

                        try
                        {
                            await _productCommand.CreateAsync(product);
                            success++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Row {r + 1}: {ex.Message}");
                        }
                    }
                }
                else if (ext == ".csv")
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    // Read header
                    var headerLine = await reader.ReadLineAsync();
                    if (headerLine == null)
                        return BadRequest(new { ok = false, error = "CSV is empty" });

                    string[] ParseCsvLine(string line)
                    {
                        var fields = new List<string>();
                        if (line == null) return fields.ToArray();
                        var sb = new StringBuilder();
                        bool inQuote = false;
                        for (int i = 0; i < line.Length; i++)
                        {
                            var ch = line[i];
                            if (ch == '"')
                            {
                                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                                {
                                    sb.Append('"');
                                    i++;
                                }
                                else
                                {
                                    inQuote = !inQuote;
                                }
                            }
                            else if (ch == ',' && !inQuote)
                            {
                                fields.Add(sb.ToString());
                                sb.Clear();
                            }
                            else
                            {
                                sb.Append(ch);
                            }
                        }
                        fields.Add(sb.ToString());
                        return fields.ToArray();
                    }

                    int rowIndex = 1;
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        rowIndex++;
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        total++;
                        var parts = ParseCsvLine(line);
                        string GetAt(int idx) => idx < parts.Length ? parts[idx].Trim() : string.Empty;

                        var code = GetAt(0);
                        var name = GetAt(1);
                        var brand = GetAt(2);
                        var priceRaw = GetAt(3);
                        var stockRaw = GetAt(4);
                        var desc = GetAt(5);
                        var statusRaw = GetAt(6);

                        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(brand))
                        {
                            errors.Add($"Row {rowIndex}: missing Code/Name/Brand");
                            continue;
                        }

                        if (!decimal.TryParse(priceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                            price = 0m;
                        if (!int.TryParse(stockRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
                            stock = 0;
                        int status = 1;
                        if (int.TryParse(statusRaw, out var st) && (st == 0 || st == 1)) status = st;

                        var product = new ProductModel
                        {
                            Code = code,
                            Name = name,
                            BrandName = brand,
                            PriceVnd = price,
                            Stock = stock,
                            Description = string.IsNullOrWhiteSpace(desc) ? null : desc,
                            Status = status,
                            CreateBy = userId,
                            UpdateBy = userId
                        };

                        try
                        {
                            await _productCommand.CreateAsync(product);
                            success++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Row {rowIndex}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    return BadRequest(new { ok = false, error = "chỉ hỗ trợ .xlsx và .csv" });
                }

                return Ok(new { ok = true, message = $"Nhập {success}/{total} cột", success, total, errors });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsvNpoi()
        {
            try
            {
                var q = Request.Query;
                var filter = new ProductModel();

                string GetFirst(params string[] keys)
                {
                    foreach (var k in keys)
                        if (q.TryGetValue(k, out var val) && !string.IsNullOrWhiteSpace(val)) return val.ToString();
                    return string.Empty;
                }

                var kw = GetFirst("Keyword", "keyword");
                if (!string.IsNullOrWhiteSpace(kw)) filter.Keyword = kw.Trim();

                var brand = GetFirst("BrandName", "brandname", "brand");
                if (!string.IsNullOrWhiteSpace(brand)) filter.BrandName = brand.Trim();

                string pfRaw = GetFirst("PriceFrom", "priceFrom", "pricefrom");
                string ptRaw = GetFirst("PriceTo", "priceTo", "priceto");
                string priceExactRaw = GetFirst("price", "Price", "PriceVnd");

                if (decimal.TryParse(pfRaw, out var pf)) filter.PriceFrom = pf;
                if (decimal.TryParse(ptRaw, out var pt)) filter.PriceTo = pt;
                if (decimal.TryParse(priceExactRaw, out var pExact)) filter.PriceVnd = pExact;

                string sfRaw = GetFirst("StockFrom", "stockFrom", "stockfrom");
                string stRaw = GetFirst("StockTo", "stockTo", "stockto");
                string stockExactRaw = GetFirst("stock", "Stock");

                if (int.TryParse(sfRaw, out var sf)) filter.StockFrom = sf;
                if (int.TryParse(stRaw, out var st)) filter.StockTo = st;
                if (int.TryParse(stockExactRaw, out var sExact)) filter.Stock = sExact;

                var statusRaw = GetFirst("Status", "status");
                if (int.TryParse(statusRaw, out var stt) && (stt == 0 || stt == 1))
                    filter.Status = stt;
                else
                    filter.Status = -999;

                var fileResult = await _productMany.ExportCsvAsync(filter);
                return File(fileResult.Content, fileResult.ContentType, fileResult.FileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(int id)
        {
            try
            {
                var pdfBytes = await _productOne.GeneratePdfAsync(id);
                var product = await _productOne.GetProductByIdAsync(id);
                var fileName = $"product_{product.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportWord(int id)
        {
            try
            {
                var wordBytes = await _productOne.GenerateWordAsync(id);
                var product = await _productOne.GetProductByIdAsync(id);
                var fileName = $"product_{product.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx";
                return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}