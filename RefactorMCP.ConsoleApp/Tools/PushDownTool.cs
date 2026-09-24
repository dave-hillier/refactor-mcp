using ModelContextProtocol.Server;
using ModelContextProtocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using System.ComponentModel;
using static HierarchyMemberHelpers;

[McpServerToolType]
public static class PushDownTool
{
    [McpServerTool, Description("Move a field from a class into the subclasses that use it, or into every subclass when none does")]
    public static async Task<string> PushDownField(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the field")] string filePath,
        [Description("Name of the class declaring the field")] string className,
        [Description("Name of the field to push down")] string fieldName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var target = await FindFieldAsync(document, className, fieldName, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;

            EnsureNoPrivateUse(model, target.Symbol, target.ContainingType, target.Field.Declaration.Type, target.Variable);
            var receivers = await ReceiversAsync(solution, target.Symbol, target.ContainingType, target.Field, cancellationToken);

            var edits = new TrackedEdits(solution);
            RemoveField(edits, document, target.Type, target.Field, target.Variable);
            foreach (var subclass in receivers)
            {
                EnsureSubclassLacks(subclass.Symbol, fieldName, _ => true);
                var subclassModel = (await subclass.Document.GetSemanticModelAsync(cancellationToken))!;
                var map = TowardsSubclass(subclass.Symbol, subclassModel, subclass.Declaration.SpanStart);
                var substituted = Substitute(target.Field, model, map);
                var moved = SingleVariable(substituted, substituted.Declaration.Variables.First(v => v.Identifier.ValueText == fieldName));
                moved = NarrowForSealed(moved, subclass.Symbol);

                edits.Replace(subclass.Document, subclass.Declaration, t => InsertMember(t, moved, TypeRefactoringHelpers.EndOfLine(t.SyntaxTree.GetRoot())));
                edits.Import(subclass.Document, TypeRefactoringHelpers.NamespacesUsedBy(target.Field.Declaration.Type, model)
                    .Concat(TypeRefactoringHelpers.NamespacesUsedBy(target.Variable, model)));
            }

            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                await edits.ApplyAsync(cancellationToken),
                errors => $"Error: Pushing down {fieldName} would break the build: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return $"Successfully pushed down {fieldName} from {className} to {string.Join(", ", receivers.Select(r => r.Symbol.Name))}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error pushing down field: {ex.Message}", ex);
        }
    }

    [McpServerTool, Description("Move a method from a class into the subclasses that use it, or remove an abstract declaration and keep the subclasses' implementations")]
    public static async Task<string> PushDownMethod(
        [Description("Absolute path to the solution file (.sln or .slnx)")] string solutionPath,
        [Description("Path to the C# file declaring the method")] string filePath,
        [Description("Name of the class declaring the method")] string className,
        [Description("Name of the method to push down")] string methodName,
        [Description("Line of the method's declaration, to choose between overloads (optional)")] int? line = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (solution, document) = await TypeRefactoringHelpers.LoadDocumentAsync(solutionPath, filePath, cancellationToken);
            var target = await FindMethodAsync(document, className, methodName, line, cancellationToken);
            var model = (await document.GetSemanticModelAsync(cancellationToken))!;
            var symbol = target.Symbol;

            if (symbol.IsOverride)
                throw new McpException($"Error: {methodName} overrides {symbol.OverriddenMethod!.ContainingType.Name}.{methodName}, so callers reach it without naming {className}");

            var overrides = (await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: cancellationToken)).ToList();
            if (symbol.IsVirtual && overrides.Count > 0)
                throw new McpException($"Error: {methodName} is overridden in {string.Join(", ", overrides.Select(o => o.ContainingType.Name))}");

            EnsureNoPrivateUse(model, symbol, target.ContainingType, target.Method);
            var receivers = await ReceiversAsync(solution, symbol, target.ContainingType, target.Method, cancellationToken);

            var edits = new TrackedEdits(solution);
            edits.RemoveMember(document, target.Type, target.Method);

            if (symbol.IsAbstract)
            {
                // Each implementation stays where it is and stops overriding.
                foreach (var implementation in overrides)
                {
                    var reference = implementation.DeclaringSyntaxReferences.First();
                    var implementationDocument = solution.GetDocument(reference.SyntaxTree)!;
                    var declaration = (MethodDeclarationSyntax)await reference.GetSyntaxAsync(cancellationToken);
                    var overridden = (await SymbolFinder.FindOverridesAsync(implementation, solution, cancellationToken: cancellationToken)).Any();
                    edits.Replace(implementationDocument, declaration, m => WithModifiers(m, modifiers =>
                    {
                        var without = WithoutModifiers(modifiers, SyntaxKind.OverrideKeyword, SyntaxKind.SealedKeyword);
                        return overridden ? WithModifier(without, SyntaxKind.VirtualKeyword) : without;
                    }));
                }
            }
            else
            {
                foreach (var subclass in receivers)
                {
                    EnsureSubclassLacks(subclass.Symbol, methodName, member => member is not IMethodSymbol other || SameParameters(other, symbol, subclass.Symbol));
                    var subclassModel = (await subclass.Document.GetSemanticModelAsync(cancellationToken))!;
                    var map = TowardsSubclass(subclass.Symbol, subclassModel, subclass.Declaration.SpanStart);
                    var moved = NarrowForSealed(Substitute(target.Method, model, map), subclass.Symbol);

                    edits.Replace(subclass.Document, subclass.Declaration, t => InsertMember(t, moved, TypeRefactoringHelpers.EndOfLine(t.SyntaxTree.GetRoot())));
                    edits.Import(subclass.Document, TypeRefactoringHelpers.NamespacesUsedBy(target.Method, model));
                }
            }

            await TypeRefactoringHelpers.ApplyIfCompilesAsync(
                solution,
                await edits.ApplyAsync(cancellationToken),
                errors => $"Error: Pushing down {methodName} would break the build: {TypeRefactoringHelpers.Describe(errors)}",
                cancellationToken);

            return symbol.IsAbstract
                ? $"Successfully removed the abstract {methodName} from {className}"
                : $"Successfully pushed down {methodName} from {className} to {string.Join(", ", receivers.Select(r => r.Symbol.Name))}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error pushing down method: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The direct subclasses that need the member: those whose code, or whose
    /// callers, use it. Every direct subclass when nothing uses it. Refuses
    /// when the class itself uses it, or code reaches it through a reference
    /// of the class's type rather than a subclass's.
    /// </summary>
    private static async Task<IReadOnlyList<SourceType>> ReceiversAsync(
        Solution solution,
        ISymbol member,
        INamedTypeSymbol owner,
        SyntaxNode declaration,
        CancellationToken cancellationToken)
    {
        var subclasses = await SubclassesAsync(solution, owner, transitive: false, cancellationToken);
        if (subclasses.Count == 0)
            throw new McpException($"Error: {owner.Name} has no subclasses to push {member.Name} down to");

        var users = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var references = await SymbolFinder.FindReferencesAsync(member, solution, cancellationToken);
        foreach (var location in references
            .Where(r => SymbolEqualityComparer.Default.Equals(r.Definition.OriginalDefinition, member.OriginalDefinition))
            .SelectMany(r => r.Locations))
        {
            var root = await location.Document.GetSyntaxRootAsync(cancellationToken);
            var node = root!.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (declaration.SyntaxTree == node.SyntaxTree && declaration.Span.Contains(node.Span))
                continue;

            var model = (await location.Document.GetSemanticModelAsync(cancellationToken))!;
            var enclosing = node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
            var enclosingType = model.GetDeclaredSymbol(enclosing, cancellationToken)!;
            if (Within(enclosingType, owner))
                throw new McpException($"Error: {owner.Name} itself uses {member.Name}, which cannot move to the subclasses");

            var receiver = ReceiverType(node, model, enclosingType);
            var subclass = DirectSubclassOf(receiver, owner);
            if (subclass is null)
            {
                var line = location.Location.GetLineSpan().StartLinePosition.Line + 1;
                throw new McpException(
                    $"Error: Code in {Path.GetFileName(location.Document.FilePath)}({line}) reaches {member.Name} through {receiver?.ToDisplayString() ?? "an unknown type"}, which would no longer have it");
            }

            users.Add(subclass);
        }

        return users.Count == 0
            ? subclasses
            : subclasses.Where(s => users.Contains(s.Symbol)).ToList();
    }

    /// <summary>The type of the expression a member is reached through, or the enclosing type when it is used unqualified.</summary>
    private static ITypeSymbol? ReceiverType(SyntaxNode node, SemanticModel model, INamedTypeSymbol enclosingType) => node.Parent switch
    {
        MemberAccessExpressionSyntax access when access.Name == node => model.GetTypeInfo(access.Expression).Type,
        MemberBindingExpressionSyntax binding when binding.Parent?.Parent is ConditionalAccessExpressionSyntax conditional =>
            model.GetTypeInfo(conditional.Expression).Type,
        AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax creation } } assignment
            when assignment.Left == node => model.GetTypeInfo(creation).Type,
        _ => enclosingType,
    };

    /// <summary>The direct subclass of <paramref name="owner"/> that <paramref name="type"/> is or derives from.</summary>
    private static INamedTypeSymbol? DirectSubclassOf(ITypeSymbol? type, INamedTypeSymbol owner)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.BaseType?.OriginalDefinition, owner.OriginalDefinition))
                return current.OriginalDefinition;
        }

        return null;
    }

    private static bool Within(INamedTypeSymbol type, INamedTypeSymbol owner)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, owner.OriginalDefinition))
                return true;
        }

        return false;
    }

    /// <summary>Refuses when the member uses something private to its class, which the subclasses cannot see.</summary>
    private static void EnsureNoPrivateUse(SemanticModel model, ISymbol member, INamedTypeSymbol owner, params SyntaxNode[] nodes)
    {
        foreach (var name in nodes.SelectMany(n => n.DescendantNodesAndSelf().OfType<SimpleNameSyntax>()))
        {
            var symbol = model.GetSymbolInfo(name).Symbol;
            if (symbol is { DeclaredAccessibility: Accessibility.Private, ContainingType: { } containing }
                && symbol is not ILocalSymbol and not IParameterSymbol and not IRangeVariableSymbol
                && !SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, member.OriginalDefinition)
                && SymbolEqualityComparer.Default.Equals(containing.OriginalDefinition, owner.OriginalDefinition))
            {
                throw new McpException($"Error: {member.Name} uses {symbol.Name}, which is private to {owner.Name}");
            }
        }
    }

    private static void EnsureSubclassLacks(INamedTypeSymbol subclass, string name, Func<ISymbol, bool> clashes)
    {
        if (subclass.GetMembers(name).Any(m => !m.IsImplicitlyDeclared && clashes(m)))
            throw new McpException($"Error: {subclass.Name} already has a member named {name}");
    }

    /// <summary>Whether a subclass's method has the parameters the pushed-down one will have there.</summary>
    private static bool SameParameters(IMethodSymbol existing, IMethodSymbol pushed, INamedTypeSymbol subclass)
    {
        var asSeen = subclass.BaseType!.GetMembers(pushed.Name).OfType<IMethodSymbol>()
            .First(m => SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, pushed.OriginalDefinition));
        return existing.Parameters.Length == asSeen.Parameters.Length
            && existing.Parameters.Zip(asSeen.Parameters).All(p =>
                p.First.RefKind == p.Second.RefKind && SymbolEqualityComparer.Default.Equals(p.First.Type, p.Second.Type));
    }

    /// <summary>
    /// In a sealed class nothing derives, so protected becomes private and a
    /// method cannot be virtual.
    /// </summary>
    private static T NarrowForSealed<T>(T member, INamedTypeSymbol subclass)
        where T : MemberDeclarationSyntax
    {
        if (!subclass.IsSealed)
            return member;

        return WithModifiers(member, modifiers =>
        {
            var access = modifiers.Where(m => SyntaxFacts.IsAccessibilityModifier(m.Kind())).ToList();
            if (access.Count > 0 && access.All(m => m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.PrivateKeyword)))
            {
                var index = modifiers.IndexOf(access[0]);
                modifiers = WithoutModifiers(modifiers, SyntaxKind.ProtectedKeyword, SyntaxKind.PrivateKeyword)
                    .Insert(index, SyntaxFactory.Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            }

            return WithoutModifiers(modifiers, SyntaxKind.VirtualKeyword);
        });
    }
}
