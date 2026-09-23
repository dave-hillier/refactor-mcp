using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Threading;

[McpServerToolType]
public static class ConvertLocalFunctionToMethodTool
{
    [McpServerTool, Description("Convert a local function to a private method of the containing type; the variables it captures become parameters")]
    public static async Task<string> ConvertLocalFunctionToMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file")] string filePath,
        [Description("Line of the local function's name, or of a call to it (1-based)")] int line,
        [Description("Column of that name (1-based)")] int column,
        [Description("Name for the method (optional, defaults to the local function's name)")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var target = await PositionTarget.FindAsync(solutionPath, filePath, line, column, cancellationToken);
            var function = LocalFunctionAt(target, cancellationToken);
            var conversion = new Conversion(target.Model, function, string.IsNullOrEmpty(name) ? function.Identifier.ValueText : name!, cancellationToken);

            var editor = new SyntaxEditor(target.Root, target.Document.Project.Solution.Services);
            foreach (var reference in conversion.References.Where(r => !function.Span.Contains(r.Span)))
            {
                var original = Conversion.UseOf(reference);
                editor.ReplaceNode(original, (current, _) => conversion.Rewrite(current, original));
            }
            editor.RemoveNode(function, SyntaxRemoveOptions.KeepNoTrivia);
            editor.InsertAfter(conversion.Member, conversion.Method());

            await target.WriteAsync(editor.GetChangedRoot());
            return $"Successfully converted local function '{function.Identifier.ValueText}' to method '{conversion.Name}' in {filePath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error converting local function to method: {ex.Message}", ex);
        }
    }

    /// <summary>The local function declared or called at the position.</summary>
    private static LocalFunctionStatementSyntax LocalFunctionAt(PositionTarget target, CancellationToken cancellationToken)
    {
        if (target.Token.Parent is LocalFunctionStatementSyntax declared && declared.Identifier == target.Token)
            return declared;

        if (target.Token.Parent is SimpleNameSyntax name &&
            target.Model.GetSymbolInfo(name, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } symbol &&
            symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is LocalFunctionStatementSyntax function)
        {
            return function;
        }

        throw new McpException($"Error: There is no local function at {target.Describe()}");
    }

    private sealed class Conversion
    {
        private readonly SemanticModel _model;
        private readonly LocalFunctionStatementSyntax _function;
        private readonly IMethodSymbol _symbol;
        private readonly CancellationToken _cancellationToken;

        // What the function reads from the member around it: variables, which become
        // parameters, and type parameters, which become the method's own.
        private readonly List<ISymbol> _captured;
        private readonly HashSet<ISymbol> _written;
        private readonly List<ITypeParameterSymbol> _typeParameters;
        private readonly bool _explicitTypeArguments;

        public Conversion(SemanticModel model, LocalFunctionStatementSyntax function, string name, CancellationToken cancellationToken)
        {
            _model = model;
            _function = function;
            _symbol = model.GetDeclaredSymbol(function, cancellationToken)!;
            _cancellationToken = cancellationToken;
            Name = name;
            Member = function.Ancestors().OfType<MemberDeclarationSyntax>().First(m => m.Parent is TypeDeclarationSyntax);
            References = Member.DescendantNodes().OfType<SimpleNameSyntax>()
                .Where(n => n.Identifier.ValueText == _symbol.Name && Refers(n, _symbol))
                .ToList();

            var names = function.DescendantNodes().OfType<SimpleNameSyntax>().ToList();
            var outside = names
                .Select(n => (Name: n, Symbol: _model.GetSymbolInfo(n, cancellationToken).Symbol))
                .Where(n => n.Symbol != null && !n.Symbol.DeclaringSyntaxReferences.Any(r => function.Span.Contains(r.Span)))
                .ToList();

            var calledFunction = outside.FirstOrDefault(n => n.Symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction } &&
                                                             !SymbolEqualityComparer.Default.Equals(n.Symbol, _symbol));
            if (calledFunction.Symbol != null)
                throw new McpException($"Error: '{_symbol.Name}' calls local function '{calledFunction.Symbol.Name}', which a method could not reach");

            _captured = outside
                .Select(n => n.Symbol!)
                .Where(s => s is ILocalSymbol || s is IParameterSymbol { IsThis: false })
                .Distinct(SymbolEqualityComparer.Default)
                .OrderBy(s => s.Locations[0].SourceSpan.Start)
                .ToList();
            _written = outside
                .Where(n => _captured.Contains(n.Symbol, SymbolEqualityComparer.Default) && LocalVariableTarget.IsWrite(n.Name))
                .Select(n => n.Symbol!)
                .ToHashSet(SymbolEqualityComparer.Default);
            if (_written.Count > 0 && (_symbol.IsAsync || IsIterator(function)))
            {
                throw new McpException(
                    $"Error: '{_symbol.Name}' is async or an iterator and assigns '{_written.First().Name}', which it would need to take by ref");
            }

            _typeParameters = outside.Select(n => n.Symbol).OfType<ITypeParameterSymbol>()
                .Concat(_captured.SelectMany(s => TypeParametersIn(TypeOf(s))))
                .Where(t => t.TypeParameterKind == TypeParameterKind.Method && !SymbolEqualityComparer.Default.Equals(t.DeclaringMethod, _symbol))
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<ITypeParameterSymbol>()
                .OrderBy(t => t.Ordinal)
                .ToList();

            // Calls leave type arguments to inference only when every one can be inferred.
            var parameterTypes = _symbol.Parameters.Select(p => p.Type).Concat(_captured.Select(TypeOf)).ToList();
            _explicitTypeArguments = _typeParameters.Any(t => !parameterTypes.Any(p => TypeParametersIn(p).Contains(t, SymbolEqualityComparer.Default)));

            if ((_captured.Count > 0 || _typeParameters.Count > 0) &&
                References.Any(r => !(r.Parent is InvocationExpressionSyntax call && call.Expression == r)))
            {
                throw new McpException(
                    $"Error: '{_symbol.Name}' is used as a delegate and captures variables of '{MemberName}', which a method could not be passed with");
            }

            IsStatic = UsesNoInstance();
            EnsureNameIsFree();
        }

        public string Name { get; }

        public MemberDeclarationSyntax Member { get; }

        public List<SimpleNameSyntax> References { get; }

        private bool IsStatic { get; }

        private string MemberName => _model.GetDeclaredSymbol(Member, _cancellationToken)?.Name ?? Member.Kind().ToString();

        private bool Refers(SimpleNameSyntax name, ISymbol symbol) =>
            SymbolEqualityComparer.Default.Equals(_model.GetSymbolInfo(name, _cancellationToken).Symbol?.OriginalDefinition, symbol);

        private ITypeSymbol TypeOf(ISymbol variable)
        {
            var type = variable switch
            {
                ILocalSymbol local => local.Type,
                IParameterSymbol parameter => parameter.Type,
                _ => throw new InvalidOperationException(),
            };

            // A local declared with var is nullable in a nullable context; it is the value
            // it holds where the function reads it that the parameter needs to allow.
            if (variable is ILocalSymbol && type.NullableAnnotation == NullableAnnotation.Annotated &&
                variable.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(_cancellationToken).Parent is VariableDeclarationSyntax { Type.IsVar: true })
            {
                var read = _function.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault(n => Refers(n, variable));
                if (read != null && _model.GetTypeInfo(read, _cancellationToken).Nullability.FlowState == NullableFlowState.NotNull)
                    type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            }

            return type;
        }

        private static bool IsIterator(LocalFunctionStatementSyntax function) =>
            function.DescendantNodes(n => n is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax || n == function)
                .OfType<YieldStatementSyntax>()
                .Any();

        private static IEnumerable<ITypeParameterSymbol> TypeParametersIn(ITypeSymbol type) => type switch
        {
            ITypeParameterSymbol parameter => new[] { parameter },
            IArrayTypeSymbol array => TypeParametersIn(array.ElementType),
            IPointerTypeSymbol pointer => TypeParametersIn(pointer.PointedAtType),
            INamedTypeSymbol named => named.TypeArguments.SelectMany(TypeParametersIn),
            _ => Enumerable.Empty<ITypeParameterSymbol>(),
        };

        /// <summary>
        /// The method can be static unless the function reaches the instance, through
        /// <c>this</c>, <c>base</c> or a simple name for an instance member.
        /// </summary>
        private bool UsesNoInstance()
        {
            if (_model.GetDeclaredSymbol(Member, _cancellationToken) is { IsStatic: true })
                return true;
            if (_function.DescendantNodes().Any(n => n is ThisExpressionSyntax or BaseExpressionSyntax))
                return false;

            return !_function.DescendantNodes().OfType<SimpleNameSyntax>()
                .Where(n => !(n.Parent is MemberAccessExpressionSyntax access && access.Name == n) && n.Parent is not MemberBindingExpressionSyntax)
                .Select(n => _model.GetSymbolInfo(n, _cancellationToken).Symbol)
                .Any(s => s is IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary } &&
                          !s.IsStatic && s.ContainingType != null);
        }

        private void EnsureNameIsFree()
        {
            var type = _model.GetDeclaredSymbol((TypeDeclarationSyntax)Member.Parent!, _cancellationToken)!;
            if (_model.LookupSymbols(Member.SpanStart, type, Name).Any())
                throw new McpException($"Error: '{type.Name}' already has a member named '{Name}'");
        }

        /// <summary>The call a reference makes, or the reference itself when it is not called.</summary>
        public static SyntaxNode UseOf(SimpleNameSyntax reference) =>
            reference.Parent is InvocationExpressionSyntax call && call.Expression == reference ? call : reference;

        /// <summary>
        /// A call gets the captured variables as arguments, and the method's name.
        /// <paramref name="original"/> is the use as the semantic model knows it;
        /// <paramref name="current"/> may already have had calls in its arguments rewritten.
        /// </summary>
        public SyntaxNode Rewrite(SyntaxNode current, SyntaxNode original)
        {
            if (current is not InvocationExpressionSyntax call)
                return SyntaxFactory.IdentifierName(Name).WithTriviaFrom(current);

            var named = call.ArgumentList.Arguments.Any(a => a.NameColon != null);
            var arguments = call.ArgumentList.Arguments.AddRange(_captured.Select(variable =>
            {
                var argument = SyntaxFactory.Argument(SyntaxFactory.IdentifierName(variable.Name));
                if (named)
                    argument = argument.WithNameColon(SyntaxFactory.NameColon(variable.Name));
                return _written.Contains(variable)
                    ? argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space))
                    : argument;
            }));

            var separators = Enumerable.Range(0, Math.Max(0, arguments.Count - 1))
                .Select(i => i < call.ArgumentList.Arguments.SeparatorCount
                    ? call.ArgumentList.Arguments.GetSeparator(i)
                    : SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space));
            return call
                .WithExpression(CallName((SimpleNameSyntax)((InvocationExpressionSyntax)original).Expression).WithTriviaFrom(call.Expression))
                .WithArgumentList(call.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(arguments, separators)));
        }

        private SimpleNameSyntax CallName(SimpleNameSyntax name)
        {
            if (!_explicitTypeArguments && name is not GenericNameSyntax)
                return SyntaxFactory.IdentifierName(Name);

            var own = name is GenericNameSyntax generic
                ? generic.TypeArgumentList.Arguments
                : SyntaxFactory.SeparatedList(((IMethodSymbol)_model.GetSymbolInfo(name, _cancellationToken).Symbol!).TypeArguments
                    .Select(t => SyntaxFactory.ParseTypeName(t.ToMinimalDisplayString(_model, name.SpanStart))));
            var all = own.AddRange(_typeParameters.Select(t => SyntaxFactory.IdentifierName(t.Name)));
            return SyntaxFactory.GenericName(SyntaxFactory.Identifier(Name), SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(all, Enumerable.Range(0, Math.Max(0, all.Count - 1))
                    .Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space)))));
        }

        /// <summary>
        /// The private method: the function's signature with the captured type parameters
        /// and variables added, its body with its own calls rewritten, indented as a member
        /// and set apart from the member before it by a blank line.
        /// </summary>
        public MethodDeclarationSyntax Method()
        {
            var recursive = _function.DescendantNodes().OfType<SimpleNameSyntax>().Where(n => References.Contains(n)).Select(UseOf);
            var rewritten = _function.ReplaceNodes(recursive, (original, current) => Rewrite(current, original));

            var memberIndentation = PositionTarget.IndentationOf(Member);
            var shifted = ReindentDetached(rewritten, memberIndentation - PositionTarget.IndentationOf(_function));

            var modifiers = new List<SyntaxToken> { Keyword(SyntaxKind.PrivateKeyword) };
            if (IsStatic)
                modifiers.Add(Keyword(SyntaxKind.StaticKeyword));
            modifiers.AddRange(shifted.Modifiers
                .Where(m => !m.IsKind(SyntaxKind.StaticKeyword))
                .Select(m => m.WithLeadingTrivia().WithTrailingTrivia(SyntaxFactory.Space)));

            var parameters = shifted.ParameterList.Parameters.AddRange(_captured.Select(variable =>
            {
                var parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(variable.Name))
                    .WithType(SyntaxFactory.ParseTypeName(TypeOf(variable).ToMinimalDisplayString(_model, _function.SpanStart)).WithTrailingTrivia(SyntaxFactory.Space));
                return _written.Contains(variable) ? parameter.WithModifiers(SyntaxFactory.TokenList(Keyword(SyntaxKind.RefKeyword))) : parameter;
            }));
            var parameterList = shifted.ParameterList.WithParameters(SyntaxFactory.SeparatedList(parameters, Commas(parameters.Count)));

            var typeParameters = (shifted.TypeParameterList?.Parameters ?? default)
                .AddRange(_typeParameters.Select(t => SyntaxFactory.TypeParameter(t.Name)));
            var typeParameterList = typeParameters.Count == 0
                ? null
                : SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(typeParameters, Commas(typeParameters.Count)));

            var constraints = shifted.ConstraintClauses.AddRange(_typeParameters
                .Select(ConstraintClause)
                .OfType<TypeParameterConstraintClauseSyntax>()
                .Select(c => c.WithoutTrivia().WithLeadingTrivia(SyntaxFactory.Space)));
            if (constraints.Count > shifted.ConstraintClauses.Count)
            {
                // The line break after the parameters now follows the constraints.
                constraints = constraints.Replace(constraints.Last(), constraints.Last().WithTrailingTrivia(parameterList.CloseParenToken.TrailingTrivia));
                parameterList = parameterList.WithCloseParenToken(parameterList.CloseParenToken.WithTrailingTrivia());
            }

            var method = SyntaxFactory.MethodDeclaration(
                    shifted.AttributeLists,
                    SyntaxFactory.TokenList(modifiers),
                    shifted.ReturnType.WithoutLeadingTrivia(),
                    null,
                    SyntaxFactory.Identifier(Name).WithTriviaFrom(shifted.Identifier),
                    typeParameterList,
                    parameterList,
                    constraints,
                    shifted.Body,
                    shifted.ExpressionBody,
                    shifted.SemicolonToken);

            // Blank lines above the function give way to the one blank line added here.
            var trivia = shifted.GetLeadingTrivia().ToList();
            var content = trivia.FindIndex(t => !t.IsKind(SyntaxKind.EndOfLineTrivia) && !t.IsKind(SyntaxKind.WhitespaceTrivia));
            var lineStart = trivia.FindLastIndex(content < 0 ? trivia.Count - 1 : content, t => t.IsKind(SyntaxKind.EndOfLineTrivia)) + 1;
            return method.WithLeadingTrivia(SyntaxFactory.TriviaList(MemberBody.EndOfLine(_function)).AddRange(trivia.Skip(lineStart)));
        }

        /// <summary>The constraint on a captured type parameter, from the method declaring it.</summary>
        private TypeParameterConstraintClauseSyntax? ConstraintClause(ITypeParameterSymbol typeParameter)
        {
            var declaration = typeParameter.DeclaringMethod?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(_cancellationToken);
            var clauses = declaration switch
            {
                MethodDeclarationSyntax method => method.ConstraintClauses,
                LocalFunctionStatementSyntax function => function.ConstraintClauses,
                _ => default,
            };
            return clauses.FirstOrDefault(c => c.Name.Identifier.ValueText == typeParameter.Name);
        }

        /// <summary>
        /// <see cref="PositionTarget.Reindent"/> needs the node in its tree, so the
        /// rewritten function is reindented through the original's layout.
        /// </summary>
        private LocalFunctionStatementSyntax ReindentDetached(LocalFunctionStatementSyntax rewritten, int delta)
        {
            var tree = SyntaxFactory.SyntaxTree(rewritten.WithLeadingTrivia(_function.GetLeadingTrivia()));
            var reparsed = (LocalFunctionStatementSyntax)tree.GetRoot();
            return PositionTarget.Reindent(reparsed, delta);
        }

        private static SyntaxToken Keyword(SyntaxKind kind) =>
            SyntaxFactory.Token(kind).WithTrailingTrivia(SyntaxFactory.Space);

        private static IEnumerable<SyntaxToken> Commas(int count) =>
            Enumerable.Range(0, Math.Max(0, count - 1))
                .Select(_ => SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space));
    }
}
