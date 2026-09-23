using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>A member of generated code: its name and its type as written in the file.</summary>
internal sealed record GeneratedValue(string Name, string Type);

/// <summary>
/// Source for the constructor, properties and value-equality members the
/// compiler generates for records and anonymous types, so a conversion to a
/// plain class can keep their behaviour. Members are written as text without
/// indentation and parsed with <see cref="Parse"/>.
/// </summary>
internal static class GeneratedMembers
{
    /// <summary>A constructor assigning each parameter to the property it is named after.</summary>
    public static string Constructor(string typeName, IReadOnlyList<GeneratedValue> properties)
    {
        var parameters = string.Join(", ", properties.Select(p => $"{p.Type} {ParameterName(p.Name)}"));
        var text = new StringBuilder();
        text.AppendLine($"public {typeName}({parameters})");
        text.AppendLine("{");
        foreach (var property in properties)
            text.AppendLine($"    {Assignment(property.Name)}");
        text.Append('}');
        return text.ToString();
    }

    public static string Property(GeneratedValue property, string accessors) =>
        $"public {property.Type} {property.Name} {{ {accessors} }}";

    public static string Deconstruct(IReadOnlyList<GeneratedValue> properties)
    {
        var parameters = string.Join(", ", properties.Select(p => $"out {p.Type} {ParameterName(p.Name)}"));
        var text = new StringBuilder();
        text.AppendLine($"public void Deconstruct({parameters})");
        text.AppendLine("{");
        foreach (var property in properties)
            text.AppendLine($"    {Assignment(property.Name, fromParameter: false)}");
        text.Append('}');
        return text.ToString();
    }

    /// <summary>
    /// <c>Equals(T other)</c> comparing every field with its default
    /// comparer, as records do. A type that can be derived from also compares
    /// runtime types, standing in for the record's equality contract.
    /// </summary>
    public static string TypedEquals(string typeName, IReadOnlyList<GeneratedValue> fields, bool isSealed, bool annotate)
    {
        var conditions = new List<string> { "other is not null" };
        if (!isSealed)
            conditions.Add("GetType() == other.GetType()");
        conditions.AddRange(fields.Select(f => $"EqualityComparer<{f.Type}>.Default.Equals({f.Name}, other.{f.Name})"));

        var modifiers = isSealed ? "public" : "public virtual";
        return $"{modifiers} bool Equals({typeName}{Nullable(annotate)} other)\n{{\n    return {string.Join("\n        && ", conditions)};\n}}";
    }

    public static string ObjectEqualsCallingTyped(string typeName, bool annotate) =>
        $"public override bool Equals(object{Nullable(annotate)} obj) => Equals(obj as {typeName});";

    /// <summary><c>Equals(object)</c> for a sealed type with no typed overload, as anonymous types have.</summary>
    public static string ObjectEquals(string typeName, IReadOnlyList<GeneratedValue> fields, bool annotate)
    {
        var conditions = new List<string> { $"obj is {typeName} other" };
        conditions.AddRange(fields.Select(f => $"EqualityComparer<{f.Type}>.Default.Equals({f.Name}, other.{f.Name})"));
        return $"public override bool Equals(object{Nullable(annotate)} obj)\n{{\n    return {string.Join("\n        && ", conditions)};\n}}";
    }

    public static string GetHashCodeMethod(IReadOnlyList<GeneratedValue> fields)
    {
        if (fields.Count == 0)
            return "public override int GetHashCode() => 0;";

        if (fields.Count <= 8)
            return $"public override int GetHashCode() => HashCode.Combine({string.Join(", ", fields.Select(f => f.Name))});";

        var text = new StringBuilder();
        text.AppendLine("public override int GetHashCode()");
        text.AppendLine("{");
        text.AppendLine("    var hash = new HashCode();");
        foreach (var field in fields)
            text.AppendLine($"    hash.Add({field.Name});");
        text.AppendLine("    return hash.ToHashCode();");
        text.Append('}');
        return text.ToString();
    }

    /// <summary>
    /// <c>ToString</c> in the format records and anonymous types use:
    /// <c>Name { A = 1, B = 2 }</c>, or <c>{ A = 1 }</c> with no name.
    /// </summary>
    public static string ToStringMethod(string? typeName, IReadOnlyList<GeneratedValue> printed)
    {
        var prefix = typeName is null ? "" : typeName + " ";
        var members = string.Join(", ", printed.Select(p => $"{p.Name} = {{{p.Name}}}"));
        var body = printed.Count == 0 ? "{{ }}" : $"{{{{ {members} }}}}";
        return $"public override string ToString() => $\"{prefix}{body}\";";
    }

    public static IEnumerable<string> EqualityOperators(string typeName, bool annotate)
    {
        var parameter = typeName + Nullable(annotate);
        yield return $"public static bool operator ==({parameter} left, {parameter} right) => left is null ? right is null : left.Equals(right);";
        yield return $"public static bool operator !=({parameter} left, {parameter} right) => !(left == right);";
    }

    /// <summary>
    /// Parses a generated member, indenting every line and putting a blank
    /// line before it, in the file's own line endings.
    /// </summary>
    public static MemberDeclarationSyntax Parse(string text, string indentation, SyntaxTrivia newLine, bool blankLineBefore = true)
    {
        var eol = newLine.ToString();
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.Length == 0 ? l : indentation + l);
        var source = (blankLineBefore ? eol : "") + string.Join(eol, lines) + eol;
        return SyntaxFactory.ParseMemberDeclaration(source)
            ?? throw new InvalidOperationException($"Generated code does not parse: {text}");
    }

    /// <summary>The camel-case parameter name for a property, escaped when it is a keyword.</summary>
    public static string ParameterName(string propertyName)
    {
        var name = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }

    /// <summary><c>Name = name;</c>, qualified with <c>this.</c> when the two names are the same.</summary>
    private static string Assignment(string propertyName, bool fromParameter = true)
    {
        var parameter = ParameterName(propertyName);
        var property = parameter == propertyName ? "this." + propertyName : propertyName;
        return fromParameter ? $"{property} = {parameter};" : $"{parameter} = {property};";
    }

    private static string Nullable(bool annotate) => annotate ? "?" : "";

    /// <summary>The indentation of a type's members: that of its first member, or one level inside the type.</summary>
    public static string MemberIndentation(TypeDeclarationSyntax type)
    {
        if (type.Members.Count > 0)
            return Indentation(type.Members[0].GetFirstToken());

        return Indentation(type.GetFirstToken()) + "    ";
    }

    public static string Indentation(SyntaxToken token) =>
        token.LeadingTrivia.LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia)).ToString();
}
