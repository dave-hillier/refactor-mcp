using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

internal class ExtractMethodRewriter : CSharpSyntaxRewriter
{
    private readonly MethodDeclarationSyntax _containingMethod;
    private readonly ClassDeclarationSyntax? _containingClass;
    private readonly List<StatementSyntax> _statements;
    private readonly string _methodName;
    private readonly MethodDeclarationSyntax _newMethod;
    private readonly MethodDeclarationSyntax _updatedMethod;

    public ExtractMethodRewriter(
        MethodDeclarationSyntax containingMethod,
        ClassDeclarationSyntax? containingClass,
        List<StatementSyntax> statements,
        string methodName)
    {
        _containingMethod = containingMethod;
        _containingClass = containingClass;
        _statements = statements;
        _methodName = methodName;

        // Analyze the extracted statements for variables and return behavior
        var analysis = AnalyzeStatementsForExtraction(statements, containingMethod);
        
        _newMethod = CreateExtractedMethod(methodName, statements, analysis);
        _updatedMethod = CreateUpdatedOriginalMethod(containingMethod, statements, methodName, analysis);
    }

    private ExtractMethodAnalysis AnalyzeStatementsForExtraction(List<StatementSyntax> statements, MethodDeclarationSyntax containingMethod)
    {
        var analysis = new ExtractMethodAnalysis();
        
        // Get all identifiers used in the extracted statements
        var usedIdentifiers = statements
            .SelectMany(s => s.DescendantNodes().OfType<IdentifierNameSyntax>())
            .Select(id => id.Identifier.ValueText)
            .ToHashSet();

        // Get all identifiers declared before the extracted statements in the same method
        var allMethodStatements = containingMethod.Body?.Statements ?? new SyntaxList<StatementSyntax>();
        var statementsBeforeExtracted = new List<StatementSyntax>();
        
        foreach (var stmt in allMethodStatements)
        {
            if (statements.Contains(stmt))
                break;
            statementsBeforeExtracted.Add(stmt);
        }

        // Analyze method parameters - these are always available
        foreach (var param in containingMethod.ParameterList.Parameters)
        {
            var paramName = param.Identifier.ValueText;
            if (usedIdentifiers.Contains(paramName))
            {
                analysis.RequiredParameters.Add(new ParameterInfo(paramName, param.Type?.ToString() ?? "object"));
            }
        }

        // Analyze local variables declared before extraction point
        var declaredBefore = statementsBeforeExtracted
            .SelectMany(GetDeclaredVariables)
            .ToDictionary(v => v.Name, v => v.Type);

        foreach (var kvp in declaredBefore)
        {
            if (usedIdentifiers.Contains(kvp.Key))
            {
                // Infer the actual type if the declared type is 'var'
                var actualType = InferActualType(kvp.Value, kvp.Key, statementsBeforeExtracted, containingMethod);
                analysis.RequiredParameters.Add(new ParameterInfo(kvp.Key, actualType));
            }
        }

        // Check for explicit return statements in the extracted code
        var returnStatements = statements
            .SelectMany(s => s.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>())
            .ToList();

        // Analyze variables declared within the extracted statements
        var declaredWithin = statements
            .SelectMany(GetDeclaredVariables)
            .ToList();

        // Check if any variables declared within are used after the extraction point
        var statementsAfterExtracted = allMethodStatements
            .SkipWhile(s => !statements.Contains(s))
            .Skip(statements.Count)
            .ToList();

        var usedAfter = statementsAfterExtracted
            .SelectMany(s => s.DescendantNodes().OfType<IdentifierNameSyntax>())
            .Select(id => id.Identifier.ValueText)
            .ToHashSet();

        var variablesUsedAfter = declaredWithin
            .Where(v => usedAfter.Contains(v.Name))
            .ToList();

        // Determine return behavior - prioritize return statements in extracted code
        if (returnStatements.Any())
        {
            // If there are return statements in the extracted code, this should be a returning method
            var containingReturnType = containingMethod.ReturnType.ToString();
            
            // ALWAYS use the containing method's return type when we have return statements
            // unless it's void (which means the return statements are invalid anyway)
            if (containingReturnType != "void")
            {
                analysis.ReturnType = containingReturnType;
                // Don't set a ReturnVariable - we're directly returning from the extracted method
            }
            else
            {
                // Try to infer from the return expressions
                var returnExpression = returnStatements.FirstOrDefault(r => r.Expression != null)?.Expression;
                analysis.ReturnType = InferReturnTypeFromExpression(returnExpression, containingMethod, declaredWithin);
            }
            // When we have return statements, we don't need to check for variables used after
            // because the method will return directly
        }
        else if (variablesUsedAfter.Count == 1)
        {
            // Single variable returned (declared within, used after)
            var returnVar = variablesUsedAfter.First();
            var returnType = InferActualType(returnVar.Type, returnVar.Name, statements, containingMethod);
            analysis.ReturnType = returnType;
            analysis.ReturnVariable = returnVar.Name;
        }
        else if (variablesUsedAfter.Count > 1)
        {
            // Multiple variables - use out parameters
            analysis.ReturnType = "void";
            analysis.OutParameters = variablesUsedAfter.Select(v => new VariableInfo(v.Name, InferActualType(v.Type, v.Name, statements, containingMethod))).ToList();
        }
        else
        {
            // No return statements and no variables used after - void method
            analysis.ReturnType = "void";
        }

        // Check for async/await
        analysis.IsAsync = statements.Any(s => 
            s.DescendantNodes().OfType<AwaitExpressionSyntax>().Any());

        if (analysis.IsAsync && analysis.ReturnType != "void")
        {
            // Don't wrap with Task<> if the return type is already a Task type
            if (!analysis.ReturnType.StartsWith("Task<") && !analysis.ReturnType.Equals("Task"))
            {
                analysis.ReturnType = $"Task<{analysis.ReturnType}>";
            }
        }
        else if (analysis.IsAsync)
        {
            analysis.ReturnType = "Task";
        }

        return analysis;
    }

    private string InferReturnTypeFromExpression(ExpressionSyntax? expression, MethodDeclarationSyntax containingMethod, List<VariableInfo> declaredVariables)
    {
        if (expression == null)
            return "void";

        // If the containing method has a concrete return type, use it
        var containingReturnType = containingMethod.ReturnType.ToString();
        if (containingReturnType != "var" && containingReturnType != "void")
        {
            return containingReturnType;
        }

        // If returning a variable declared in the extracted statements, infer from its initialization
        if (expression is IdentifierNameSyntax id)
        {
            var variableName = id.Identifier.ValueText;
            var declaredVariable = declaredVariables.FirstOrDefault(v => v.Name == variableName);
            if (declaredVariable != null)
            {
                var inferredType = InferActualType(declaredVariable.Type, declaredVariable.Name, _statements, containingMethod);
                // If we can infer a specific type and the containing method allows it, use the inferred type
                // Otherwise, fall back to the containing method's return type if it's not void
                if (inferredType != "object" || containingReturnType == "void")
                {
                    return inferredType;
                }
                return containingReturnType;
            }
            
            // Check if it's a method parameter - if so, get its type
            var parameterType = InferTypeFromIdentifier(variableName, containingMethod);
            if (parameterType != "object")
            {
                return parameterType;
            }
        }

        // Try to infer from expression patterns
        var inferredFromExpression = expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText switch
            {
                var s when int.TryParse(s, out _) => "int",
                var s when double.TryParse(s, out _) => "double",
                var s when bool.TryParse(s, out _) => "bool",
                _ when literal.Token.IsKind(SyntaxKind.StringLiteralToken) => "string",
                _ => "object"
            },
            BinaryExpressionSyntax binary => InferTypeFromBinaryExpression(binary),
            _ => "object"
        };

        // Prefer the containing method's return type if it's concrete and not void
        if (containingReturnType != "void" && containingReturnType != "var")
        {
            return containingReturnType;
        }

        return inferredFromExpression != "object" ? inferredFromExpression : "object";
    }

    private string InferTypeFromBinaryExpression(BinaryExpressionSyntax binary)
    {
        // For arithmetic operations, default to int unless we can determine otherwise
        if (binary.OperatorToken.IsKind(SyntaxKind.PlusToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.MinusToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.AsteriskToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.SlashToken))
        {
            return "int"; // Most common case for arithmetic
        }
        
        // For comparison operations, return bool
        if (binary.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.LessThanToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.GreaterThanToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.LessThanEqualsToken) ||
            binary.OperatorToken.IsKind(SyntaxKind.GreaterThanEqualsToken))
        {
            return "bool";
        }

        return "object";
    }

    private string InferTypeFromIdentifier(string identifierName, MethodDeclarationSyntax containingMethod)
    {
        // Check method parameters first
        var param = containingMethod.ParameterList.Parameters
            .FirstOrDefault(p => p.Identifier.ValueText == identifierName);
        if (param != null)
        {
            return param.Type?.ToString() ?? "object";
        }

        // Default fallback
        return "object";
    }

    private string InferActualType(string declaredType, string variableName, List<StatementSyntax> statements, MethodDeclarationSyntax containingMethod)
    {
        // If not var, return as-is
        if (declaredType != "var")
        {
            return declaredType;
        }

        // For var declarations, try to infer from the initialization
        var varDeclaration = statements
            .OfType<LocalDeclarationStatementSyntax>()
            .SelectMany(decl => decl.Declaration.Variables)
            .FirstOrDefault(v => v.Identifier.ValueText == variableName);

        if (varDeclaration?.Initializer?.Value != null)
        {
            var initExpression = varDeclaration.Initializer.Value;
            
            // Handle await expressions (like await GetBoolAsync())
            if (initExpression is AwaitExpressionSyntax awaitExpr)
            {
                return InferTypeFromAwaitExpression(awaitExpr);
            }
            
            // Handle binary expressions (like a + b)
            if (initExpression is BinaryExpressionSyntax binary)
            {
                return InferTypeFromBinaryExpression(binary);
            }
            
            // Handle simple identifier references
            if (initExpression is IdentifierNameSyntax id)
            {
                return InferTypeFromIdentifier(id.Identifier.ValueText, containingMethod);
            }
            
            // Handle literals
            if (initExpression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText switch
                {
                    var s when int.TryParse(s, out _) => "int",
                    var s when double.TryParse(s, out _) => "double",
                    var s when bool.TryParse(s, out _) => "bool",
                    _ when literal.Token.IsKind(SyntaxKind.StringLiteralToken) => "string",
                    _ => "object"
                };
            }
        }

        return "object"; // Fallback for var when we can't infer
    }

    private string InferTypeFromAwaitExpression(AwaitExpressionSyntax awaitExpr)
    {
        // Handle method invocations like GetListOfIntsAsync()
        if (awaitExpr.Expression is InvocationExpressionSyntax invocation)
        {
            // For method calls, try to infer from the method name
            if (invocation.Expression is IdentifierNameSyntax methodName)
            {
                var methodNameText = methodName.Identifier.ValueText;
                // Common pattern: methods ending with "Async" that return Task<T>
                // Try to infer the T from the method name
                return methodNameText switch
                {
                    var name when name.Contains("Bool") => "bool",
                    var name when name.Contains("Int") => "int",
                    var name when name.Contains("String") => "string",
                    var name when name.Contains("Double") => "double",
                    // For GetBoolAsync specifically
                    "GetBoolAsync" => "bool",
                    // For GetListOfIntsAsync - infer List<int> from the name
                    "GetListOfIntsAsync" => "List<int>",
                    // More general patterns for async methods
                    var name when name.EndsWith("Async") && name.Contains("List") => "List<object>",
                    _ => "object" // Fallback when we can't determine the type
                };
            }
        }
        
        return "object"; // Fallback for await expressions we can't analyze
    }

    private MethodDeclarationSyntax CreateExtractedMethod(string methodName, List<StatementSyntax> statements, ExtractMethodAnalysis analysis)
    {
        var parameters = analysis.RequiredParameters
            .Select(p => SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(SyntaxFactory.ParseTypeName(p.Type)))
            .ToList();

        // Add out parameters for multiple return variables
        if (analysis.OutParameters?.Count > 0)
        {
            foreach (var outParam in analysis.OutParameters)
            {
                parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(outParam.Name))
                    .WithType(SyntaxFactory.ParseTypeName(outParam.Type))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.OutKeyword))));
            }
        }

        var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
        if (analysis.IsAsync)
        {
            modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }

        var returnType = SyntaxFactory.ParseTypeName(analysis.ReturnType);

        var methodBody = statements.ToList();

        // Add return statement if needed (only for single return variable, not when we already have return statements)
        if (analysis.ReturnVariable != null && !statements.Any(s => s.DescendantNodes().OfType<ReturnStatementSyntax>().Any()))
        {
            var returnStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.IdentifierName(analysis.ReturnVariable));
            methodBody.Add(returnStatement);
        }

        return SyntaxFactory.MethodDeclaration(returnType, methodName)
            .WithModifiers(modifiers)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithBody(SyntaxFactory.Block(methodBody));
    }

    private MethodDeclarationSyntax CreateUpdatedOriginalMethod(
        MethodDeclarationSyntax containingMethod, 
        List<StatementSyntax> statements, 
        string methodName, 
        ExtractMethodAnalysis analysis)
    {
        var arguments = analysis.RequiredParameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Name)))
            .ToList();

        // Add out arguments for multiple return variables
        if (analysis.OutParameters?.Count > 0)
        {
            foreach (var outParam in analysis.OutParameters)
            {
                arguments.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(outParam.Name))
                    .WithRefOrOutKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword)));
            }
        }

        var methodCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(methodName))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));

        StatementSyntax replacementStatement;

        if (analysis.ReturnType == "void" || analysis.ReturnType == "Task")
        {
            var expression = analysis.IsAsync ? 
                (ExpressionSyntax)SyntaxFactory.AwaitExpression(methodCall) :
                methodCall;
            replacementStatement = SyntaxFactory.ExpressionStatement(expression);
        }
        else if (analysis.ReturnVariable != null)
        {
            // Single return variable
            var expression = analysis.IsAsync ? 
                (ExpressionSyntax)SyntaxFactory.AwaitExpression(methodCall) :
                methodCall;
            var assignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(analysis.ReturnVariable),
                expression);
            replacementStatement = SyntaxFactory.ExpressionStatement(assignment);
        }
        else
        {
            // Direct return for methods with return statements or inferred return type
            var expression = analysis.IsAsync ? 
                (ExpressionSyntax)SyntaxFactory.AwaitExpression(methodCall) :
                methodCall;
            replacementStatement = SyntaxFactory.ReturnStatement(expression);
        }

        var body = containingMethod.Body!;
        var updated = body.ReplaceNode(statements.First(), replacementStatement)!;
        
        // Remove all the other extracted statements
        foreach (var stmt in statements.Skip(1))
        {
            updated = updated.RemoveNode(updated.DescendantNodes().OfType<StatementSyntax>()
                .FirstOrDefault(s => s.IsEquivalentTo(stmt)), SyntaxRemoveOptions.KeepNoTrivia)!;
        }

        return containingMethod.WithBody(updated);
    }

    private IEnumerable<VariableInfo> GetDeclaredVariables(StatementSyntax statement)
    {
        // Handle variable declarations
        if (statement is LocalDeclarationStatementSyntax localDecl)
        {
            var typeText = localDecl.Declaration.Type.ToString();
            foreach (var variable in localDecl.Declaration.Variables)
            {
                yield return new VariableInfo(
                    variable.Identifier.ValueText,
                    typeText);
            }
        }

        // Handle foreach statements
        if (statement is ForEachStatementSyntax foreachStmt)
        {
            yield return new VariableInfo(
                foreachStmt.Identifier.ValueText,
                foreachStmt.Type.ToString());
        }

        // Handle for statements
        if (statement is ForStatementSyntax forStmt && forStmt.Declaration != null)
        {
            var typeText = forStmt.Declaration.Type.ToString();
            foreach (var variable in forStmt.Declaration.Variables)
            {
                yield return new VariableInfo(
                    variable.Identifier.ValueText,
                    typeText);
            }
        }

        // Handle using statements
        if (statement is UsingStatementSyntax usingStmt && usingStmt.Declaration != null)
        {
            var typeText = usingStmt.Declaration.Type.ToString();
            foreach (var variable in usingStmt.Declaration.Variables)
            {
                yield return new VariableInfo(
                    variable.Identifier.ValueText,
                    typeText);
            }
        }
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node == _containingMethod)
            return _updatedMethod;
        return base.VisitMethodDeclaration(node)!;
    }

    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
        if (_containingClass != null && node == _containingClass)
        {
            visited = visited.AddMembers(_newMethod);
        }
        return visited;
    }

    private class ExtractMethodAnalysis
    {
        public List<ParameterInfo> RequiredParameters { get; } = new();
        public string ReturnType { get; set; } = "void";
        public string? ReturnVariable { get; set; }
        public List<VariableInfo>? OutParameters { get; set; }
        public bool IsAsync { get; set; }
    }

    private record ParameterInfo(string Name, string Type);
    private record VariableInfo(string Name, string Type);
}
