namespace SPMH.Services.Models
{
    public class ProductModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int BrandId { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public decimal PriceVnd { get; set; }
        public int Stock { get; set; }
        public int Status { get; set; } 
        public string? Description { get; set; }
        public string Url { get; set; } = string.Empty;

        public ProductModel() { }

        public ProductModel(
            int id,
            string code,
            string name,
            int brandId,
            string brandName,
            decimal priceVnd,
            int stock,
            int status = 1,
            string? description = null,
            string url = "")
        {
            Id = id;
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            BrandId = brandId;
            BrandName = brandName ?? string.Empty;
            PriceVnd = priceVnd;
            Stock = stock;
            Status = status;
            Description = description;
            Url = url ?? string.Empty;
        }
    }
}