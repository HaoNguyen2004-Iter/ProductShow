

namespace SPMH.Services.Models
{
    public sealed class ProductFilter
    {
        public string? Code { get; init; }
        public string? Name { get; init; }
        public string? Brand { get; init; }
        public decimal? Price { get; init; }
        public int? Stock { get; init; }
        public int? Status { get; init; }
    }
}
