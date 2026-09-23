using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RefactorMCP.Tests.Catalog;

/// <summary>
/// One fixture directory under <c>Catalog/</c>: a <c>case.json</c>, the files
/// it starts from in <c>before/</c>, and the files it should end with in
/// <c>after/</c>.
/// </summary>
internal sealed class CatalogCase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private CatalogCase(string id, string directory, CaseDefinition definition)
    {
        Id = id;
        Directory = directory;
        Definition = definition;
    }

    /// <summary><c>tier/refactoring/case</c>, the directory relative to the catalog root.</summary>
    public string Id { get; }

    public string Directory { get; }

    public CaseDefinition Definition { get; }

    public string BeforeDirectory => Path.Combine(Directory, "before");

    public string AfterDirectory => Path.Combine(Directory, "after");

    public bool ExpectsError => Definition.Expect == "error";

    public bool IsUnimplemented => Definition.Status == "unimplemented";

    /// <summary>
    /// The operations to apply in order. A case naming a single refactoring is
    /// one step; a composite run as its recipe lists several.
    /// </summary>
    public IReadOnlyList<CaseStep> Steps =>
        Definition.Steps is { Count: > 0 } steps
            ? steps
            : new[] { new CaseStep(Definition.Refactoring!, Definition.Target, Definition.Arguments) };

    public static CatalogCase Load(string catalogRoot, string id)
    {
        var directory = Path.Combine(catalogRoot, id);
        var json = File.ReadAllText(Path.Combine(directory, "case.json"));
        var definition = JsonSerializer.Deserialize<CaseDefinition>(json, JsonOptions)
            ?? throw new InvalidDataException($"{id}/case.json is empty");

        definition.Validate(id);
        return new CatalogCase(id, directory, definition);
    }

    /// <summary>Every case under the catalog root, by id.</summary>
    public static IEnumerable<string> Discover(string catalogRoot)
    {
        return System.IO.Directory
            .EnumerateFiles(catalogRoot, "case.json", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(catalogRoot, Path.GetDirectoryName(file)!).Replace('\\', '/'))
            .OrderBy(id => id, StringComparer.Ordinal);
    }

    public override string ToString() => Id;
}

internal sealed class CaseDefinition
{
    public string? Refactoring { get; init; }

    public string? Description { get; init; }

    public CaseTarget? Target { get; init; }

    public Dictionary<string, JsonElement>? Arguments { get; init; }

    public List<CaseStep>? Steps { get; init; }

    public string Expect { get; init; } = "success";

    public string? ErrorCode { get; init; }

    public string? ErrorContains { get; init; }

    public string? Status { get; init; }

    public ProjectSettings? Project { get; init; }

    public List<ProjectDefinition>? Projects { get; init; }

    public void Validate(string id)
    {
        if (Refactoring is null)
            throw new InvalidDataException($"{id}/case.json has no 'refactoring'");

        if (Expect is not ("success" or "error"))
            throw new InvalidDataException($"{id}/case.json 'expect' must be 'success' or 'error', not '{Expect}'");

        if (Expect == "error" && ErrorCode is null && ErrorContains is null)
            throw new InvalidDataException($"{id}/case.json expects an error but names no 'errorCode' or 'errorContains'");

        if (Status is not (null or "unimplemented"))
            throw new InvalidDataException($"{id}/case.json 'status' must be 'unimplemented' when present, not '{Status}'");

        if (Steps is { Count: > 0 } && Target is not null)
            throw new InvalidDataException($"{id}/case.json has 'steps', so each step carries its own 'target'");
    }
}

internal sealed record CaseStep(
    string Refactoring,
    CaseTarget? Target,
    Dictionary<string, JsonElement>? Arguments);

/// <summary>
/// Where a step applies. <see cref="Selection"/> set to <c>marker</c> uses the
/// <c>/*[*/ ... /*]*/</c> markers in <see cref="File"/>, <see cref="Caret"/>
/// set to <c>marker</c> uses <c>/*^*/</c>, and <see cref="Symbol"/> is a
/// documentation comment id.
/// </summary>
internal sealed record CaseTarget(
    string? File,
    string? Symbol,
    string? Selection,
    string? Caret,
    string? Range);

internal sealed record ProjectSettings(
    [property: JsonPropertyName("langVersion")] string? LangVersion,
    [property: JsonPropertyName("nullable")] string? Nullable);

/// <summary>
/// A project in a multi-project case. Its files live in <c>before/&lt;name&gt;/</c>.
/// </summary>
internal sealed record ProjectDefinition(
    string Name,
    List<string>? References,
    ProjectSettings? Settings);
