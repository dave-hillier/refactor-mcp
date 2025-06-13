using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
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
//    - MoveStaticMethodAst(): Transforms static methods in memory using context objects
//    - MoveInstanceMethodAst(): Transforms instance methods in memory using context objects
//    - AddMethodToTargetClass(): Adds methods to target classes
//    - PropagateUsings(): Manages using statements
//    - Helper methods: FindStaticMethod(), TransformStaticMethodForMove(), etc.
//
// 2. FILE OPERATION LAYER: File I/O operations using AST layer
//    - MoveStaticMethodInFile(): Handles file-based static method moves
//    - MoveInstanceMethodInFile(): Handles file-based instance method moves
//    - Helper methods: ValidateFileExists(), CreateFileOperationContext(), etc.
//
// 3. SOLUTION OPERATION LAYER: Solution/Document operations using AST layer
//    - MoveStaticMethod(): Public API for solution-based static moves
//    - MoveInstanceMethod(): Public API for solution-based instance moves
//    - Helper methods: PrepareStaticMethodMove(), ExtractStaticMethodFromSource(), etc.
//
// 4. LEGACY COMPATIBILITY: String-based methods for backward compatibility
//    - MoveStaticMethodInSource(): Legacy string-based static moves
//    - MoveInstanceMethodInSource(): Legacy string-based instance moves
//
// 5. CONTEXT CLASSES: Encapsulate operation state and parameters
//    - StaticMoveContext: For static method transformations
//    - InstanceMoveContext: For instance method transformations
//    - FileOperationContext: For file-based operations
//    - StaticMethodMoveContext: For public API static method operations
//
// 6. ANALYZER CLASSES: Specialized syntax tree visitors for code analysis
//    - InstanceMemberUsageChecker: Detects instance member usage
//    - InstanceMemberRewriter: Rewrites instance member access
//    - MethodCallChecker: Detects method calls within methods
//    - MethodCallRewriter: Rewrites method calls with parameter access
//    - StaticFieldChecker: Detects static field references
//    - StaticFieldRewriter: Qualifies static field references
// ===============================================================================


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
        var context = CreateStaticMoveContext(sourceRoot, methodName, targetClass);
        var transformedMethod = TransformStaticMethodForMove(context);
        var stubMethod = CreateStaticStubMethod(context);
        var updatedSourceRoot = UpdateSourceRootWithStub(sourceRoot, context.Method, stubMethod);

        return new MoveStaticMethodResult
        {
            NewSourceRoot = updatedSourceRoot,
            MovedMethod = transformedMethod,
            StubMethod = stubMethod
        };
    }

    private class StaticMoveContext
    {
        public MethodDeclarationSyntax Method { get; set; }
        public ClassDeclarationSyntax SourceClass { get; set; }
        public string MethodName { get; set; }
        public string TargetClassName { get; set; }
        public HashSet<string> StaticFieldNames { get; set; }
        public bool NeedsStaticFieldQualification { get; set; }
        public TypeParameterListSyntax? TypeParameters { get; set; }
        public bool IsVoid { get; set; }
    }

    private static StaticMoveContext CreateStaticMoveContext(SyntaxNode sourceRoot, string methodName, string targetClass)
    {
        var method = FindStaticMethod(sourceRoot, methodName);
        var sourceClass = FindSourceClassForMethod(sourceRoot, method);
        var staticFieldNames = GetStaticFieldNames(sourceClass);

        return new StaticMoveContext
        {
            Method = method,
            SourceClass = sourceClass,
            MethodName = methodName,
            TargetClassName = targetClass,
            StaticFieldNames = staticFieldNames,
            NeedsStaticFieldQualification = HasStaticFieldReferences(method, staticFieldNames),
            TypeParameters = method.TypeParameterList,
            IsVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword)
        };
    }

    private static MethodDeclarationSyntax FindStaticMethod(SyntaxNode sourceRoot, string methodName)
    {
        var method = sourceRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                 m.Modifiers.Any(SyntaxKind.StaticKeyword));

        if (method == null)
            throw new McpException($"Error: Static method '{methodName}' not found");

        return method;
    }

    private static ClassDeclarationSyntax FindSourceClassForMethod(SyntaxNode sourceRoot, MethodDeclarationSyntax method)
    {
        var sourceClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Members.Contains(method));

        if (sourceClass == null)
            throw new McpException($"Error: Could not find source class for method '{method.Identifier.ValueText}'");

        return sourceClass;
    }

    private static MethodDeclarationSyntax TransformStaticMethodForMove(StaticMoveContext context)
    {
        var transformedMethod = context.Method;

        if (context.NeedsStaticFieldQualification)
        {
            var staticFieldRewriter = new StaticFieldRewriter(context.StaticFieldNames, context.SourceClass.Identifier.ValueText);
            transformedMethod = (MethodDeclarationSyntax)staticFieldRewriter.Visit(transformedMethod)!;
        }

        return transformedMethod;
    }

    private static MethodDeclarationSyntax CreateStaticStubMethod(StaticMoveContext context)
    {
        var argumentList = CreateStaticMethodArgumentList(context.Method);
        var methodExpression = CreateStaticMethodExpression(context);
        var invocation = CreateStaticMethodInvocation(context.TargetClassName, methodExpression, argumentList);
        var callStatement = CreateStaticCallStatement(context, invocation);

        return context.Method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    private static ArgumentListSyntax CreateStaticMethodArgumentList(MethodDeclarationSyntax method)
    {
        return SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                method.ParameterList.Parameters
                    .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)))));
    }

    private static SimpleNameSyntax CreateStaticMethodExpression(StaticMoveContext context)
    {
        var typeArgumentList = context.TypeParameters != null
            ? SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(
                    context.TypeParameters.Parameters.Select(p =>
                        (TypeSyntax)SyntaxFactory.IdentifierName(p.Identifier))))
            : null;

        return typeArgumentList != null
            ? SyntaxFactory.GenericName(context.MethodName).WithTypeArgumentList(typeArgumentList)
            : SyntaxFactory.IdentifierName(context.MethodName);
    }

    private static InvocationExpressionSyntax CreateStaticMethodInvocation(
        string targetClass,
        SimpleNameSyntax methodExpression,
        ArgumentListSyntax argumentList)
    {
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(targetClass),
                methodExpression),
            argumentList);
    }

    private static StatementSyntax CreateStaticCallStatement(StaticMoveContext context, InvocationExpressionSyntax invocation)
    {
        return context.IsVoid
            ? SyntaxFactory.ExpressionStatement(invocation)
            : SyntaxFactory.ReturnStatement(invocation);
    }

    private static SyntaxNode UpdateSourceRootWithStub(SyntaxNode sourceRoot, MethodDeclarationSyntax originalMethod, MethodDeclarationSyntax stubMethod)
    {
        return sourceRoot.ReplaceNode(originalMethod, stubMethod);
    }

    public static MoveInstanceMethodResult MoveInstanceMethodAst(
        SyntaxNode sourceRoot,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType)
    {
        var context = CreateInstanceMoveContext(sourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        var transformedMethod = TransformMethodForMove(context);
        var stubMethod = CreateStubMethod(context);
        var updatedSourceRoot = UpdateSourceClassWithStub(context, stubMethod);

        return new MoveInstanceMethodResult
        {
            NewSourceRoot = updatedSourceRoot,
            MovedMethod = transformedMethod,
            StubMethod = stubMethod,
            AccessMember = context.AccessMember,
            NeedsThisParameter = context.NeedsThisParameter
        };
    }

    private class InstanceMoveContext
    {
        public ClassDeclarationSyntax SourceClass { get; set; }
        public MethodDeclarationSyntax Method { get; set; }
        public string SourceClassName { get; set; }
        public string MethodName { get; set; }
        public string TargetClassName { get; set; }
        public string AccessMemberName { get; set; }
        public string AccessMemberType { get; set; }
        public HashSet<string> InstanceMembers { get; set; }
        public HashSet<string> MethodNames { get; set; }
        public HashSet<string> OtherMethodNames { get; set; }
        public bool UsesInstanceMembers { get; set; }
        public bool CallsOtherMethods { get; set; }
        public bool IsRecursive { get; set; }
        public bool NeedsThisParameter { get; set; }
        public MemberDeclarationSyntax? AccessMember { get; set; }
        public bool IsAsync { get; set; }
        public bool IsVoid { get; set; }
        public TypeParameterListSyntax? TypeParameters { get; set; }
    }

    private static InstanceMoveContext CreateInstanceMoveContext(
        SyntaxNode sourceRoot,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType)
    {
        var originClass = FindSourceClass(sourceRoot, sourceClass);
        var method = FindMethodInClass(originClass, methodName);

        var context = new InstanceMoveContext
        {
            SourceClass = originClass,
            Method = method,
            SourceClassName = sourceClass,
            MethodName = methodName,
            TargetClassName = targetClass,
            AccessMemberName = accessMemberName,
            AccessMemberType = accessMemberType,
            InstanceMembers = GetInstanceMemberNames(originClass),
            MethodNames = GetMethodNames(originClass),
            IsAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword),
            IsVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword),
            TypeParameters = method.TypeParameterList
        };

        context.OtherMethodNames = new HashSet<string>(context.MethodNames);
        context.OtherMethodNames.Remove(methodName);

        AnalyzeMethodDependencies(context);
        CreateAccessMemberIfNeeded(context);

        return context;
    }

    private static ClassDeclarationSyntax FindSourceClass(SyntaxNode sourceRoot, string sourceClass)
    {
        var originClass = sourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);

        if (originClass == null)
            throw new McpException($"Error: Source class '{sourceClass}' not found");

        return originClass;
    }

    private static MethodDeclarationSyntax FindMethodInClass(ClassDeclarationSyntax originClass, string methodName)
    {
        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);

        if (method == null)
            throw new McpException($"Error: No method named '{methodName}' found");

        return method;
    }

    private static void AnalyzeMethodDependencies(InstanceMoveContext context)
    {
        context.UsesInstanceMembers = HasInstanceMemberUsage(context.Method, context.InstanceMembers);
        context.CallsOtherMethods = HasMethodCalls(context.Method, context.OtherMethodNames);
        context.IsRecursive = HasMethodCalls(context.Method, new HashSet<string> { context.MethodName });
        context.NeedsThisParameter = context.UsesInstanceMembers || context.CallsOtherMethods || context.IsRecursive;
    }

    private static void CreateAccessMemberIfNeeded(InstanceMoveContext context)
    {
        bool accessMemberExists = MemberExists(context.SourceClass, context.AccessMemberName);
        if (!accessMemberExists)
        {
            context.AccessMember = CreateAccessMember(context.AccessMemberType, context.AccessMemberName, context.TargetClassName);
        }
    }

    private static MethodDeclarationSyntax TransformMethodForMove(InstanceMoveContext context)
    {
        var transformedMethod = context.Method;

        if (context.NeedsThisParameter)
        {
            transformedMethod = AddThisParameterToMethod(transformedMethod, context);
            transformedMethod = RewriteMethodBody(transformedMethod, context);
        }

        return EnsureMethodIsPublic(transformedMethod);
    }

    private static MethodDeclarationSyntax AddThisParameterToMethod(MethodDeclarationSyntax method, InstanceMoveContext context)
    {
        var sourceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(context.SourceClassName.ToLower()))
            .WithType(SyntaxFactory.IdentifierName(context.SourceClassName));

        var parameters = method.ParameterList.Parameters.Insert(0, sourceParameter);
        var newParameterList = method.ParameterList.WithParameters(parameters);

        return method.WithParameterList(newParameterList);
    }

    private static MethodDeclarationSyntax RewriteMethodBody(MethodDeclarationSyntax method, InstanceMoveContext context)
    {
        var parameterName = context.SourceClassName.ToLower();

        if (context.UsesInstanceMembers)
        {
            var memberRewriter = new InstanceMemberRewriter(parameterName, context.InstanceMembers);
            method = (MethodDeclarationSyntax)memberRewriter.Visit(method)!;
        }

        if (context.CallsOtherMethods)
        {
            var methodCallRewriter = new MethodCallRewriter(context.OtherMethodNames, parameterName);
            method = (MethodDeclarationSyntax)methodCallRewriter.Visit(method)!;
        }

        if (context.IsRecursive)
        {
            var recursiveCallRewriter = new MethodCallRewriter(new HashSet<string> { context.MethodName }, parameterName);
            method = (MethodDeclarationSyntax)recursiveCallRewriter.Visit(method)!;
        }

        return method;
    }

    private static MethodDeclarationSyntax EnsureMethodIsPublic(MethodDeclarationSyntax method)
    {
        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) &&
                                                  !m.IsKind(SyntaxKind.ProtectedKeyword) &&
                                                  !m.IsKind(SyntaxKind.InternalKeyword));
            return method.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }
        return method;
    }

    private static MethodDeclarationSyntax CreateStubMethod(InstanceMoveContext context)
    {
        var invocation = BuildDelegationInvocation(context);
        var callStatement = CreateDelegationStatement(context, invocation);

        return context.Method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    private static InvocationExpressionSyntax BuildDelegationInvocation(InstanceMoveContext context)
    {
        var originalParameters = context.Method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))).ToList();

        if (context.NeedsThisParameter)
        {
            originalParameters.Add(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
        }

        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(originalParameters));
        var accessExpression = SyntaxFactory.IdentifierName(context.AccessMemberName);
        var methodExpression = CreateMethodExpression(context);

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                accessExpression,
                methodExpression),
            argumentList);
    }

    private static SimpleNameSyntax CreateMethodExpression(InstanceMoveContext context)
    {
        var typeArgumentList = context.TypeParameters != null
            ? SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(
                    context.TypeParameters.Parameters.Select(p =>
                        (TypeSyntax)SyntaxFactory.IdentifierName(p.Identifier))))
            : null;

        return (typeArgumentList != null && context.NeedsThisParameter)
            ? SyntaxFactory.GenericName(context.MethodName).WithTypeArgumentList(typeArgumentList)
            : SyntaxFactory.IdentifierName(context.MethodName);
    }

    private static StatementSyntax CreateDelegationStatement(InstanceMoveContext context, InvocationExpressionSyntax invocation)
    {
        if (context.IsVoid)
        {
            return SyntaxFactory.ExpressionStatement(invocation);
        }

        ExpressionSyntax returnExpression = invocation;
        if (context.IsAsync)
        {
            returnExpression = SyntaxFactory.AwaitExpression(invocation);
        }

        return SyntaxFactory.ReturnStatement(returnExpression);
    }

    private static SyntaxNode UpdateSourceClassWithStub(InstanceMoveContext context, MethodDeclarationSyntax stubMethod)
    {
        var originMembers = context.SourceClass.Members.ToList();

        if (context.AccessMember != null)
        {
            var insertIndex = FindAccessMemberInsertionIndex(originMembers);
            originMembers.Insert(insertIndex, context.AccessMember);
        }

        var methodIndex = originMembers.FindIndex(m => m == context.Method);
        if (methodIndex >= 0)
        {
            originMembers[methodIndex] = stubMethod;
        }

        var newOriginClass = context.SourceClass.WithMembers(SyntaxFactory.List(originMembers));
        return context.SourceClass.SyntaxTree.GetRoot().ReplaceNode(context.SourceClass, newOriginClass);
    }

    private static int FindAccessMemberInsertionIndex(List<MemberDeclarationSyntax> members)
    {
        var fieldIndex = members.FindLastIndex(m => m is FieldDeclarationSyntax || m is PropertyDeclarationSyntax);
        return fieldIndex >= 0 ? fieldIndex + 1 : 0;
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
        ValidateFileExists(filePath);

        var context = await CreateFileOperationContext(filePath, targetFilePath, targetClass);
        var moveResult = MoveStaticMethodAst(context.SourceRoot, methodName, targetClass);

        var updatedTargetRoot = await PrepareTargetRoot(context, moveResult.MovedMethod);
        await WriteTransformedFiles(context, moveResult.NewSourceRoot, updatedTargetRoot);

        return $"Successfully moved static method '{methodName}' to {targetClass} in {context.TargetPath}";
    }

    private class FileOperationContext
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public bool SameFile { get; set; }
        public SyntaxNode SourceRoot { get; set; }
        public string TargetClassName { get; set; }
    }

    private static void ValidateFileExists(string filePath)
    {
        if (!File.Exists(filePath))
            throw new McpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");
    }

    private static async Task<FileOperationContext> CreateFileOperationContext(
        string filePath,
        string? targetFilePath,
        string targetClass)
    {
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");
        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        return new FileOperationContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = sourceRoot,
            TargetClassName = targetClass
        };
    }

    private static async Task<SyntaxNode> PrepareTargetRoot(FileOperationContext context, MethodDeclarationSyntax methodToMove)
    {
        SyntaxNode targetRoot;

        if (context.SameFile)
        {
            targetRoot = context.SourceRoot;
        }
        else
        {
            targetRoot = await LoadOrCreateTargetRoot(context.TargetPath);
            targetRoot = PropagateUsings(context.SourceRoot, targetRoot);
        }

        return AddMethodToTargetClass(targetRoot, context.TargetClassName, methodToMove);
    }

    private static async Task<SyntaxNode> LoadOrCreateTargetRoot(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var targetText = await File.ReadAllTextAsync(targetPath);
            return await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
        }
        else
        {
            return SyntaxFactory.CompilationUnit();
        }
    }

    private static async Task WriteTransformedFiles(
        FileOperationContext context,
        SyntaxNode newSourceRoot,
        SyntaxNode targetRoot)
    {
        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);

        if (!context.SameFile)
        {
            var formattedSource = Formatter.Format(newSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString());
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString());
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
        ValidateFileExists(filePath);

        var context = await CreateInstanceFileOperationContext(
            filePath, targetFilePath ?? filePath, targetClass, sourceClass, methodName, accessMemberName, accessMemberType);

        var moveResult = MoveInstanceMethodAst(
            context.SourceRoot, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);

        await ProcessInstanceMethodFileOperations(context, moveResult);

        return BuildInstanceMethodSuccessMessage(context, sourceClass, methodName, targetClass, targetFilePath);
    }

    private class InstanceFileOperationContext : FileOperationContext
    {
        public string SourceClassName { get; set; }
        public string MethodName { get; set; }
        public string AccessMemberName { get; set; }
        public string AccessMemberType { get; set; }
    }

    private static async Task<InstanceFileOperationContext> CreateInstanceFileOperationContext(
        string filePath,
        string targetPath,
        string targetClass,
        string sourceClass,
        string methodName,
        string accessMemberName,
        string accessMemberType)
    {
        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceRoot = await syntaxTree.GetRootAsync();

        return new InstanceFileOperationContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = sourceRoot,
            TargetClassName = targetClass,
            SourceClassName = sourceClass,
            MethodName = methodName,
            AccessMemberName = accessMemberName,
            AccessMemberType = accessMemberType
        };
    }

    private static async Task ProcessInstanceMethodFileOperations(
        InstanceFileOperationContext context,
        MoveInstanceMethodResult moveResult)
    {
        if (context.SameFile)
        {
            await ProcessSameFileInstanceMove(context, moveResult);
        }
        else
        {
            await ProcessCrossFileInstanceMove(context, moveResult);
        }
    }

    private static async Task ProcessSameFileInstanceMove(
        InstanceFileOperationContext context,
        MoveInstanceMethodResult moveResult)
    {
        var targetRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, context.TargetClassName, moveResult.MovedMethod);
        var formatted = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        await File.WriteAllTextAsync(context.TargetPath, formatted.ToFullString());
    }

    private static async Task ProcessCrossFileInstanceMove(
        InstanceFileOperationContext context,
        MoveInstanceMethodResult moveResult)
    {
        // Handle source file
        var formattedSource = Formatter.Format(moveResult.NewSourceRoot, RefactoringHelpers.SharedWorkspace);
        await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString());

        // Handle target file
        var targetRoot = await LoadOrCreateTargetRoot(context.TargetPath);
        targetRoot = PropagateUsings(context.SourceRoot, targetRoot);
        targetRoot = AddMethodToTargetClass(targetRoot, context.TargetClassName, moveResult.MovedMethod);

        var formattedTarget = Formatter.Format(targetRoot, RefactoringHelpers.SharedWorkspace);
        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString());
    }

    private static string BuildInstanceMethodSuccessMessage(
        InstanceFileOperationContext context,
        string sourceClass,
        string methodName,
        string targetClass,
        string? targetFilePath)
    {
        var locationInfo = targetFilePath != null ? $" in {context.TargetPath}" : "";
        return $"Successfully moved instance method {sourceClass}.{methodName} to {targetClass}{locationInfo}";
    }

    // ===== SOLUTION OPERATION LAYER =====
    // Solution/Document operations that use the AST layer

    internal static async Task<(string Message, Document UpdatedDocument)> MoveInstanceMethodWithSolution(
        Document document,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync();

        // Perform AST transformation
        var moveResult = MoveInstanceMethodAst(
            syntaxRoot!,
            sourceClass,
            methodName,
            targetClass,
            accessMemberName,
            accessMemberType);

        // Add method to target class in the same document
        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod);

        // Format and update the document
        var formatted = Formatter.Format(finalRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());
        RefactoringHelpers.UpdateSolutionCache(newDocument);

        var message = $"Successfully moved {sourceClass}.{methodName} instance method to {targetClass} in {document.FilePath}";
        return (message, newDocument);
    }

    internal static async Task<(string Message, Document UpdatedDocument)> MoveStaticMethodWithSolution(
        Document document,
        string methodName,
        string targetClass,
        string? targetFilePath)
    {
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(document.FilePath!)!, $"{targetClass}.cs");
        var sameFile = targetPath == document.FilePath;
        var sourceRoot = await document.GetSyntaxRootAsync();

        var context = new FileOperationContext
        {
            SourcePath = document.FilePath!,
            TargetPath = targetPath,
            SameFile = sameFile,
            SourceRoot = sourceRoot!,
            TargetClassName = targetClass
        };

        var moveResult = MoveStaticMethodAst(context.SourceRoot, methodName, targetClass);
        var updatedTargetRoot = await PrepareTargetRoot(context, moveResult.MovedMethod);
        await WriteTransformedFiles(context, moveResult.NewSourceRoot, updatedTargetRoot);

        SyntaxNode newRoot = sameFile
            ? Formatter.Format(updatedTargetRoot, document.Project.Solution.Workspace)
            : Formatter.Format(moveResult.NewSourceRoot, document.Project.Solution.Workspace);

        var updatedDocument = document.WithSyntaxRoot(newRoot);
        RefactoringHelpers.UpdateSolutionCache(updatedDocument);
        var message = $"Successfully moved static method '{methodName}' to {targetClass} in {context.TargetPath}";
        return (message, updatedDocument);
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
            ValidateFileExists(filePath);

            var moveContext = await PrepareStaticMethodMove(filePath, targetFilePath, targetClass);
            var solution = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
            var duplicateDoc = await RefactoringHelpers.FindClassInSolution(
                solution,
                targetClass,
                filePath,
                moveContext.TargetPath);
            if (duplicateDoc != null)
                return RefactoringHelpers.ThrowMcpException($"Error: Class {targetClass} already exists in {duplicateDoc.FilePath}");
            var method = ExtractStaticMethodFromSource(moveContext.SourceRoot, methodName);
            var updatedSources = UpdateSourceAndTargetForStaticMove(moveContext, method);
            await WriteStaticMethodMoveResults(moveContext, updatedSources);

            return $"Successfully moved static method '{methodName}' to {targetClass} in {moveContext.TargetPath}";
        }
        catch (Exception ex)
        {
            throw new McpException($"Error moving static method: {ex.Message}", ex);
        }
    }

    private class StaticMethodMoveContext
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public bool SameFile { get; set; }
        public SyntaxNode SourceRoot { get; set; }
        public List<UsingDirectiveSyntax> SourceUsings { get; set; }
        public string TargetClassName { get; set; }
    }

    private class SourceAndTargetRoots
    {
        public SyntaxNode UpdatedSourceRoot { get; set; }
        public SyntaxNode UpdatedTargetRoot { get; set; }
    }

    private static async Task<StaticMethodMoveContext> PrepareStaticMethodMove(
        string filePath,
        string? targetFilePath,
        string targetClass)
    {
        var sourceText = await File.ReadAllTextAsync(filePath);
        var targetPath = targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs");

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var syntaxRoot = await syntaxTree.GetRootAsync();
        var sourceUsings = syntaxRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

        return new StaticMethodMoveContext
        {
            SourcePath = filePath,
            TargetPath = targetPath,
            SameFile = targetPath == filePath,
            SourceRoot = syntaxRoot,
            SourceUsings = sourceUsings,
            TargetClassName = targetClass
        };
    }

    private static MethodDeclarationSyntax ExtractStaticMethodFromSource(SyntaxNode sourceRoot, string methodName)
    {
        var method = sourceRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                m.Modifiers.Any(SyntaxKind.StaticKeyword));

        if (method == null)
            throw new McpException($"Error: Static method '{methodName}' not found");

        return method;
    }

    private static SourceAndTargetRoots UpdateSourceAndTargetForStaticMove(
        StaticMethodMoveContext context,
        MethodDeclarationSyntax method)
    {
        var newSourceRoot = context.SourceRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
        var targetRoot = PrepareTargetRootForStaticMove(context);
        var updatedTargetRoot = AddMethodToTargetClass(targetRoot, context.TargetClassName, method);

        return new SourceAndTargetRoots
        {
            UpdatedSourceRoot = newSourceRoot!,
            UpdatedTargetRoot = updatedTargetRoot
        };
    }

    private static SyntaxNode PrepareTargetRootForStaticMove(StaticMethodMoveContext context)
    {
        SyntaxNode targetRoot;

        if (context.SameFile)
        {
            targetRoot = context.SourceRoot;
        }
        else if (File.Exists(context.TargetPath))
        {
            var targetText = File.ReadAllText(context.TargetPath);
            targetRoot = CSharpSyntaxTree.ParseText(targetText).GetRoot();
        }
        else
        {
            targetRoot = SyntaxFactory.CompilationUnit();
        }

        return PropagateUsingsToTarget(context, targetRoot);
    }

    private static SyntaxNode PropagateUsingsToTarget(StaticMethodMoveContext context, SyntaxNode targetRoot)
    {
        var targetCompilationUnit = (CompilationUnitSyntax)targetRoot;
        var targetUsingNames = targetCompilationUnit.Usings
            .Select(u => u.Name.ToString())
            .ToHashSet();

        var missingUsings = context.SourceUsings
            .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
            .ToArray();

        if (missingUsings.Length > 0)
        {
            targetCompilationUnit = targetCompilationUnit.AddUsings(missingUsings);
            return targetCompilationUnit;
        }

        return targetRoot;
    }

    private static async Task WriteStaticMethodMoveResults(
        StaticMethodMoveContext context,
        SourceAndTargetRoots updatedRoots)
    {
        var formattedTarget = Formatter.Format(updatedRoots.UpdatedTargetRoot, RefactoringHelpers.SharedWorkspace);

        if (!context.SameFile)
        {
            var formattedSource = Formatter.Format(updatedRoots.UpdatedSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(context.SourcePath, formattedSource.ToFullString());
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPath)!);
        await File.WriteAllTextAsync(context.TargetPath, formattedTarget.ToFullString());
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

            var duplicateDoc = await RefactoringHelpers.FindClassInSolution(
                solution,
                targetClass,
                filePath,
                targetFilePath ?? Path.Combine(Path.GetDirectoryName(filePath)!, $"{targetClass}.cs"));
            if (duplicateDoc != null)
                return RefactoringHelpers.ThrowMcpException($"Error: Class {targetClass} already exists in {duplicateDoc.FilePath}");

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
                var (msg, updatedDoc) = await MoveInstanceMethodWithSolution(
                    currentDocument,
                    sourceClass,
                    name,
                    targetClass,
                    accessMemberName,
                    accessMemberType);
                results.Add(msg);

                // Use returned document for subsequent operations
                currentDocument = updatedDoc;
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
