using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class RefactoringHelpersRangeTests
{
    [Fact]
    public void TryParseRange_ValidFormat_ReturnsParsedValues()
    {
        var success = RefactoringHelpers.TryParseRange("2:3-4:5", out var sl, out var sc, out var el, out var ec);

        Assert.True(success);
        Assert.Equal(2, sl);
        Assert.Equal(3, sc);
        Assert.Equal(4, el);
        Assert.Equal(5, ec);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("1:2:3-4:5")]
    [InlineData("1:2-4")]
    public void TryParseRange_InvalidFormat_ReturnsFalse(string range)
    {
        var result = RefactoringHelpers.TryParseRange(range, out _, out _, out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void ValidateRange_NegativeValues_ReturnsError()
    {
        var text = SourceText.From("line1\nline2");
        var valid = RefactoringHelpers.ValidateRange(text, -1, 1, 1, 2, out var error);

        Assert.False(valid);
        Assert.Equal("Error: Range values must be positive", error);
    }

    [Fact]
    public void ValidateRange_ReversedRange_ReturnsError()
    {
        var text = SourceText.From("line1\nline2");
        var valid = RefactoringHelpers.ValidateRange(text, 2, 5, 1, 4, out var error);

        Assert.False(valid);
        Assert.Equal("Error: Range start must precede end", error);
    }

    [Fact]
    public void ValidateRange_ExceedsFileLength_ReturnsError()
    {
        var text = SourceText.From("line1\nline2");
        var valid = RefactoringHelpers.ValidateRange(text, 1, 1, text.Lines.Count + 1, 1, out var error);

        Assert.False(valid);
        Assert.Equal("Error: Range exceeds file length", error);
    }
}
