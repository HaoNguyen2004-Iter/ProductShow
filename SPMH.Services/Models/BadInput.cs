using System.Text.RegularExpressions;

namespace SPMH.Services.Models
{
    public static class BadInput
    {
        private static readonly Regex badInput = new(
            @"\b(?:CREATE|DROP|ALTER|TRUNCATE|RENAME|INSERT|UPDATE|DELETE|MERGE|UPSERT|BULK\s+INSERT|COPY|KILL|SHUTDOWN|UNION|ALL|SELECT)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static bool hasBadInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return badInput.IsMatch(input);
        }

        public static void EnsureSafe(string input)
        {
            if (hasBadInput(input))
                throw new ArgumentException("tồn tại từ khóa không hợp lệ");
        }

        public static void EnsureSafe(ProductFilter? f)
        {
            if (f == null) return;
            if (hasBadInput(f.Code) || hasBadInput(f.Brand) || hasBadInput(f.Name))
                throw new ArgumentException("Tồn tại từ khóa không hợp lệ");
        }
    }
}
