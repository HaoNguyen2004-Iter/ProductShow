namespace SPMH.Services.Executes.Products
{
    public record ExportFileResult(byte[] Content, string ContentType, string FileName);
}