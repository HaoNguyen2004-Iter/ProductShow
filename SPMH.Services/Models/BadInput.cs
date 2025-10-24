using System.Text.RegularExpressions;

namespace SPMH.Services.Models
{
    public static class BadInput
    {
        private static readonly Regex badInput = new(
            @"\b(?:CREATE|DROP|ALTER|TRUNCATE|RENAME|INSERT|UPDATE|DELETE|MERGE|UPSERT|BULK\s+INSERT|COPY|KILL|SHUTDOWN|UNION|ALL|SELECT)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool hasBadInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            return badInput.IsMatch(input);
        }

    }
}
