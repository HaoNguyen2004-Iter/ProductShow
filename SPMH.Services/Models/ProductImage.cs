namespace SPMH.Services.Models
{
    public record ProductImage
    {
        public required string Url { get; init; }
        public string? Alt { get; init; }
    }
}
