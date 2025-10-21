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

        private readonly string _physicalRoot;
        private readonly string _publicBasePath;

        public ImageStorage(string physicalRoot, string publicBasePath)
        {
            _physicalRoot = physicalRoot ?? throw new ArgumentNullException(nameof(physicalRoot));
            _publicBasePath = publicBasePath ?? throw new ArgumentNullException(nameof(publicBasePath));
            Directory.CreateDirectory(_physicalRoot);
        }

        public async Task<ProductImage> SaveProductImageAsync(Stream content, string fileName)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            //Lấy và kiểm tra đuôi của file
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext) || !AllowedExt.Contains(ext))
            {
                throw new ArgumentNullException("Định dạng không hợp lệ");
            }

            // Tạo tên mới cho ảnh
            var newName = $"{Guid.NewGuid():N}{ext.ToLower()}";
            var physicalPath = Path.Combine(_physicalRoot, newName);

            // Lưu
            using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(fs);
            }

            // Đường dẫn công khai
            var url = $"{_publicBasePath}/{newName}";
            return new ProductImage { Url = url, Alt = Path.GetFileNameWithoutExtension(fileName) };
        }
    }
}
