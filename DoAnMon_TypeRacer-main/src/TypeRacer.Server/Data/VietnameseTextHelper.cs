using System.Globalization;
using System.Text;

namespace TypeRacer.Server.Data;

internal static class VietnameseTextHelper
{
    private static readonly HashSet<char> ToneMarks = new()
    {
        '\u0300', // grave
        '\u0301', // acute
        '\u0303', // tilde
        '\u0309', // hook above
        '\u0323', // dot below
        '\u0302', // circumflex
        '\u0306', // breve
        '\u031B', // horn
    };

    public static bool ContainsVietnameseDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.IndexOf('đ') >= 0 || text.IndexOf('Đ') >= 0)
            return true;

        var normalized = text.Normalize(NormalizationForm.FormD);
        for (var i = 0; i < normalized.Length; i++)
        {
            var c = normalized[i];
            if (!IsVietnameseLatinBase(c))
                continue;

            for (var j = i + 1; j < normalized.Length; j++)
            {
                var mark = normalized[j];
                if (CharUnicodeInfo.GetUnicodeCategory(mark) != UnicodeCategory.NonSpacingMark)
                    break;

                if (ToneMarks.Contains(mark))
                    return true;
            }
        }

        return false;
    }

    private static bool IsVietnameseLatinBase(char c)
    {
        return c is 'A' or 'a' or
            'E' or 'e' or
            'I' or 'i' or
            'O' or 'o' or
            'U' or 'u' or
            'Y' or 'y';
    }
}
