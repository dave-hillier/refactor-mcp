using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[McpServerToolType]
public static class AddParameterDefaultValueTool
{
    [McpServerTool, Description("Give a method parameter a default value, on its overrides and implementations too, optionally dropping arguments that pass the same value")]
    public static async Task<string> AddParameterDefaultValue(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the method, or of the type for a constructor")] string methodName,
        [Description("Name of the parameter to give a default")] string parameterName,
        [Description("The default value, a C# constant expression")] string value,
        [Description("Drop arguments that pass the default value from calls")] bool removeFromCallSites = false,
        [Description("A line of the method's declaration (1-based), to choose between overloads")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
            var method = await SolutionEdits.FindMethodAsync(solution, filePath, methodName, line, cancellationToken);
            var parameter = SolutionEdits.FindParameter(method, parameterName);
            EnsureCanBeOptional(method, parameter);

            var defaultValue = SyntaxFactory.ParseExpression(value);
            var declared = await SignatureChange.ApplyAsync(
                solution,
                method,
                Slots(method, parameter, defaultValue, omitArgument: null),
                cancellationToken);
            await EnsureValidDefaultAsync(solution, declared, parameter, value, cancellationToken);

            var changed = declared;
            if (removeFromCallSites)
            {
                var declaredMethod = await SolutionEdits.ResolveAsync(
                    declared,
                    SolutionEdits.ProjectOf(solution, method),
                    method,
                    cancellationToken);
                var constant = declaredMethod.Parameters[parameter.Ordinal].ExplicitDefaultValue;
                changed = await SignatureChange.ApplyAsync(
                    solution,
                    method,
                    Slots(method, parameter, defaultValue, (site, argument) => PassesConstant(site, argument, constant)),
                    cancellationToken);
            }

            await SolutionEdits.EnsureCompilesAsync(solution, changed, cancellationToken);
            await SolutionEdits.WriteAsync(solution, changed, cancellationToken);

            return $"Successfully gave parameter '{parameterName}' of '{methodName}' the default value {value}";
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new McpException($"Error adding default value: {ex.Message}", ex);
        }
    }

    private static void EnsureCanBeOptional(IMethodSymbol method, IParameterSymbol parameter)
    {
        if (parameter.HasExplicitDefaultValue)
            throw new McpException(
                $"Error: Parameter '{parameter.Name}' already has a default value; changing it would change what calls that leave it out pass");

        if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.IsThis)
            throw new McpException(
                $"Error: Parameter '{parameter.Name}' is a ref, out, in, params or this parameter, which cannot have a default value");

        var required = method.Parameters
            .Skip(parameter.Ordinal + 1)
            .FirstOrDefault(p => !p.HasExplicitDefaultValue && !p.IsParams);
        if (required is not null)
            throw new McpException(
                $"Error: Parameter '{required.Name}' after '{parameter.Name}' has no default value, so '{parameter.Name}' cannot have one");
    }

    private static List<ParameterSlot> Slots(
        IMethodSymbol method,
        IParameterSymbol parameter,
        ExpressionSyntax defaultValue,
        Func<SignatureCallSite, ArgumentSyntax, bool>? omitArgument) =>
        method.Parameters
            .Select(p => p.Ordinal == parameter.Ordinal
                ? ParameterSlot.Existing(p.Ordinal, syntax => WithDefault(syntax, defaultValue), omitArgument)
                : ParameterSlot.Existing(p.Ordinal))
            .ToList();

    /// <summary>Adds <c>= value</c> after the name, before any comment that followed it.</summary>
    private static ParameterSyntax WithDefault(ParameterSyntax parameter, ExpressionSyntax value)
    {
        var identifier = parameter.Identifier;
        var clause = SyntaxFactory.EqualsValueClause(
                SyntaxFactory.Token(SyntaxKind.EqualsToken)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                value.WithoutTrivia())
            .WithTrailingTrivia(identifier.TrailingTrivia);
        return parameter.WithIdentifier(identifier.WithTrailingTrivia()).WithDefault(clause);
    }

    /// <summary>The compiler checks the value; its complaints are named here.</summary>
    private static async Task EnsureValidDefaultAsync(
        Solution solution,
        Solution declared,
        IParameterSymbol parameter,
        string value,
        CancellationToken cancellationToken)
    {
        var errors = await SolutionEdits.NewDiagnosticsAsync(
            solution,
            declared,
            d => d.Severity == DiagnosticSeverity.Error,
            cancellationToken);

        if (errors.Any(e => e.Id == "CS1736"))
            throw new McpException($"Error: The default value '{value}' is not a compile-time constant");

        if (errors.Any(e => e.Id is "CS1750" or "CS0029" or "CS0266" or "CS1763"))
            throw new McpException(
                $"Error: The default value '{value}' cannot be converted to the type of '{parameter.Name}', {parameter.Type.ToDisplayString()}");

        if (errors.Count > 0)
            throw new McpException($"Error: The change would not compile: {SolutionEdits.Describe(errors[0])}");
    }

    private static bool PassesConstant(SignatureCallSite site, ArgumentSyntax argument, object? constant)
    {
        var passed = site.Model.GetConstantValue(argument.Expression);
        return passed.HasValue && ConstantValues.Same(passed.Value, constant);
    }
}
