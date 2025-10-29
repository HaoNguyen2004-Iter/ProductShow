using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Aspose.Words;
using Aspose.Words.Tables;
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
        private readonly IWebHostEnvironment _env;

        public ProductController(
            ProductCommand productCommand,
            ProductMany productMany,
            ProductOne productOne,
            BrandMany brandMany,
            ImageStorage imageStorage,
            IWebHostEnvironment env)
        {
            _productCommand = productCommand;
            _productMany = productMany;
            _productOne = productOne;
            _brandMany = brandMany;
            _imageStorage = imageStorage;
            _env = env;
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
                return BadRequest(new { ok = false, error = "Chừa chọn sản phẩm để xóa" });
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

                // Lấy dữ liệu danh sách sản phẩm từ service (service chỉ trả dữ liệu, không tạo file)
                var list = await _productMany.GetAllAsync(filter);

                // Tạo workbook và sheet giống logic trước — rồi chuyển sheet thành CSV bytes
                using var wb = new XSSFWorkbook();
                var sheet = wb.CreateSheet("Products");
                var header = sheet.CreateRow(0);
                var headers = new[] { "Code", "Name", "Brand", "PriceVnd", "Stock", "Status", "Description", "CreateDate", "LastUpdate" };
                for (int i = 0; i < headers.Length; i++)
                    header.CreateCell(i).SetCellValue(headers[i]);

                var dateStyle = wb.CreateCellStyle();
                dateStyle.DataFormat = wb.CreateDataFormat().GetFormat("yyyy-MM-dd HH:mm:ss");

                int rowIndex = 1;
                foreach (var p in list)
                {
                    var row = sheet.CreateRow(rowIndex++);
                    int col = 0;
                    row.CreateCell(col++).SetCellValue(p.Code ?? string.Empty);
                    row.CreateCell(col++).SetCellValue(p.Name ?? string.Empty);
                    row.CreateCell(col++).SetCellValue(p.BrandName ?? string.Empty);
                    row.CreateCell(col++).SetCellValue((double)(p.PriceVnd));
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
                return File(ms.ToArray(), "text/csv", fileName);
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
                var product = await _productOne.GetProductByIdAsync(id);

                // Build HTML like in ProductOne.GeneratePdfAsync
                string imgHtml = string.Empty;
                if (!string.IsNullOrWhiteSpace(product.Url))
                {
                    try
                    {
                        var urlTrim = product.Url.Trim();
                        if (!urlTrim.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            && !urlTrim.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = urlTrim.TrimStart('/');
                            var physicalPath = Path.Combine(_env.WebRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(physicalPath))
                            {
                                var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                                var ext = Path.GetExtension(physicalPath).ToLowerInvariant();
                                var mime = ext switch
                                {
                                    ".png" => "image/png",
                                    ".jpg" => "image/jpeg",
                                    ".jpeg" => "image/jpeg",
                                    ".gif" => "image/gif",
                                    _ => "application/octet-stream"
                                };
                                var b64 = Convert.ToBase64String(bytes);
                                imgHtml = $"<div style=\"text-align:center;margin-bottom:12px;\"><img src=\"data:{mime};base64,{b64}\" style=\"max-height:220px;object-fit:contain;\"/></div>";
                            }
                        }
                    }
                    catch { }
                }

                var html = new StringBuilder();
                html.AppendLine("<!doctype html><html><head><meta charset='utf-8'>");
                html.AppendLine("<style>");
                html.AppendLine("body{font-family:Arial,Helvetica,sans-serif;margin:20px;color:#222}");
                html.AppendLine(".hdr{display:flex;justify-content:space-between;align-items:center;margin-bottom:18px}");
                html.AppendLine(".title{font-size:20px;font-weight:700}");
                html.AppendLine(".meta{font-size:12px;color:#666}");
                html.AppendLine(".grid{display:flex;flex-wrap:wrap;gap:12px}");
                html.AppendLine(".cell{flex:1 1 45%;min-width:200px;background:#f8f8f8;padding:10px;border-radius:6px}");
                html.AppendLine(".label{font-weight:600;color:#444;margin-bottom:6px;display:block}");
                html.AppendLine(".val{font-size:14px;color:#111}");
                html.AppendLine(".desc{margin-top:10px;padding:10px;background:#fff;border:1px solid #eee;border-radius:6px}");
                html.AppendLine("</style>");
                html.AppendLine("</head><body>");
                html.AppendLine("<div class=\"hdr\">");
                html.AppendLine($"<div class=\"title\">{System.Net.WebUtility.HtmlEncode(product.Name)}</div>");
                html.AppendLine($"<div class=\"meta\">Mã: {System.Net.WebUtility.HtmlEncode(product.Code)}<br/>Thương hiệu: {System.Net.WebUtility.HtmlEncode(product.BrandName)}</div>");
                html.AppendLine("</div>");
                html.AppendLine(imgHtml);
                html.AppendLine("<div class=\"grid\">");
                html.AppendLine("<div class=\"cell\"><span class=\"label\">Giá (VND)</span><div class=\"val\">" + product.PriceVnd.ToString("#,0") + "</div></div>");
                html.AppendLine("<div class=\"cell\"><span class=\"label\">Tồn kho</span><div class=\"val\">" + product.Stock.ToString("#,0") + "</div></div>");
                html.AppendLine("<div class=\"cell\"><span class=\"label\">Trạng thái</span><div class=\"val\">" + (product.Status == 1 ? "Hoạt động" : "Dừng bán") + "</div></div>");
                html.AppendLine("<div class=\"cell\"><span class=\"label\">Người tạo</span><div class=\"val\">" + System.Net.WebUtility.HtmlEncode(product.CreateByName ?? string.Empty) + "</div></div>");
                html.AppendLine("</div>");
                html.AppendLine("<div class=\"desc\"><span class=\"label\">Mô tả</span><div class=\"val\">" + System.Net.WebUtility.HtmlEncode(product.Description ?? string.Empty).Replace("\n", "<br/>") + "</div></div>");
                html.AppendLine("<div style=\"margin-top:14px;font-size:11px;color:#888\">In lúc: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</div>");
                html.AppendLine("</body></html>");

                var htmlString = html.ToString();

                // wkhtmltopdf binary path under wwwroot/Rotativa
                var rotativaDir = Path.Combine(_env.WebRootPath, "Rotativa");
                var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wkhtmltopdf.exe" : "wkhtmltopdf";
                var exePath = Path.Combine(rotativaDir, exeName);

                if (!System.IO.File.Exists(exePath))
                    return BadRequest($"wkhtmltopdf not found at '{exePath}'. Put the binary into wwwroot/Rotativa.");

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "- -",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start wkhtmltopdf process.");

                await using (var stdin = proc.StandardInput.BaseStream)
                {
                    var htmlBytes = Encoding.UTF8.GetBytes(htmlString);
                    await stdin.WriteAsync(htmlBytes, 0, htmlBytes.Length);
                    await stdin.FlushAsync();
                }

                await using var ms = new MemoryStream();
                var copyTask = proc.StandardOutput.BaseStream.CopyToAsync(ms);

                var exited = proc.WaitForExit(30000);
                if (!exited)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    return BadRequest("wkhtmltopdf timed out.");
                }

                await copyTask;

                var stderr = await proc.StandardError.ReadToEndAsync();
                if (proc.ExitCode != 0)
                {
                    return BadRequest("wkhtmltopdf failed: " + stderr);
                }

                var fileName = $"product_{product.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                return File(ms.ToArray(), "application/pdf", fileName);
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
                var product = await _productOne.GetProductByIdAsync(id);

                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);

                // ==== PAGE + BASE STYLE ====
                builder.PageSetup.PaperSize = Aspose.Words.PaperSize.A4;
                builder.PageSetup.TopMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(20);
                builder.PageSetup.BottomMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(20);
                builder.PageSetup.LeftMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(20);
                builder.PageSetup.RightMargin = Aspose.Words.ConvertUtil.MillimeterToPoint(20);

                builder.Font.Name = "Segoe UI";
                builder.Font.Size = 11;
                builder.Font.Color = System.Drawing.Color.Black;
                builder.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
                builder.ParagraphFormat.LineSpacing = 14;
                builder.ParagraphFormat.SpaceAfter = 6;
                builder.ParagraphFormat.SpaceBefore = 0;
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Left;

                // ==== HEADER / TITLE ====
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Center;
                builder.Font.Size = 18;
                builder.Font.Bold = true;
                builder.Font.Color = System.Drawing.Color.FromArgb(30, 30, 30);
                builder.Writeln(product.Name ?? string.Empty);

                builder.Font.Size = 11;
                builder.Font.Bold = false;
                builder.Font.Color = System.Drawing.Color.Gray;
                builder.Writeln($"Mã sản phẩm: {product.Code ?? "-"}");

                builder.ParagraphFormat.Alignment = ParagraphAlignment.Left;
                builder.Font.Color = System.Drawing.Color.Black;
                builder.InsertParagraph();

                var shapeLine = new Aspose.Words.Drawing.Shape(doc, Aspose.Words.Drawing.ShapeType.Line)
                {
                    StrokeColor = System.Drawing.Color.Silver,
                    StrokeWeight = 1.0,
                    Width = 450
                };
                shapeLine.RelativeHorizontalPosition = Aspose.Words.Drawing.RelativeHorizontalPosition.Margin;
                shapeLine.RelativeVerticalPosition = Aspose.Words.Drawing.RelativeVerticalPosition.Paragraph;
                shapeLine.WrapType = Aspose.Words.Drawing.WrapType.None;
                builder.InsertNode(shapeLine);

                builder.InsertParagraph();
                builder.InsertParagraph();

                // ==== IMAGE BLOCK ====
                if (!string.IsNullOrWhiteSpace(product.Url))
                {
                    try
                    {
                        var urlTrim = product.Url.Trim();
                        if (!urlTrim.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            && !urlTrim.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            var rel = urlTrim.TrimStart('/');
                            var physicalPath = Path.Combine(_env.WebRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(physicalPath))
                            {
                                var imgBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
                                using var imgStream = new MemoryStream(imgBytes);

                                builder.ParagraphFormat.Alignment = ParagraphAlignment.Center;

                                var imgShape = builder.InsertImage(imgStream);
                                if (imgShape.Width > 400)
                                {
                                    var ratio = 400.0 / imgShape.Width;
                                    imgShape.Width = 400;
                                    imgShape.Height = imgShape.Height * ratio;
                                }

                                builder.InsertParagraph();
                                builder.Font.Size = 9;
                                builder.Font.Color = System.Drawing.Color.Gray;
                                builder.Font.Bold = false;
                                builder.Writeln("Hình minh họa sản phẩm");

                                // reset style
                                builder.Font.Size = 11;
                                builder.Font.Color = System.Drawing.Color.Black;
                                builder.Font.Bold = false;
                                builder.InsertParagraph();
                            }
                        }
                    }
                    catch { }
                }

                // ==== PRODUCT INFO TABLE ====
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Left;
                builder.Font.Bold = true;
                builder.Font.Size = 12;
                builder.Writeln("Thông tin chi tiết");
                builder.Font.Bold = false;
                builder.Font.Size = 11;

                builder.InsertParagraph();

                builder.StartTable();

                builder.CellFormat.ClearFormatting();
                builder.RowFormat.ClearFormatting();
                builder.CellFormat.VerticalAlignment = CellVerticalAlignment.Center;
                builder.CellFormat.LeftPadding = 6;
                builder.CellFormat.RightPadding = 6;
                builder.CellFormat.TopPadding = 4;
                builder.CellFormat.BottomPadding = 4;
                builder.CellFormat.Borders.Color = System.Drawing.Color.Silver;
                builder.CellFormat.Borders.LineWidth = 0.5;

                void AddRowStyled(string label, string value)
                {
                    builder.InsertCell();
                    builder.CellFormat.Width = 140;
                    builder.Font.Bold = true;
                    builder.Font.Color = System.Drawing.Color.Black;
                    builder.Write(label);

                    builder.InsertCell();
                    builder.CellFormat.Width = 300;
                    builder.Font.Bold = false;
                    builder.Font.Color = System.Drawing.Color.Black;
                    builder.Write(value ?? string.Empty);

                    builder.EndRow();
                }

                AddRowStyled("Thương hiệu", product.BrandName ?? string.Empty);
                AddRowStyled("Giá (VND)", product.PriceVnd.ToString("#,0"));
                AddRowStyled("Tồn kho", product.Stock.ToString("#,0"));
                AddRowStyled("Trạng thái", product.Status == 1 ? "Hoạt động" : "Dừng bán");
                AddRowStyled("Người tạo", product.CreateByName ?? string.Empty);
                AddRowStyled("Ngày tạo", product.CreateDate != default ? product.CreateDate.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty);
                AddRowStyled("Người sửa gần nhất", product.UpdateByName ?? string.Empty);
                AddRowStyled("Ngày sửa gần nhất", product.LastUpdateDay != default ? product.LastUpdateDay.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty);

                builder.EndTable();

                builder.InsertParagraph();
                builder.InsertParagraph();

                // ==== DESCRIPTION ====
                builder.Font.Bold = true;
                builder.Font.Size = 12;
                builder.Font.Color = System.Drawing.Color.Black;
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Left;
                builder.Writeln("Mô tả sản phẩm");

                builder.Font.Bold = false;
                builder.Font.Size = 11;
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Justify;

                if (!string.IsNullOrWhiteSpace(product.Description))
                {
                    var lines = product.Description
                        .Replace("\r\n", "\n")
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        builder.Writeln(line.Trim());
                    }
                }
                else
                {
                    builder.Writeln("(Không có mô tả)");
                }

                builder.InsertParagraph();
                builder.InsertParagraph();

                // ==== FOOTER ====
                builder.ParagraphFormat.Alignment = ParagraphAlignment.Left;
                builder.Font.Size = 9;
                builder.Font.Bold = false;
                builder.Font.Color = System.Drawing.Color.Gray;
                builder.Writeln($"In lúc: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                using var msOut = new MemoryStream();
                doc.Save(msOut, SaveFormat.Docx);
                var fileName = $"product_{product.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx";
                return File(msOut.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}