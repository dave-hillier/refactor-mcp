using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// A small hand rolled argument reader for the command line surface:
/// <c>refactor extract-method --solution App.sln --file src/Foo.cs</c>.
///
/// Options gather every value up to the next <c>--option</c>, so a flag that
/// takes a list (<c>--method-names A B</c>) works without a schema. Anything
/// that is not an option is positional. <c>--option=value</c> is accepted too.
/// </summary>
internal sealed class ParsedCommand
{
    private readonly Dictionary<string, string[]> _options;

    private ParsedCommand(string verb, Dictionary<string, string[]> options, IReadOnlyList<string> positionals)
    {
        Verb = verb;
        _options = options;
        Positionals = positionals;
    }

    public string Verb { get; }

    public IReadOnlyList<string> Positionals { get; }

    public static ParsedCommand Parse(string[] args)
    {
        var options = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        var verb = string.Empty;

        var index = 0;
        if (args.Length > 0 && !args[0].StartsWith('-'))
        {
            verb = args[0];
            index = 1;
        }

        while (index < args.Length)
        {
            var token = args[index];
            index++;

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            var name = token[2..];
            string? value = null;

            // --name=value is one token, --name value takes the next one. An
            // option takes a single value so that the arguments after it stay
            // positional; repeat the option (or use commas) for lists.
            var separator = name.IndexOf('=');
            if (separator >= 0)
            {
                value = name[(separator + 1)..];
                name = name[..separator];
            }
            else if (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[index];
                index++;
            }

            var values = options.TryGetValue(name, out var existing) ? existing.ToList() : new List<string>();
            if (value is not null)
                values.Add(value);

            options[name] = values.ToArray();
        }

        return new ParsedCommand(verb, options, positionals);
    }

    public bool HasOption(string name) => _options.ContainsKey(name);

    /// <summary>Every value supplied for an option, or an empty list.</summary>
    public IReadOnlyList<string> OptionValues(string name)
        => _options.TryGetValue(name, out var values) ? values : Array.Empty<string>();

    /// <summary>The first value supplied for an option, or null.</summary>
    public string? Option(string name)
        => _options.TryGetValue(name, out var values) && values.Length > 0 ? values[0] : null;

    /// <summary>All options, for tools that bind their parameters by name.</summary>
    public IReadOnlyDictionary<string, string[]> Options => _options;

    /// <summary>
    /// Reads an option that names a length of time: <c>30s</c>, <c>10m</c>,
    /// <c>2h</c>, <c>250ms</c> or a plain number of seconds. Zero disables the
    /// timeout it applies to.
    /// </summary>
    public static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();

        if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            return FromNumber(text[..^2], TimeSpan.FromMilliseconds(1));
        if (text.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return FromNumber(text[..^1], TimeSpan.FromSeconds(1));
        if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
            return FromNumber(text[..^1], TimeSpan.FromMinutes(1));
        if (text.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            return FromNumber(text[..^1], TimeSpan.FromHours(1));

        // A bare number is a number of seconds.
        return FromNumber(text, TimeSpan.FromSeconds(1));
    }

    private static TimeSpan? FromNumber(string text, TimeSpan unit)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number * unit
            : null;
}
