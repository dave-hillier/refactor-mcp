using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

public static partial class MoveMethodsTool
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
}
