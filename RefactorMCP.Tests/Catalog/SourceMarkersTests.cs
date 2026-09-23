using System;
using Xunit;

namespace RefactorMCP.Tests.Catalog;

public class SourceMarkersTests
{
    [Fact]
    public void Strip_RemovesSelectionMarkersAndReportsAnExclusiveRange()
    {
        var markers = SourceMarkers.Strip("class A\n{\n    void M() { /*[*/int x = 1;/*]*/ }\n}\n");

        Assert.Equal("class A\n{\n    void M() { int x = 1; }\n}\n", markers.Text);
        Assert.Equal("3:16-3:26", markers.SelectionRange("A.cs"));
    }

    [Fact]
    public void Strip_SelectionSpanningLines_CountsColumnsFromTheStrippedText()
    {
        var markers = SourceMarkers.Strip("/*[*/a\r\nbc/*]*/\r\n");

        Assert.Equal("a\r\nbc\r\n", markers.Text);
        Assert.Equal("1:1-2:3", markers.SelectionRange("A.cs"));
    }

    [Fact]
    public void Strip_Caret_ReportsThePositionOfTheFollowingCharacter()
    {
        var markers = SourceMarkers.Strip("x\n  /*^*/if (a) { }\n");

        Assert.Equal("x\n  if (a) { }\n", markers.Text);
        Assert.Equal(new SourceMarkers.Position(2, 3), markers.CaretOrThrow("A.cs"));
    }

    [Fact]
    public void Strip_NoMarkers_LeavesTextAlone()
    {
        var markers = SourceMarkers.Strip("class A { }\n");

        Assert.Equal("class A { }\n", markers.Text);
        Assert.False(markers.HasMarkers);
        Assert.Throws<InvalidOperationException>(() => markers.SelectionRange("A.cs"));
    }

    [Fact]
    public void Strip_UnclosedSelection_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => SourceMarkers.Strip("/*[*/class A { }"));
    }

    [Fact]
    public void Strip_RepeatedMarker_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => SourceMarkers.Strip("/*^*/a/*^*/"));
    }
}
