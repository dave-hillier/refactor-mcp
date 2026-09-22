using System;
using System.Linq;
using System.Reflection;
using System.Threading;

/// <summary>
/// Metadata for one parameter of a refactoring tool. Injected parameters
/// (<see cref="CancellationToken"/>, <see cref="IProgress{T}"/>) are supplied by
/// the host rather than the caller and so are excluded from the CLI surface.
/// </summary>
internal sealed class ToolParameterDescriptor
{
    public ToolParameterDescriptor(ParameterInfo parameter)
    {
        Name = parameter.Name ?? string.Empty;
        ParameterType = parameter.ParameterType;
        HasDefaultValue = parameter.HasDefaultValue;
        DefaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null;
        Description = parameter.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .OfType<System.ComponentModel.DescriptionAttribute>()
            .FirstOrDefault()?.Description;
    }

    public string Name { get; }

    public Type ParameterType { get; }

    public string? Description { get; }

    public bool HasDefaultValue { get; }

    public object? DefaultValue { get; }

    /// <summary>True when the host supplies the value, not the caller.</summary>
    public bool IsInjected => ParameterType == typeof(CancellationToken) || IsProgress();

    /// <summary>True when a caller must supply a value.</summary>
    public bool IsRequired => !IsInjected && !HasDefaultValue;

    /// <summary>Short type name for help output, e.g. <c>string[]</c>.</summary>
    public string TypeName => FriendlyName(ParameterType);

    private bool IsProgress()
    {
        var type = ParameterType;
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IProgress<>);
    }

    internal static string FriendlyName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return FriendlyName(underlying) + "?";

        if (!type.IsArray)
            return Aliases.TryGetValue(type, out var alias) ? alias : type.Name;

        return FriendlyName(type.GetElementType()!) + "[]";
    }

    private static readonly System.Collections.Generic.Dictionary<Type, string> Aliases = new()
    {
        [typeof(string)] = "string",
        [typeof(bool)] = "bool",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(double)] = "double",
        [typeof(CancellationToken)] = "cancellationToken",
    };
}
