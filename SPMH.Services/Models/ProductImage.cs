namespace SPMH.Services.Models
{
    public record ProductImage
    {
        public required string Url { get; init; }
        public string? Alt { get; init; }
        public required string OriginalFileName { get; set; }
    }
}
