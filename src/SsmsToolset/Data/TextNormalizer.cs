using System.Globalization;
using System.Text;

namespace SsmsToolset.Data
{
    /// <summary>
    /// Normalizes text for forgiving, accent-insensitive search: lower-cases and
    /// strips diacritics, so "é" matches "e", "Ç" matches "c", etc.
    /// </summary>
    public static class TextNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Decompose accented chars into base char + combining mark, then drop the marks.
            string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
