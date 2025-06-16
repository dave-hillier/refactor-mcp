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
        public string? Namespace { get; set; }
    }

    public class MoveInstanceMethodResult
    {
        public SyntaxNode NewSourceRoot { get; set; }
        public MethodDeclarationSyntax MovedMethod { get; set; }
        public MethodDeclarationSyntax StubMethod { get; set; }
        public MemberDeclarationSyntax? AccessMember { get; set; }
        public bool NeedsThisParameter { get; set; }
        public string? Namespace { get; set; }
    }

    public static MoveStaticMethodResult MoveStaticMethodAst(
        SyntaxNode sourceRoot,
        string methodName,
        string targetClass)
    {
        var method = FindStaticMethod(sourceRoot, methodName);
        var sourceClass = FindSourceClassForMethod(sourceRoot, method);
        var staticFieldNames = GetStaticFieldNames(sourceClass);
        var needsQualification = HasStaticFieldReferences(method, staticFieldNames);
        var typeParameters = method.TypeParameterList;
        var isVoid = method.ReturnType is PredefinedTypeSyntax pts &&
                     pts.Keyword.IsKind(SyntaxKind.VoidKeyword);

        var transformedMethod = TransformStaticMethodForMove(
            method,
            needsQualification,
            staticFieldNames,
            sourceClass.Identifier.ValueText);
        var stubMethod = CreateStaticStubMethod(
            method,
            methodName,
            targetClass,
            isVoid,
            typeParameters);
        var updatedSourceRoot = UpdateSourceRootWithStub(sourceRoot, method, stubMethod);

        var ns = (sourceClass.Parent as NamespaceDeclarationSyntax)?.Name.ToString()
                 ?? (sourceClass.Parent as FileScopedNamespaceDeclarationSyntax)?.Name.ToString();

        return new MoveStaticMethodResult
        {
            NewSourceRoot = updatedSourceRoot,
            MovedMethod = transformedMethod,
            StubMethod = stubMethod,
            Namespace = ns
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

    private static MethodDeclarationSyntax TransformStaticMethodForMove(
        MethodDeclarationSyntax method,
        bool needsStaticFieldQualification,
        HashSet<string> staticFieldNames,
        string sourceClassName)
    {
        var transformedMethod = method;

        if (needsStaticFieldQualification)
        {
            var staticFieldRewriter = new StaticFieldRewriter(staticFieldNames, sourceClassName);
            transformedMethod = (MethodDeclarationSyntax)staticFieldRewriter.Visit(transformedMethod)!;
        }

        return transformedMethod;
    }

    private static MethodDeclarationSyntax CreateStaticStubMethod(
        MethodDeclarationSyntax method,
        string methodName,
        string targetClassName,
        bool isVoid,
        TypeParameterListSyntax? typeParameters)
    {
        var argumentList = CreateStaticMethodArgumentList(method);
        var methodExpression = CreateStaticMethodExpression(methodName, typeParameters);
        var invocation = CreateStaticMethodInvocation(targetClassName, methodExpression, argumentList);
        var callStatement = CreateStaticCallStatement(isVoid, invocation);

        return method.WithBody(SyntaxFactory.Block(callStatement))
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

    private static SimpleNameSyntax CreateStaticMethodExpression(
        string methodName,
        TypeParameterListSyntax? typeParameters)
    {
        var typeArgumentList = typeParameters != null
            ? SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(
                    typeParameters.Parameters.Select(p =>
                        (TypeSyntax)SyntaxFactory.IdentifierName(p.Identifier))))
            : null;

        return typeArgumentList != null
            ? SyntaxFactory.GenericName(methodName).WithTypeArgumentList(typeArgumentList)
            : SyntaxFactory.IdentifierName(methodName);
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

    private static StatementSyntax CreateStaticCallStatement(bool isVoid, InvocationExpressionSyntax invocation)
    {
        return isVoid
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
        var originClass = FindSourceClass(sourceRoot, sourceClass);
        var method = FindMethodInClass(originClass, methodName);

        var instanceMembers = GetInstanceMemberNames(originClass);
        var methodNames = GetMethodNames(originClass);
        var privateFieldInfos = GetPrivateFieldInfos(originClass);
        var usedPrivateFields = GetUsedPrivateFields(method, new HashSet<string>(privateFieldInfos.Keys));

        var membersForAnalysis = new HashSet<string>(instanceMembers);
        foreach (var f in usedPrivateFields)
            membersForAnalysis.Remove(f);

        var analysis = new MethodAnalysisWalker(membersForAnalysis, methodNames, methodName);
        analysis.Visit(method);

        bool usesInstanceMembers = analysis.UsesInstanceMembers;
        bool callsOtherMethods = analysis.CallsOtherMethods;
        bool isRecursive = analysis.IsRecursive;
        bool needsThisParameter = usesInstanceMembers || callsOtherMethods || isRecursive;

        var otherMethodNames = new HashSet<string>(methodNames);
        otherMethodNames.Remove(methodName);

        var accessMember = MemberExists(originClass, accessMemberName)
            ? null
            : CreateAccessMember(accessMemberType, accessMemberName, targetClass);

        bool isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        bool isVoid = method.ReturnType is PredefinedTypeSyntax pts &&
                       pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        var typeParameters = method.TypeParameterList;

        var injectedParameters = usedPrivateFields
            .Select(n => SyntaxFactory.Parameter(SyntaxFactory.Identifier(n))
                .WithType(privateFieldInfos[n]))
            .ToList();

        var transformedMethod = TransformMethodForMove(
            method,
            sourceClass,
            methodName,
            needsThisParameter,
            usesInstanceMembers,
            callsOtherMethods,
            isRecursive,
            membersForAnalysis,
            otherMethodNames,
            injectedParameters,
            usedPrivateFields);

        var stubMethod = CreateStubMethod(
            method,
            methodName,
            accessMemberName,
            accessMemberType,
            needsThisParameter,
            isVoid,
            isAsync,
            typeParameters,
            usedPrivateFields);

        var updatedSourceRoot = UpdateSourceClassWithStub(originClass, method, stubMethod, accessMember);

        var ns = (originClass.Parent as NamespaceDeclarationSyntax)?.Name.ToString()
                 ?? (originClass.Parent as FileScopedNamespaceDeclarationSyntax)?.Name.ToString();

        return new MoveInstanceMethodResult
        {
            NewSourceRoot = updatedSourceRoot,
            MovedMethod = transformedMethod,
            StubMethod = stubMethod,
            AccessMember = accessMember,
            NeedsThisParameter = needsThisParameter,
            Namespace = ns
        };
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


    private static MethodDeclarationSyntax TransformMethodForMove(
        MethodDeclarationSyntax method,
        string sourceClassName,
        string methodName,
        bool needsThisParameter,
        bool usesInstanceMembers,
        bool callsOtherMethods,
        bool isRecursive,
        HashSet<string> instanceMembers,
        HashSet<string> otherMethodNames,
        List<ParameterSyntax> injectedParameters,
        HashSet<string> injectedNames)
    {
        var transformedMethod = method;

        if (injectedParameters.Count > 0)
        {
            var newParams = transformedMethod.ParameterList.Parameters.AddRange(injectedParameters);
            transformedMethod = transformedMethod.WithParameterList(transformedMethod.ParameterList.WithParameters(newParams));
            var map = injectedNames.ToDictionary(n => n, n => (ExpressionSyntax)SyntaxFactory.IdentifierName(n));
            var rewriter = new ParameterRewriter(map);
            transformedMethod = (MethodDeclarationSyntax)rewriter.Visit(transformedMethod)!;
        }

        if (needsThisParameter)
        {
            transformedMethod = AddThisParameterToMethod(transformedMethod, sourceClassName);
            transformedMethod = RewriteMethodBody(
                transformedMethod,
                methodName,
                usesInstanceMembers,
                callsOtherMethods,
                isRecursive,
                instanceMembers,
                otherMethodNames);
        }

        return EnsureMethodIsPublic(transformedMethod);
    }

    private static MethodDeclarationSyntax AddThisParameterToMethod(
        MethodDeclarationSyntax method,
        string sourceClassName)
    {
        var sourceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier("@this"))
            .WithType(SyntaxFactory.IdentifierName(sourceClassName));

        var parameters = method.ParameterList.Parameters.Insert(0, sourceParameter);
        var newParameterList = method.ParameterList.WithParameters(parameters);

        return method.WithParameterList(newParameterList);
    }

    private static MethodDeclarationSyntax RewriteMethodBody(
        MethodDeclarationSyntax method,
        string methodName,
        bool usesInstanceMembers,
        bool callsOtherMethods,
        bool isRecursive,
        HashSet<string> instanceMembers,
        HashSet<string> otherMethodNames)
    {
        var parameterName = "@this";

        if (usesInstanceMembers)
        {
            var memberRewriter = new InstanceMemberRewriter(parameterName, instanceMembers);
            method = (MethodDeclarationSyntax)memberRewriter.Visit(method)!;
        }

        if (callsOtherMethods)
        {
            var methodCallRewriter = new MethodCallRewriter(otherMethodNames, parameterName);
            method = (MethodDeclarationSyntax)methodCallRewriter.Visit(method)!;
        }

        if (isRecursive)
        {
            var recursiveCallRewriter = new MethodCallRewriter(new HashSet<string> { methodName }, parameterName);
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

    private static MethodDeclarationSyntax CreateStubMethod(
        MethodDeclarationSyntax method,
        string methodName,
        string accessMemberName,
        string accessMemberType,
        bool needsThisParameter,
        bool isVoid,
        bool isAsync,
        TypeParameterListSyntax? typeParameters,
        IEnumerable<string> fieldArguments)
    {
        var invocation = BuildDelegationInvocation(
            method,
            methodName,
            accessMemberName,
            accessMemberType,
            needsThisParameter,
            typeParameters,
            fieldArguments);
        var callStatement = CreateDelegationStatement(isVoid, isAsync, invocation);

        return method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    private static InvocationExpressionSyntax BuildDelegationInvocation(
        MethodDeclarationSyntax method,
        string methodName,
        string accessMemberName,
        string accessMemberType,
        bool needsThisParameter,
        TypeParameterListSyntax? typeParameters,
        IEnumerable<string> fieldArguments)
    {
        var originalParameters = method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))).ToList();

        if (needsThisParameter)
        {
            originalParameters.Insert(0, SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
        }

        foreach (var fieldName in fieldArguments)
        {
            originalParameters.Add(SyntaxFactory.Argument(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ThisExpression(),
                    SyntaxFactory.IdentifierName(fieldName))));
        }

        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(originalParameters));

        ExpressionSyntax accessExpression;
        if (string.Equals(accessMemberType, "field", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(accessMemberType, "property", StringComparison.OrdinalIgnoreCase))
        {
            accessExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ThisExpression(),
                SyntaxFactory.IdentifierName(accessMemberName));
        }
        else
        {
            accessExpression = SyntaxFactory.IdentifierName(accessMemberName);
        }

        var methodExpression = CreateMethodExpression(methodName, typeParameters, needsThisParameter);

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                accessExpression,
                methodExpression),
            argumentList);
    }

    private static SimpleNameSyntax CreateMethodExpression(
        string methodName,
        TypeParameterListSyntax? typeParameters,
        bool needsThisParameter)
    {
        var typeArgumentList = typeParameters != null
            ? SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SeparatedList(
                    typeParameters.Parameters.Select(p =>
                        (TypeSyntax)SyntaxFactory.IdentifierName(p.Identifier))))
            : null;

        return (typeArgumentList != null && needsThisParameter)
            ? SyntaxFactory.GenericName(methodName).WithTypeArgumentList(typeArgumentList)
            : SyntaxFactory.IdentifierName(methodName);
    }

    private static StatementSyntax CreateDelegationStatement(
        bool isVoid,
        bool isAsync,
        InvocationExpressionSyntax invocation)
    {
        if (isVoid)
        {
            return SyntaxFactory.ExpressionStatement(invocation);
        }

        ExpressionSyntax returnExpression = invocation;
        if (isAsync)
        {
            returnExpression = SyntaxFactory.AwaitExpression(invocation);
        }

        return SyntaxFactory.ReturnStatement(returnExpression);
    }

    private static SyntaxNode UpdateSourceClassWithStub(
        ClassDeclarationSyntax sourceClass,
        MethodDeclarationSyntax originalMethod,
        MethodDeclarationSyntax stubMethod,
        MemberDeclarationSyntax? accessMember)
    {
        var originMembers = sourceClass.Members.ToList();

        if (accessMember != null)
        {
            var insertIndex = FindAccessMemberInsertionIndex(originMembers);
            originMembers.Insert(insertIndex, accessMember);
        }

        var methodIndex = originMembers.FindIndex(m => m == originalMethod);
        if (methodIndex >= 0)
        {
            originMembers[methodIndex] = stubMethod;
        }

        var newOriginClass = sourceClass.WithMembers(SyntaxFactory.List(originMembers));
        return sourceClass.SyntaxTree.GetRoot().ReplaceNode(sourceClass, newOriginClass);
    }

    private static int FindAccessMemberInsertionIndex(List<MemberDeclarationSyntax> members)
    {
        var fieldIndex = members.FindLastIndex(m => m is FieldDeclarationSyntax || m is PropertyDeclarationSyntax);
        return fieldIndex >= 0 ? fieldIndex + 1 : 0;
    }

    public static SyntaxNode AddMethodToTargetClass(
        SyntaxNode targetRoot,
        string targetClass,
        MethodDeclarationSyntax method,
        string? namespaceName = null)
    {
        var targetClassDecl = targetRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(method.WithLeadingTrivia());
            var compilationUnit = (CompilationUnitSyntax)targetRoot;

            var nsDecl = compilationUnit.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsDecl != null)
            {
                var updatedNs = nsDecl.AddMembers(newClass);
                return compilationUnit.ReplaceNode(nsDecl, updatedNs);
            }
            else if (!string.IsNullOrEmpty(namespaceName))
            {
                var ns = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                    .AddMembers(newClass);
                return compilationUnit.AddMembers(ns);
            }
            else
            {
                return compilationUnit.AddMembers(newClass);
            }
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
