using TypeRacer.Shared.Typing;

namespace TypeRacer.Tests.Shared;

public class TypingBoundaryComparerTests
{
    [Fact]
    public void CompletedSegmentMismatch_IgnoresActiveWordTelexMismatch()
    {
        Assert.False(TypingBoundaryComparer.HasCompletedSegmentMismatch("ê", "e"));
        Assert.False(TypingBoundaryComparer.HasCompletedSegmentMismatch("xin chào", "xin chao"));
    }

    [Fact]
    public void PendingActiveWordMismatch_DetectsFullLengthUncommittedTelexText()
    {
        Assert.True(TypingBoundaryComparer.HasPendingActiveWordMismatch("ê", "e"));
        Assert.True(TypingBoundaryComparer.HasPendingActiveWordMismatch("xin chào", "xin chao"));
        Assert.False(TypingBoundaryComparer.HasPendingActiveWordMismatch("xin chào", "xin chào"));
    }

    [Fact]
    public void CompletedSegmentMismatch_FiresAfterWordBoundary()
    {
        Assert.True(TypingBoundaryComparer.HasCompletedSegmentMismatch("ê", "e "));
        Assert.True(TypingBoundaryComparer.HasCompletedSegmentMismatch("xin chào", "xin chao "));
    }

    [Fact]
    public void CompletedSegmentMismatch_FiresWhenTypingPastPassage()
    {
        Assert.True(TypingBoundaryComparer.HasCompletedSegmentMismatch("abc", "abcd"));
    }
}
