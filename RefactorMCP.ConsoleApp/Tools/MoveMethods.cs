using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

// ===============================================================================
// REFACTORED METHOD MOVEMENT ARCHITECTURE
// ===============================================================================
// This tool follows a layered architecture with clear separation of concerns:
//
// 1. AST TRANSFORMATION LAYER: Pure syntax tree operations (no file I/O)
//    - MoveStaticMethodAst(): Transforms static methods in memory
//    - MoveInstanceMethodAst(): Transforms instance methods in memory
//    - AddMethodToTargetClass(): Adds methods to target classes
//    - PropagateUsings(): Manages using statements
//
// 2. FILE OPERATION LAYER: File I/O operations using AST layer
//    - MoveStaticMethodInFile(): Handles file-based static method moves
//    - MoveInstanceMethodInFile(): Handles file-based instance method moves
//
// 3. SOLUTION OPERATION LAYER: Solution/Document operations using AST layer
//    - MoveStaticMethod(): Public API for solution-based moves
//    - MoveInstanceMethod(): Public API for solution-based moves
//
// 4. LEGACY COMPATIBILITY: String-based methods for backward compatibility
//    - MoveStaticMethodInSource(): Legacy string-based static moves
//    - MoveInstanceMethodInSource(): Legacy string-based instance moves
// ===============================================================================

public class InstanceMemberUsageChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _knownInstanceMembers;
    public bool HasInstanceMemberUsage { get; private set; }

    public InstanceMemberUsageChecker(HashSet<string> knownInstanceMembers)
    {
        _knownInstanceMembers = knownInstanceMembers;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;

        // Skip if this is part of a parameter or type declaration
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        // Check if this is a known instance member being accessed
        if (_knownInstanceMembers.Contains(node.Identifier.ValueText))
        {
            // If it's a member access where this identifier is the expression part (e.g., numbers.Sum())
            // Or if it's accessed directly (e.g., numbers)
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                HasInstanceMemberUsage = true;
            }
            // Direct access to instance member
            else if (parent is not MemberAccessExpressionSyntax && parent is not InvocationExpressionSyntax)
            {
                HasInstanceMemberUsage = true;
            }
        }

        return base.VisitIdentifierName(node);
    }
}

public class InstanceMemberRewriter : CSharpSyntaxRewriter
{
    private readonly string _parameterName;
    private readonly HashSet<string> _knownInstanceMembers;

    public InstanceMemberRewriter(string parameterName, HashSet<string> knownInstanceMembers)
    {
        _parameterName = parameterName;
        _knownInstanceMembers = knownInstanceMembers;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;

        // Skip if this is part of a parameter or type declaration
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        // Check if this is a known instance member being accessed
        if (_knownInstanceMembers.Contains(node.Identifier.ValueText))
        {
            // If it's a member access where this identifier is the expression part (e.g., numbers.Sum())
            if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
            {
                // Replace with parameter.member (e.g., calculator.numbers)
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node);
            }
            // Direct access to instance member
            else if (parent is not MemberAccessExpressionSyntax)
            {
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    node);
            }
        }

        return base.VisitIdentifierName(node);
    }
}

// New: Detect method calls to other methods in the same class
public class MethodCallChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _classMethodNames;
    public bool HasMethodCalls { get; private set; }

    public MethodCallChecker(HashSet<string> classMethodNames)
    {
        _classMethodNames = classMethodNames;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check for simple method calls (not member access)
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            if (_classMethodNames.Contains(identifier.Identifier.ValueText))
            {
                HasMethodCalls = true;
            }
        }

        return base.VisitInvocationExpression(node);
    }
}

// New: Rewrite method calls to include this parameter
public class MethodCallRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _classMethodNames;
    private readonly string _parameterName;

    public MethodCallRewriter(HashSet<string> classMethodNames, string parameterName)
    {
        _classMethodNames = classMethodNames;
        _parameterName = parameterName;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check for simple method calls (not member access)
        if (node.Expression is IdentifierNameSyntax identifier)
        {
            if (_classMethodNames.Contains(identifier.Identifier.ValueText))
            {
                // Replace with parameter.method(args)
                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_parameterName),
                    identifier);
                
                return node.WithExpression(memberAccess);
            }
        }

        return base.VisitInvocationExpression(node);
    }
}

// New: Detect static field references
public class StaticFieldChecker : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _staticFieldNames;
    public bool HasStaticFieldReferences { get; private set; }

    public StaticFieldChecker(HashSet<string> staticFieldNames)
    {
        _staticFieldNames = staticFieldNames;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;

        // Skip if this is part of a parameter or type declaration
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        // Check if this is a static field being accessed directly (not through class qualification)
        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            // Make sure it's not already qualified
            if (parent is not MemberAccessExpressionSyntax || 
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                HasStaticFieldReferences = true;
            }
        }

        return base.VisitIdentifierName(node);
    }
}

// New: Rewrite static field references to include class qualification
public class StaticFieldRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _staticFieldNames;
    private readonly string _sourceClassName;

    public StaticFieldRewriter(HashSet<string> staticFieldNames, string sourceClassName)
    {
        _staticFieldNames = staticFieldNames;
        _sourceClassName = sourceClassName;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var parent = node.Parent;

        // Skip if this is part of a parameter or type declaration
        if (parent is ParameterSyntax || parent is TypeSyntax)
        {
            return base.VisitIdentifierName(node);
        }

        // Check if this is a static field being accessed directly
        if (_staticFieldNames.Contains(node.Identifier.ValueText))
        {
            // Make sure it's not already qualified and it's not the name part of a member access
            if (parent is not MemberAccessExpressionSyntax || 
                (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node))
            {
                // Replace with ClassName.field
                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(_sourceClassName),
                    node);
            }
        }

        return base.VisitIdentifierName(node);
    }
}

[McpServerToolType]
public static class MoveMethodsTool
{
    // ===== AST TRANSFORMATION LAYER =====
    // Pure syntax tree operations with no file I/O

    public class MoveStaticMethodResult
    {
        public SyntaxNode NewSourceRoot { get; set; }
        public MethodDeclarationSyntax MovedMethod { get; set; }
        public MethodDeclarationSyntax StubMethod { get; set; }
    }

    public class MoveInstanceMethodResult
    {
        public SyntaxNode NewSourceRoot { get; set; }
        public MethodDeclarationSyntax MovedMethod { get; set; }
        public MethodDeclarationSyntax StubMethod { get; set; }
        public MemberDeclarationSyntax? AccessMember { get; set; }
        public bool NeedsThisParameter { get; set; }
    }

    public static MoveStaticMethodResult MoveStaticMethodAst(SyntaxNode sourceRoot, string methodName, string targetClass)
    {
        var method = sourceRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                 m.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (method == null)
            throw new McpException($"Error: Static method '{methodName}' not found");

        // Find the source class containing this method
        var sourceClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Members.Contains(method));
        if (sourceClass == null)
            throw new McpException($"Error: Could not find source class for method '{methodName}'");

        // Enhanced: Check if method references static fields and qualify them
        var staticFieldNames = GetStaticFieldNames(sourceClass);
        var needsStaticFieldQualification = HasStaticFieldReferences(method, staticFieldNames);
        
        var movedMethod = method;
        if (needsStaticFieldQualification)
        {
            var staticFieldRewriter = new StaticFieldRewriter(staticFieldNames, sourceClass.Identifier.ValueText);
            movedMethod = (MethodDeclarationSyntax)staticFieldRewriter.Visit(movedMethod)!;
        }

        // Enhanced: Preserve generic type arguments in delegation call
        var typeArgumentList = method.TypeParameterList != null 
            ? SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(
                    method.TypeParameterList.Parameters.Select(p => 
                        (TypeSyntax)SyntaxFactory.IdentifierName(p.Identifier))))
            : null;

        // Prepare stub in the original class that delegates to the moved method
        var argumentList = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                method.ParameterList.Parameters
                    .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)))));

        // Enhanced: Add generic type arguments if present (static methods always include them)
        SimpleNameSyntax methodExpression = typeArgumentList != null
            ? SyntaxFactory.GenericName(methodName).WithTypeArgumentList(typeArgumentList)
            : SyntaxFactory.IdentifierName(methodName);

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(targetClass),
                methodExpression),
            argumentList);

        bool isVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        StatementSyntax callStatement = isVoid
            ? SyntaxFactory.ExpressionStatement(invocation)
            : SyntaxFactory.ReturnStatement(invocation);

        var stubMethod = method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        var newSourceRoot = sourceRoot.ReplaceNode(method, stubMethod);

        return new MoveStaticMethodResult
        {
            NewSourceRoot = newSourceRoot,
            MovedMethod = movedMethod,
            StubMethod = stubMethod
        };
    }

    public static MoveInstanceMethodResult MoveInstanceMethodAst(
        SyntaxNode sourceRoot,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType)
    {
        var originClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            throw new McpException($"Error: Source class '{sourceClass}' not found");

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: No method named '{methodName}' found");

        // Check if method uses instance members
        var knownMembers = GetInstanceMemberNames(originClass);
        
        // Enhanced: Get method names for rewriting (excluding methods being moved)
        var classMethodNames = GetMethodNames(originClass);
        var otherMethodNames = new HashSet<string>(classMethodNames);
        otherMethodNames.Remove(methodName);

        // Check if method uses any instance members or calls other methods (including itself for recursion)
        bool usesInstanceMembers = HasInstanceMemberUsage(method, knownMembers);
        bool callsOtherMethods = HasMethodCalls(method, otherMethodNames);
        bool isRecursive = HasMethodCalls(method, new HashSet<string> { methodName });
        
        // Need to pass 'this' if using instance members OR calling other methods OR is recursive
        bool needsThisParameter = usesInstanceMembers || callsOtherMethods || isRecursive;

        // Enhanced: Preserve generic type arguments and async patterns in delegation call
        var typeArgumentList = method.TypeParameterList != null 
            ? SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(
                    method.TypeParameterList.Parameters.Select(p => 
                        (TypeSyntax)SyntaxFactory.IdentifierName(p.Identifier))))
            : null;

        bool isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);

        // Build call to the moved method for the stub
        var originalParameters = method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))).ToList();

        // Only add 'this' as the last argument if the method needs it
        if (needsThisParameter)
        {
            originalParameters.Add(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
        }

        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(originalParameters));

        var accessExpression = SyntaxFactory.IdentifierName(accessMemberName);

        // Enhanced: Add generic type arguments only if method needs 'this' parameter
        SimpleNameSyntax methodExpression = (typeArgumentList != null && needsThisParameter)
            ? SyntaxFactory.GenericName(methodName).WithTypeArgumentList(typeArgumentList)
            : SyntaxFactory.IdentifierName(methodName);

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                accessExpression,
                methodExpression),
            argumentList);

        bool isVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        
        // Enhanced: Preserve async/await pattern in delegation
        ExpressionSyntax returnExpression = invocation;
        if (isAsync && !isVoid)
        {
            returnExpression = SyntaxFactory.AwaitExpression(invocation);
        }

        StatementSyntax callStatement = isVoid
            ? SyntaxFactory.ExpressionStatement(invocation)
            : SyntaxFactory.ReturnStatement(returnExpression);

        var stubMethod = method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        // Create the access member if it doesn't already exist
        MemberDeclarationSyntax? accessMember = null;
        bool accessMemberExists = MemberExists(originClass, accessMemberName);
        if (!accessMemberExists)
        {
            accessMember = CreateAccessMember(accessMemberType, accessMemberName, targetClass);
        }

        // Find the insertion position for the access member (after existing fields/properties)
        var originMembers = originClass.Members.ToList();
        var fieldIndex = originMembers.FindLastIndex(m => m is FieldDeclarationSyntax || m is PropertyDeclarationSyntax);
        var insertIndex = fieldIndex >= 0 ? fieldIndex + 1 : 0;

        // Insert the access member at the correct position if needed
        if (accessMember != null)
        {
            originMembers.Insert(insertIndex, accessMember);
        }

        // Replace the original method with the stub
        var methodIndex = originMembers.FindIndex(m => m == method);
        if (methodIndex >= 0)
        {
            originMembers[methodIndex] = stubMethod;
        }

        var newOriginClass = originClass.WithMembers(SyntaxFactory.List(originMembers));

        // Create the moved method
        var movedMethod = method;

        // Only add source class parameter if method needs it
        if (needsThisParameter)
        {
            var sourceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(sourceClass.ToLower()))
                .WithType(SyntaxFactory.IdentifierName(sourceClass));

            var newParameters = method.ParameterList.Parameters.Concat(new[] { sourceParameter });
            var newParameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newParameters));
            movedMethod = movedMethod.WithParameterList(newParameterList);

            // Replace references to instance members with parameter access
            if (usesInstanceMembers)
            {
                var memberRewriter = new InstanceMemberRewriter(sourceClass.ToLower(), knownMembers);
                movedMethod = (MethodDeclarationSyntax)memberRewriter.Visit(movedMethod)!;
            }

            // Enhanced: Replace method calls with parameter access
            if (callsOtherMethods)
            {
                var methodCallRewriter = new MethodCallRewriter(otherMethodNames, sourceClass.ToLower());
                movedMethod = (MethodDeclarationSyntax)methodCallRewriter.Visit(movedMethod)!;
            }

            // Enhanced: Replace recursive calls with parameter access
            if (isRecursive)
            {
                var recursiveCallRewriter = new MethodCallRewriter(new HashSet<string> { methodName }, sourceClass.ToLower());
                movedMethod = (MethodDeclarationSyntax)recursiveCallRewriter.Visit(movedMethod)!;
            }
        }

        // Ensure moved method is public in the target class
        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword));
            movedMethod = movedMethod.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        var newSourceRoot = sourceRoot.ReplaceNode(originClass, newOriginClass);

        return new MoveInstanceMethodResult
        {
            NewSourceRoot = newSourceRoot,
            MovedMethod = movedMethod,
            StubMethod = stubMethod,
            AccessMember = accessMember,
            NeedsThisParameter = needsThisParameter
        };
    }

    public static SyntaxNode AddMethodToTargetClass(SyntaxNode targetRoot, string targetClass, MethodDeclarationSyntax method)
    {
        var targetClassDecl = targetRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(method.WithLeadingTrivia());
            return ((CompilationUnitSyntax)targetRoot).AddMembers(newClass);
        }
        else
        {
            var updatedClass = targetClassDecl.AddMembers(method.WithLeadingTrivia());
            return targetRoot.ReplaceNode(targetClassDecl, updatedClass);
        }
    }

    public static SyntaxNode PropagateUsings(SyntaxNode sourceRoot, SyntaxNode targetRoot)
    {
        var sourceCompilationUnit = (CompilationUnitSyntax)sourceRoot;
        var sourceUsings = sourceCompilationUnit.Usings.ToList();

        var targetCompilationUnit = (CompilationUnitSyntax)targetRoot;
        var targetUsingNames = targetCompilationUnit.Usings
            .Select(u => u.Name.ToString())
            .ToHashSet();
        var missingUsings = sourceUsings
            .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
            .ToArray();
        
        if (missingUsings.Length > 0)
        {
            return targetCompilationUnit.AddUsings(missingUsings);
        }

        return targetRoot;
    }

    // ===== FILE OPERATION LAYER =====
    // File I/O operations that use the AST layer

    public static async Task<string> MoveStaticMethodInFile(
        string filePath,
        string methodName,
        string targetClass,
        string? targetFilePath = null)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sameFile = targetPath == filePath;

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        // Perform AST transformation
        var moveResult = MoveStaticMethodAst(sourceRoot, methodName, targetClass);

        SyntaxNode targetRoot;
        if (sameFile)
        {
            targetRoot = moveResult.NewSourceRoot;
        }
        else if (File.Exists(targetPath))
        {
            var targetText = await File.ReadAllTextAsync(targetPath);
            targetRoot = await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
        }
        else
        {
            targetRoot = SyntaxFactory.CompilationUnit();
        }

        // Propagate using statements
        targetRoot = PropagateUsings(sourceRoot, targetRoot);

        // Add method to target class
        targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod);

        // Write files
        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        
        if (!sameFile)
        {
            var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString());

        return $"Successfully moved static method '{methodName}' to {targetClass} in {targetPath}";
    }

    public static async Task<string> MoveInstanceMethodInFile(
        string filePath,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType,
        string? targetFilePath = null)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        // Perform AST transformation
        var moveResult = MoveInstanceMethodAst(sourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        SyntaxNode targetRoot;
        if (sameFile)
        {
            targetRoot = moveResult.NewSourceRoot;
        }
        else if (File.Exists(targetPath))
        {
            var targetText = await File.ReadAllTextAsync(targetPath);
            targetRoot = await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
        }
        else
        {
            targetRoot = SyntaxFactory.CompilationUnit();
        }

        // Propagate using statements
        if (!sameFile)
        {
            targetRoot = PropagateUsings(sourceRoot, targetRoot);
        }

        // Add method to target class
        targetRoot = AddMethodToTargetClass(targetRoot, targetClass, moveResult.MovedMethod);

        // Write files
        if (sameFile)
        {
            var formatted = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(targetPath, formatted.ToFullString());
        }
        else
        {
            var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());

            var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString());
        }

        return $"Successfully moved instance method {sourceClass}.{methodName} to {targetClass}" +
               (targetFilePath != null ? $" in {targetPath}" : "");
    }

    // ===== SOLUTION OPERATION LAYER =====
    // Solution/Document operations that use the AST layer

    private static async Task<string> MoveInstanceMethodWithSolution(Document document, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync();

        // Perform AST transformation
        var moveResult = MoveInstanceMethodAst(syntaxRoot!, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        // Add method to target class in the same document
        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);

        // Format and write using Document API
        var formatted = Formatter.Format(finalRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return $"Successfully moved {sourceClass}.{methodName} instance method to {targetClass} in {document.FilePath}";
    }

    // ===== HELPER METHODS =====

    private static bool HasInstanceMemberUsage(MethodDeclarationSyntax method, HashSet<string> knownMembers)
    {
        var usageChecker = new InstanceMemberUsageChecker(knownMembers);
        usageChecker.Visit(method);
        return usageChecker.HasInstanceMemberUsage;
    }

    private static bool HasMethodCalls(MethodDeclarationSyntax method, HashSet<string> methodNames)
    {
        var callChecker = new MethodCallChecker(methodNames);
        callChecker.Visit(method);
        return callChecker.HasMethodCalls;
    }

    private static bool HasStaticFieldReferences(MethodDeclarationSyntax method, HashSet<string> staticFieldNames)
    {
        var fieldChecker = new StaticFieldChecker(staticFieldNames);
        fieldChecker.Visit(method);
        return fieldChecker.HasStaticFieldReferences;
    }

    // ===== LEGACY STRING-BASED METHODS (for backward compatibility) =====

    public static string MoveStaticMethodInSource(string sourceText, string methodName, string targetClass)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var moveResult = MoveStaticMethodAst(root, methodName, targetClass);
        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);

        var formatted = Formatter.Format(finalRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveInstanceMethodInSource(string sourceText, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);

        var formatted = Formatter.Format(finalRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveMultipleInstanceMethodsInSource(string sourceText, string sourceClass, string[] methodNames, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();
        
        foreach (var methodName in methodNames)
        {
            var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
            root = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);
        }
        
        var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static HashSet<string> GetInstanceMemberNames(ClassDeclarationSyntax originClass)
    {
        var knownMembers = new HashSet<string>();
        foreach (var member in originClass.Members)
        {
            if (member is FieldDeclarationSyntax field)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    knownMembers.Add(variable.Identifier.ValueText);
                }
            }
            else if (member is PropertyDeclarationSyntax property)
            {
                knownMembers.Add(property.Identifier.ValueText);
            }
        }
        return knownMembers;
    }

    // New: Get method names in the class
    private static HashSet<string> GetMethodNames(ClassDeclarationSyntax originClass)
    {
        var methodNames = new HashSet<string>();
        foreach (var member in originClass.Members)
        {
            if (member is MethodDeclarationSyntax method)
            {
                methodNames.Add(method.Identifier.ValueText);
            }
        }
        return methodNames;
    }

    // New: Get static field names in the class
    private static HashSet<string> GetStaticFieldNames(ClassDeclarationSyntax originClass)
    {
        var staticFieldNames = new HashSet<string>();
        foreach (var member in originClass.Members)
        {
            if (member is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    staticFieldNames.Add(variable.Identifier.ValueText);
                }
            }
        }
        return staticFieldNames;
    }

    private static bool MemberExists(ClassDeclarationSyntax classDecl, string memberName)
    {
        return classDecl.Members.OfType<FieldDeclarationSyntax>()
                   .Any(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == memberName))
               || classDecl.Members.OfType<PropertyDeclarationSyntax>()
                   .Any(p => p.Identifier.ValueText == memberName);
    }

    private static MemberDeclarationSyntax CreateAccessMember(string accessMemberType, string accessMemberName, string targetClass)
    {
        if (accessMemberType == "property")
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(targetClass), accessMemberName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .AddAccessorListAccessors(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        return SyntaxFactory.FieldDeclaration(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName(targetClass),
                        SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.VariableDeclarator(accessMemberName)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(targetClass))
                                        .WithArgumentList(SyntaxFactory.ArgumentList())))
                        })))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
    }

    [McpServerTool, Description("Move a static method to another class (preferred for large C# file refactoring)")]
    public static async Task<string> MoveStaticMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the static method to move")] string methodName,
        [Description("Name of the target class")] string targetClass,
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

            var sourceText = await File.ReadAllTextAsync(filePath);
            var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
            var sameFile = targetPath == filePath;

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
            var syntaxRoot = await syntaxTree.GetRootAsync();

            var sourceUsings = syntaxRoot.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .ToList();

            var method = syntaxRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                    m.Modifiers.Any(SyntaxKind.StaticKeyword));
            if (method == null)
                return RefactoringHelpers.ThrowMcpException($"Error: Static method '{methodName}' not found");

            // Remove the original method
            var newSourceRoot = syntaxRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);

            SyntaxNode targetRoot;
            if (sameFile)
            {
                targetRoot = newSourceRoot;
            }
            else if (File.Exists(targetPath))
            {
                var targetText = await File.ReadAllTextAsync(targetPath);
                targetRoot = await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
            }
            else
            {
                targetRoot = SyntaxFactory.CompilationUnit();
            }

            var targetCompilationUnit = (CompilationUnitSyntax)targetRoot;
            var targetUsingNames = targetCompilationUnit.Usings
                .Select(u => u.Name.ToString())
                .ToHashSet();
            var missingUsings = sourceUsings
                .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
                .ToArray();
            if (missingUsings.Length > 0)
            {
                targetCompilationUnit = targetCompilationUnit.AddUsings(missingUsings);
                targetRoot = targetCompilationUnit;
            }

            var targetClassDecl = targetRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

            if (targetClassDecl == null)
            {
                var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddMembers(method.WithLeadingTrivia());

                targetRoot = ((CompilationUnitSyntax)targetRoot).AddMembers(newClass);
            }
            else
            {
                var newTarget = targetClassDecl.AddMembers(method.WithLeadingTrivia());
                targetRoot = targetRoot.ReplaceNode(targetClassDecl, newTarget);
            }

            var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);

            if (!sameFile)
            {
                var formattedSource = Formatter.Format(newSourceRoot, RefactoringHelpers.SharedWorkspace);
                await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString());

            return $"Successfully moved static method '{methodName}' to {targetClass} in {targetPath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving static method: {ex.Message}", ex);
        }
    }
    [McpServerTool, Description("Move one or more instance methods to another class (preferred for large C# file refactoring)")]
    public static async Task<string> MoveInstanceMethod(
        [Description("Absolute path to the solution file (.sln)")] string solutionPath,
        [Description("Path to the C# file containing the method")] string filePath,
        [Description("Name of the source class containing the method")] string sourceClass,
        [Description("Comma separated names of the methods to move")] string methodNames,
        [Description("Name of the target class")] string targetClass,
        [Description("Name for the access member")] string accessMemberName,
        [Description("Type of access member (field, property, variable)")] string accessMemberType = "field",
        [Description("Path to the target file (optional, will create if doesn't exist)")] string? targetFilePath = null)
    {
        try
        {
            var methodList = methodNames.Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToArray();
            if (methodList.Length == 0)
                return RefactoringHelpers.ThrowMcpException("Error: No method names provided");

            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var document = RefactoringHelpers.GetDocumentByPath(solution, filePath);

            return await MoveMethods(filePath, sourceClass, targetClass, accessMemberName, accessMemberType, targetFilePath, methodList, document);
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving instance method: {ex.Message}", ex);
        }
    }

    public static async Task<string> MoveMethods(string filePath, string sourceClass, string targetClass, string accessMemberName,
        string accessMemberType, string? targetFilePath, string[] methodList, Document? document)
    {
        if (document != null)
        {
            // For solution-based operations, process one by one but update document reference after each move
            var results = new List<string>();
            var currentDocument = document;

            foreach (var name in methodList)
            {
                results.Add(await MoveInstanceMethodWithSolution(currentDocument, sourceClass, name, targetClass, accessMemberName, accessMemberType));

                // Refresh the document reference to get the updated version after the move
                var solution = await RefactoringHelpers.GetOrLoadSolution(currentDocument.Project.Solution.FilePath!);
                currentDocument = RefactoringHelpers.GetDocumentByPath(solution, filePath);
            }
            return string.Join("\n", results);
        }
        else
        {
            // For single-file operations, use the bulk move method for better efficiency
            if (methodList.Length == 1)
            {
                return await MoveInstanceMethodInFile(filePath, sourceClass, methodList[0], targetClass, accessMemberName, accessMemberType, targetFilePath);
            }
            else
            {
                return await MoveBulkInstanceMethodsInFile(filePath, sourceClass, methodList, targetClass, accessMemberName, accessMemberType, targetFilePath);
            }
        }
    }

    private static async Task<string> MoveBulkInstanceMethodsInFile(string filePath, string sourceClass, string[] methodNames, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var sourceText = await File.ReadAllTextAsync(filePath);

        if (sameFile)
        {
            // Same file operation - use multiple individual AST transformations
            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();
            
            foreach (var methodName in methodNames)
            {
                var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
                root = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);
            }
            
            var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formatted.ToFullString());
            return $"Successfully moved {methodNames.Length} methods from {sourceClass} to {targetClass} in {filePath}";
        }
        else
        {
            // Cross-file operation - for now, fall back to individual moves
            // TODO: Implement efficient cross-file bulk move
            var results = new List<string>();
            foreach (var methodName in methodNames)
            {
                results.Add(await MoveInstanceMethodInFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath));
            }
            return string.Join("\n", results);
        }
    }
}
