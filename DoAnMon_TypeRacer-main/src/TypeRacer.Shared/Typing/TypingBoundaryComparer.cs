namespace TypeRacer.Shared.Typing;

public static class TypingBoundaryComparer
{
    public static bool HasCompletedSegmentMismatch(string passage, string typed)
    {
        passage ??= string.Empty;
        typed ??= string.Empty;

        var completedLength = GetCompletedComparableLength(typed, passage);
        var lengthToCompare = Math.Min(completedLength, passage.Length);
        for (var i = 0; i < lengthToCompare; i++)
        {
            if (typed[i] != passage[i])
                return true;
        }

        return completedLength > passage.Length;
    }

    public static bool HasPendingActiveWordMismatch(string passage, string typed)
    {
        passage ??= string.Empty;
        typed ??= string.Empty;

        return typed.Length >= passage.Length &&
               !string.Equals(typed, passage, StringComparison.Ordinal) &&
               !HasCompletedSegmentMismatch(passage, typed);
    }

    public static int GetCompletedComparableLength(string typed, string passage)
    {
        passage ??= string.Empty;
        typed ??= string.Empty;

        if (typed.Length == 0)
            return 0;

        if (typed.Length > passage.Length)
            return typed.Length;

        if (IsWordBoundary(typed[^1]))
            return typed.Length;

        return FindCurrentWordStart(typed);
    }

    private static int FindCurrentWordStart(string typed)
    {
        for (var i = typed.Length - 1; i >= 0; i--)
        {
            if (IsWordBoundary(typed[i]))
                return i + 1;
        }

        return 0;
    }

    private static bool IsWordBoundary(char value)
        => char.IsWhiteSpace(value) || char.IsPunctuation(value) || char.IsSymbol(value);
}
