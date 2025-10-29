using System.IO;
using System.IO.Enumeration;
using SPMH.Services.Models;

namespace SPMH.Services.Executes.Storage
{
    public class ImageStorage
    {
        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png"
        };

        // Kích thước tối đa cho mỗi chunk khi upload phân mảnh
        private const long MaxChunkSize = 500L * 1024; 

        // Giới hạn tổng kích thước file 
        private const long MaxTotalFileSize = 5L * 1024 * 1024; 

        private readonly string _physicalRoot;     // Đường dẫn vật lý trên server 
        private readonly string _publicBasePath;   // Đường dẫn công khai 

        // Constructor: Khởi tạo với đường dẫn lưu trữ và URL công khai
        public ImageStorage(string physicalRoot, string publicBasePath)
        {
            _physicalRoot = physicalRoot ?? throw new ArgumentNullException(nameof(physicalRoot));
            _publicBasePath = publicBasePath ?? throw new ArgumentNullException(nameof(publicBasePath));

            Directory.CreateDirectory(_physicalRoot);
        }

        /// <summary>
        /// Lưu từng chunk 
        /// </summary>
        public async Task SaveChunkAsync(Stream chunkStream, string fileCode, int chunkIndex)
        {
            if (chunkStream == null) throw new ArgumentNullException(nameof(chunkStream));
            if (string.IsNullOrWhiteSpace(fileCode)) throw new ArgumentNullException(nameof(fileCode));
            if (chunkIndex < 0) throw new ArgumentOutOfRangeException(nameof(chunkIndex));

            // loại bỏ dấu nháy kép để tránh lỗi đường dẫn
            fileCode = fileCode.Replace("\"", string.Empty);

            // Thư mục tạm để lưu các chunk
            var tempDir = Path.Combine(_physicalRoot, "content", "temp_upload");
            Directory.CreateDirectory(tempDir);

            var tempPath = Path.Combine(tempDir, $"{fileCode}_{chunkIndex}.chunk");

            // Ghi chunk vào file tạm
            using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920]; // Buffer 80KB
                long totalCopied = 0;
                int read;

                // Đọc từng phần từ stream chunk
                while ((read = await chunkStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    totalCopied += read;

                    // Kiểm tra giới hạn kích thước chunk
                    if (totalCopied > MaxChunkSize)
                    {
                        dst.Dispose();
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                        throw new InvalidOperationException($"Chunk vượt quá dung lượng bytes.");
                    }

                    // Ghi dữ liệu đã đọc vào file tạm
                    await dst.WriteAsync(buffer.AsMemory(0, read));
                }
            }
        }

        /// <summary>
        /// Gộp các chunk thành file hoàn chỉnh sau khi upload xong
        /// </summary>
        public async Task<ProductImage> MergeChunksAsync(string fileCode, string originalFileName, int totalChunks = 0, DateTime? date = null)
        {
            if (string.IsNullOrWhiteSpace(fileCode)) throw new ArgumentNullException(nameof(fileCode));
            if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentNullException(nameof(originalFileName));

            // Loại bỏ ký tự nguy hiểm trong fileCode
            fileCode = fileCode.Replace("\"", string.Empty);

            // Kiểm tra định dạng file gốc
            var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedExt.Contains(ext))
            {
                throw new ArgumentException("Định dạng không hợp lệ", nameof(originalFileName));
            }

            // Thư mục tạm chứa các chunk
            var tempDir = Path.Combine(_physicalRoot, "content", "temp_upload");

            // 1) Kiểm tra tổng kích thước trước khi gộp để đảm bảo file không quá giới hạn
            long totalSize = 0;
            int checkIndex = 0;
            while (true)
            {
                if (totalChunks > 0 && checkIndex >= totalChunks) break;

                var tempPathCheck = Path.Combine(tempDir, $"{fileCode}_{checkIndex}.chunk");
                if (!File.Exists(tempPathCheck))
                {
                    if (totalChunks > 0)
                    {
                        throw new FileNotFoundException($"Missing chunk file: {tempPathCheck}");
                    }
                    break; 
                }

                var fi = new FileInfo(tempPathCheck);
                totalSize += fi.Length;

                if (totalSize > MaxTotalFileSize)
                {
                    throw new InvalidOperationException($"Total uploaded file size exceeds maximum allowed {MaxTotalFileSize} bytes.");
                }

                checkIndex++;
            }

            // Tạo thư mục theo ngày: /media/upload/yyyy/MM/dd
            var publicFolder = DateFolder("/media/upload", date);
            var publicFile = $"{publicFolder}/{fileCode}{ext}";

            // Đường dẫn vật lý đầy đủ của file cuối cùng
            var finalPhysicalPath = GetFullPath(publicFile);

            // Tạo thư mục đích nếu chưa có
            Directory.CreateDirectory(Path.GetDirectoryName(finalPhysicalPath)!);

            // Mở file đích để ghi
            using (var outFs = new FileStream(finalPhysicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                int index = 0;
                while (true)
                {
                    if (totalChunks > 0 && index >= totalChunks) break;

                    var tempPath = Path.Combine(tempDir, $"{fileCode}_{index}.chunk");
                    if (!File.Exists(tempPath))
                    {
                        if (totalChunks > 0)
                        {
                            throw new FileNotFoundException($"Missing chunk file: {tempPath}");
                        }
                        break;
                    }

                    using (var chunkFs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                    {
                        await chunkFs.CopyToAsync(outFs);
                    }

                    File.Delete(tempPath);
                    index++;
                }
            }

            // Tạo URL công khai
            var url = $"{_publicBasePath}{publicFile}";

            var fileInfo = new FileInfo(finalPhysicalPath);
            return new ProductImage
            {
                Url = url,
                Alt = Path.GetFileNameWithoutExtension(originalFileName),
                size = (int?)fileInfo.Length
            };
        }

        /// <summary>
        /// Tạo đường dẫn thư mục theo ngày: /path/yyyy/MM/dd
        /// Tự động tạo thư mục nếu chưa tồn tại
        /// </summary>
        public string DateFolder(string path, DateTime? date)
        {
            var now = date ?? DateTime.Now;

            if (!path.StartsWith("/")) path = "/" + path;
            if (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);

            path += "/" + now.Year;
            var fp = GetFullPath(path);
            if (!Directory.Exists(fp)) Directory.CreateDirectory(fp);

            path += "/" + now.Month;
            fp = GetFullPath(path);
            if (!Directory.Exists(fp)) Directory.CreateDirectory(fp);

            path += "/" + now.Day;
            fp = GetFullPath(path);
            if (!Directory.Exists(fp)) Directory.CreateDirectory(fp);

            return path;
        }

        private string GetFullPath(string publicPath)
        {
            if (string.IsNullOrEmpty(publicPath)) return _physicalRoot;
            var trimmed = publicPath.TrimStart('/');
            var physical = Path.Combine(_physicalRoot, trimmed.Replace('/', Path.DirectorySeparatorChar));
            return physical;
        }
    }
}