// Không dùng Data Annotation vì sẽ cấu hình bằng FLuent API ở AppDbContext
namespace SPMH.DBContext.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int BrandId { get; set; }
        public Brand Brand { get; set; } = default!;
        public decimal PriceVnd { get; set; }
        public int Stock { get; set; }
        public int Status { get; set; } = 1; 
        public string? Description { get; set; }
        public string? Url { get; set; }

        public int CreateBy { get; set; }
        public DateTime CreateDate { get; set; }
        public Account CreateByAccount { get; set; } = default!;

        public int UpdateBy { get; set; }
        public Account UpdateByAccount { get; set; } = default!;

        public DateTime? LastUpdateDay { get; set; }

        public string? Keyword { get; set; }
    }
}