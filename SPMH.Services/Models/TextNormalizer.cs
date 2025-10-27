using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SPMH.Services.Utils
{
    public static class TextNormalizer
    {
        public static string ToAsciiKeyword(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark &&
                    cat != UnicodeCategory.SpacingCombiningMark &&
                    cat != UnicodeCategory.EnclosingMark)
                    sb.Append(ch);
            }

            var noMarks = sb.ToString().Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd').Replace('Đ', 'D');
            return Regex.Replace(noMarks, @"\s+", " ").Trim().ToLowerInvariant();
        }
    }
}