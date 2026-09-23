using System;
using System.Collections.Generic;

namespace RefactorMCP.Tests.Catalog;

/// <summary>
/// Selection and caret markers written into fixture sources. They are removed
/// before the refactoring sees the file, and their positions are reported in
/// the 1-based <c>line:column</c> form the tools accept, with an exclusive end.
/// </summary>
internal sealed class SourceMarkers
{
    public const string SelectionStart = "/*[*/";
    public const string SelectionEnd = "/*]*/";
    public const string Caret = "/*^*/";

    private SourceMarkers(string text, Position? selectionStart, Position? selectionEnd, Position? caret)
    {
        Text = text;
        SelectionStartPosition = selectionStart;
        SelectionEndPosition = selectionEnd;
        CaretPosition = caret;
    }

    /// <summary>The source with every marker removed.</summary>
    public string Text { get; }

    public Position? SelectionStartPosition { get; }

    public Position? SelectionEndPosition { get; }

    public Position? CaretPosition { get; }

    public bool HasMarkers => SelectionStartPosition is not null || CaretPosition is not null;

    /// <summary>The selection as <c>startLine:startColumn-endLine:endColumn</c>.</summary>
    public string SelectionRange(string file)
    {
        if (SelectionStartPosition is not { } start || SelectionEndPosition is not { } end)
            throw new InvalidOperationException($"{file} has no {SelectionStart} ... {SelectionEnd} selection");

        return $"{start}-{end}";
    }

    public Position CaretOrThrow(string file) =>
        CaretPosition ?? throw new InvalidOperationException($"{file} has no {Caret} caret");

    public static SourceMarkers Strip(string text)
    {
        var found = new Dictionary<string, Position>(StringComparer.Ordinal);
        var output = new System.Text.StringBuilder(text.Length);
        var line = 1;
        var column = 1;
        var i = 0;

        while (i < text.Length)
        {
            var marker = MarkerAt(text, i);
            if (marker is not null)
            {
                if (!found.TryAdd(marker, new Position(line, column)))
                    throw new InvalidOperationException($"The marker {marker} appears more than once");

                i += marker.Length;
                continue;
            }

            var c = text[i];
            output.Append(c);
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else if (c != '\r')
            {
                column++;
            }
            i++;
        }

        found.TryGetValue(SelectionStart, out var selectionStart);
        found.TryGetValue(SelectionEnd, out var selectionEnd);
        found.TryGetValue(Caret, out var caret);

        if (found.ContainsKey(SelectionStart) != found.ContainsKey(SelectionEnd))
            throw new InvalidOperationException($"A selection needs both {SelectionStart} and {SelectionEnd}");

        return new SourceMarkers(
            output.ToString(),
            found.ContainsKey(SelectionStart) ? selectionStart : null,
            found.ContainsKey(SelectionEnd) ? selectionEnd : null,
            found.ContainsKey(Caret) ? caret : null);
    }

    private static string? MarkerAt(string text, int index)
    {
        foreach (var marker in new[] { SelectionStart, SelectionEnd, Caret })
        {
            if (string.CompareOrdinal(text, index, marker, 0, marker.Length) == 0)
                return marker;
        }

        return null;
    }

    internal readonly record struct Position(int Line, int Column)
    {
        public override string ToString() => $"{Line}:{Column}";
    }
}
