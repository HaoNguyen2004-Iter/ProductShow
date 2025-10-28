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

        public async Task<byte[]> GeneratePdfAsync(int productId)
        {
            var product = await GetProductByIdAsync(productId);

            // Build a small standalone HTML with inline styles and embed image as data-uri (if present)
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
                        var physicalPath = Path.Combine(_webRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(physicalPath))
                        {
                            var bytes = await File.ReadAllBytesAsync(physicalPath);
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
                catch
                {
                }
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
            html.AppendLine($"<div class=\"title\">{EscapeHtml(product.Name)}</div>");
            html.AppendLine($"<div class=\"meta\">Mã: {EscapeHtml(product.Code)}<br/>Thương hiệu: {EscapeHtml(product.BrandName)}</div>");
            html.AppendLine("</div>");
            html.AppendLine(imgHtml);
            html.AppendLine("<div class=\"grid\">");
            html.AppendLine("<div class=\"cell\"><span class=\"label\">Giá (VND)</span><div class=\"val\">" + product.PriceVnd.ToString("#,0") + "</div></div>");
            html.AppendLine("<div class=\"cell\"><span class=\"label\">Tồn kho</span><div class=\"val\">" + product.Stock.ToString("#,0") + "</div></div>");
            html.AppendLine("<div class=\"cell\"><span class=\"label\">Trạng thái</span><div class=\"val\">" + (product.Status == 1 ? "Hoạt động" : "Dừng bán") + "</div></div>");
            html.AppendLine("<div class=\"cell\"><span class=\"label\">Người tạo</span><div class=\"val\">" + EscapeHtml(product.CreateByName ?? string.Empty) + "</div></div>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"desc\"><span class=\"label\">Mô tả</span><div class=\"val\">" + EscapeHtml(product.Description ?? string.Empty).Replace("\n", "<br/>") + "</div></div>");
            html.AppendLine("<div style=\"margin-top:14px;font-size:11px;color:#888\">In lúc: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</div>");
            html.AppendLine("</body></html>");

            var htmlString = html.ToString();

            // Find wkhtmltopdf executable under wwwroot/Rotativa
            var rotativaDir = Path.Combine(_webRootPath, "Rotativa");
            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wkhtmltopdf.exe" : "wkhtmltopdf";
            var exePath = Path.Combine(rotativaDir, exeName);

            if (!File.Exists(exePath))
                throw new InvalidOperationException($"wkhtmltopdf not found at '{exePath}'. Put the binary into wwwroot/Rotativa.");

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
                throw new InvalidOperationException("wkhtmltopdf timed out.");
            }

            await copyTask; 

            var stderr = await proc.StandardError.ReadToEndAsync();
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException("wkhtmltopdf failed: " + stderr);
            }

            return ms.ToArray();
        }

        public async Task<byte[]> GenerateWordAsync(int productId)
        {
            var product = await GetProductByIdAsync(productId);

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
                        var physicalPath = Path.Combine(_webRootPath, rel.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(physicalPath))
                        {
                            var imgBytes = await File.ReadAllBytesAsync(physicalPath);
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
            builder.CellFormat.Borders.LineStyle = LineStyle.Single;
            builder.CellFormat.Borders.Color = System.Drawing.Color.Silver;
            builder.CellFormat.Borders.LineWidth = 0.5;

            void AddRowStyled(string label, string value)
            {
                // label col
                builder.InsertCell();
                builder.CellFormat.Width = 140;
                builder.Font.Bold = true;
                builder.Font.Color = System.Drawing.Color.Black;
                builder.Write(label);

                // value col
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
            return msOut.ToArray();
        }

        private static string EscapeHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return System.Net.WebUtility.HtmlEncode(input);
        }
    }
}