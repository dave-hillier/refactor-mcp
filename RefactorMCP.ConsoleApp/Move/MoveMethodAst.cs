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
using RefactorMCP.ConsoleApp.SyntaxRewriters;
using RefactorMCP.ConsoleApp.SyntaxWalkers;

namespace RefactorMCP.ConsoleApp.Move;

public static partial class MoveMethodAst
{
    // ===== AST TRANSFORMATION LAYER =====
    // Pure syntax tree operations with no file I/O

    public class MoveStaticMethodResult
    {
        public SyntaxNode NewSourceRoot { get; set; } = null!;
        public MethodDeclarationSyntax MovedMethod { get; set; } = null!;
        public MethodDeclarationSyntax StubMethod { get; set; } = null!;
        public string? Namespace { get; set; }
    }

    public class MoveInstanceMethodResult
    {
        public SyntaxNode NewSourceRoot { get; set; } = null!;
        public MethodDeclarationSyntax MovedMethod { get; set; } = null!;
        public MethodDeclarationSyntax StubMethod { get; set; } = null!;
        public MemberDeclarationSyntax? AccessMember { get; set; }
        public MethodDeclarationSyntax? BaseWrapper { get; set; }
        public bool NeedsThisParameter { get; set; }
        public string? Namespace { get; set; }
    }

    public static MoveStaticMethodResult MoveStaticMethodAst(
        SyntaxNode sourceRoot,
        string methodName,
        string targetClass)
    {
        var method = FindStaticMethod(sourceRoot, methodName);
        if (method.Modifiers.Any(SyntaxKind.ProtectedKeyword) &&
            method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            throw new McpException($"Error: Cannot move protected override method '{methodName}'");
        var sourceClass = FindSourceClassForMethod(sourceRoot, method);
        var methodNames = GetMethodNames(sourceClass);
        var collector = new CalledMethodCollector(methodNames);
        collector.Visit(method);
        var calledMethods = collector.CalledMethods;
        var staticFieldNames = GetStaticFieldNames(sourceClass);
        var nestedClassNames = GetNestedClassNames(sourceClass);
        var needsQualification = HasStaticFieldReferences(method, staticFieldNames);
        var typeParameters = method.TypeParameterList;
        var isVoid = method.ReturnType is PredefinedTypeSyntax pts &&
                     pts.Keyword.IsKind(SyntaxKind.VoidKeyword);

        var transformedMethod = TransformStaticMethodForMove(
            method,
            needsQualification,
            staticFieldNames,
            nestedClassNames,
            sourceClass.Identifier.ValueText);
        transformedMethod = EnsureMethodIsInternal(transformedMethod);
        var stubMethod = CreateStaticStubMethod(
            method,
            methodName,
            targetClass,
            isVoid,
            typeParameters);
        var dependencyUpdates = new Dictionary<string, MethodDeclarationSyntax>();
        foreach (var m in sourceClass.Members.OfType<MethodDeclarationSyntax>())
        {
            if (calledMethods.Contains(m.Identifier.ValueText) &&
                !m.Modifiers.Any(t => t.IsKind(SyntaxKind.PublicKeyword) || t.IsKind(SyntaxKind.InternalKeyword)))
            {
                dependencyUpdates[m.Identifier.ValueText] = EnsureMethodIsInternal(m);
            }
        }
        var updatedSourceRoot = UpdateSourceRootWithStub(sourceRoot, method, stubMethod, dependencyUpdates);

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

    private static bool HasParameterUsage(MethodDeclarationSyntax method, string parameterName)
    {
        var nodes = new List<SyntaxNode>();
        if (method.Body != null)
            nodes.Add(method.Body);
        if (method.ExpressionBody != null)
            nodes.Add(method.ExpressionBody);
        
        var allNodes = nodes.SelectMany(n => n.DescendantNodes());
        
        // Check for direct identifier usage
        var hasIdentifierUsage = allNodes
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.ValueText == parameterName);
            
        // Check for usage in member access expressions (e.g., parameterName.SomeProperty)
        var hasMemberAccessUsage = allNodes
            .OfType<MemberAccessExpressionSyntax>()
            .Any(ma => ma.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == parameterName);
            
        return hasIdentifierUsage || hasMemberAccessUsage;
    }

    private static MethodDeclarationSyntax RemoveParameter(MethodDeclarationSyntax method, string parameterName)
    {
        var parameters = method.ParameterList.Parameters;
        var index = parameters.ToList().FindIndex(p => p.Identifier.ValueText == parameterName);
        if (index >= 0)
        {
            parameters = parameters.RemoveAt(index);
            method = method.WithParameterList(method.ParameterList.WithParameters(parameters));
        }
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
        HashSet<string> nestedClassNames,
        string sourceClassName)
    {
        var transformedMethod = method;

        if (needsStaticFieldQualification)
        {
            var staticFieldRewriter = new StaticFieldRewriter(staticFieldNames, sourceClassName);
            transformedMethod = (MethodDeclarationSyntax)staticFieldRewriter.Visit(transformedMethod)!;
        }

        if (nestedClassNames.Count > 0)
        {
            var nestedRewriter = new NestedClassRewriter(nestedClassNames, sourceClassName);
            transformedMethod = (MethodDeclarationSyntax)nestedRewriter.Visit(transformedMethod)!;
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

    private static SyntaxNode UpdateSourceRootWithStub(
        SyntaxNode sourceRoot,
        MethodDeclarationSyntax originalMethod,
        MethodDeclarationSyntax stubMethod,
        Dictionary<string, MethodDeclarationSyntax>? dependencyUpdates = null)
    {
        var newRoot = sourceRoot.ReplaceNode(originalMethod, stubMethod);

        if (dependencyUpdates != null)
        {
            foreach (var kvp in dependencyUpdates)
            {
                var dep = newRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == kvp.Key);
                if (dep != null)
                    newRoot = newRoot.ReplaceNode(dep, kvp.Value);
            }
        }

        return newRoot;
    }

    public static MoveInstanceMethodResult MoveInstanceMethodAst(
        SyntaxNode sourceRoot,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType,
        IEnumerable<string>? parameterInjections = null)
    {
        var originClass = FindSourceClass(sourceRoot, sourceClass);
        var method = FindMethodInClass(originClass, methodName);
        if (method.Modifiers.Any(SyntaxKind.ProtectedKeyword) &&
            method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            throw new McpException($"Error: Cannot move protected override method '{methodName}'");

        var nestedClassNames = GetNestedClassNames(originClass);

        var instanceMembers = GetInstanceMemberNames(originClass);
        instanceMembers.UnionWith(GetImplicitInstanceMembers(method));
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
        bool hasThisUsage = method.DescendantNodes()
            .OfType<ThisExpressionSyntax>()
            .Any(t => t.Parent is not MemberAccessExpressionSyntax);
        bool callsBase = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                        ma.Expression is BaseExpressionSyntax &&
                        ma.Name is IdentifierNameSyntax id &&
                        id.Identifier.ValueText == methodName);
        parameterInjections ??= Array.Empty<string>();
        bool forceThisParameter = parameterInjections.Contains("this");
        bool needsThisParameter = hasThisUsage || usesInstanceMembers || callsOtherMethods || isRecursive || callsBase || forceThisParameter;
        var thisParameterName = forceThisParameter
            ? MoveMethodFileService.GetParameterName("this", sourceClass)
            : "@this";

        var otherMethodNames = new HashSet<string>(methodNames);
        otherMethodNames.Remove(methodName);

        MemberDeclarationSyntax? accessMember;
        string actualAccessName;

        bool isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        bool isVoid = method.ReturnType is PredefinedTypeSyntax pts &&
                       pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        var typeParameters = method.TypeParameterList;

        MethodDeclarationSyntax? baseWrapper = callsBase ? CreateBaseWrapper(method) : null;

        var paramMap = usedPrivateFields.ToDictionary(n => n, n => n.TrimStart('_'));
        var injectedParameters = paramMap
            .Select(kvp => SyntaxFactory.Parameter(SyntaxFactory.Identifier(kvp.Value))
                .WithType(privateFieldInfos[kvp.Key]))
            .ToList();

        foreach (var inj in parameterInjections)
        {
            if (inj == "this")
                continue;
            if (!privateFieldInfos.TryGetValue(inj, out var type))
                continue;
            if (!paramMap.ContainsKey(inj))
            {
                var name = MoveMethodFileService.GetParameterName(inj, sourceClass);
                paramMap[inj] = name;
                injectedParameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)).WithType(type));
            }
        }

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
            nestedClassNames,
            injectedParameters,
            paramMap,
            thisParameterName);
        transformedMethod = EnsureMethodIsInternal(transformedMethod);

        if (callsBase && baseWrapper != null)
        {
            var rewriter = new BaseCallRewriter(methodName, thisParameterName, baseWrapper.Identifier.ValueText);
            transformedMethod = (MethodDeclarationSyntax)rewriter.Visit(transformedMethod)!;
        }

        var collector = new CalledMethodCollector(methodNames);
        collector.Visit(method);
        var calledMethods = collector.CalledMethods;

        if (needsThisParameter && !HasParameterUsage(transformedMethod, thisParameterName))
        {
            transformedMethod = RemoveParameter(transformedMethod, thisParameterName);
            needsThisParameter = false;
        }

        var isStaticMethod = transformedMethod.Modifiers.Any(SyntaxKind.StaticKeyword);
        actualAccessName = isStaticMethod ? targetClass : accessMemberName;
        accessMember = isStaticMethod
            ? null
            : MemberExists(originClass, accessMemberName)
                ? null
                : CreateAccessMember(accessMemberType, accessMemberName, targetClass);

        var stubMethod = CreateStubMethod(
            method,
            methodName,
            actualAccessName,
            accessMemberType,
            needsThisParameter,
            isVoid,
            isAsync,
            typeParameters,
            paramMap.Keys);

        var dependencyUpdates = new Dictionary<string, MethodDeclarationSyntax>();
        foreach (var m in originClass.Members.OfType<MethodDeclarationSyntax>())
        {
            if (calledMethods.Contains(m.Identifier.ValueText) &&
                !m.Modifiers.Any(t => t.IsKind(SyntaxKind.PublicKeyword) || t.IsKind(SyntaxKind.InternalKeyword)))
            {
                dependencyUpdates[m.Identifier.ValueText] = EnsureMethodIsInternal(m);
            }
        }

        var updatedSourceRoot = UpdateSourceClassWithStub(originClass, method, stubMethod, accessMember, dependencyUpdates, baseWrapper);

        var ns = (originClass.Parent as NamespaceDeclarationSyntax)?.Name.ToString()
                 ?? (originClass.Parent as FileScopedNamespaceDeclarationSyntax)?.Name.ToString();

        return new MoveInstanceMethodResult
        {
            NewSourceRoot = updatedSourceRoot,
            MovedMethod = transformedMethod,
            StubMethod = stubMethod,
            AccessMember = accessMember,
            BaseWrapper = baseWrapper,
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
        HashSet<string> nestedClassNames,
        List<ParameterSyntax> injectedParameters,
        Dictionary<string, string> injectedParameterMap,
        string thisParameterName)
    {
        var transformedMethod = method;

        // Apply parameter rewriting BEFORE other transformations to avoid casting issues
        if (injectedParameters.Count > 0)
        {
            var map = injectedParameterMap.ToDictionary(kvp => kvp.Key, kvp => (ExpressionSyntax)SyntaxFactory.IdentifierName(kvp.Value));
            var rewriter = new ParameterRewriter(map);
            transformedMethod = (MethodDeclarationSyntax)rewriter.Visit(transformedMethod)!;
        }

        if (needsThisParameter)
        {
            transformedMethod = AddThisParameterToMethod(transformedMethod, sourceClassName, thisParameterName);
            transformedMethod = RewriteMethodBody(
                transformedMethod,
                methodName,
                thisParameterName,
                usesInstanceMembers,
                callsOtherMethods,
                isRecursive,
                instanceMembers,
                otherMethodNames);
        }

        // Add injected parameters to the parameter list after transformation
        if (injectedParameters.Count > 0)
        {
            var parameters = transformedMethod.ParameterList.Parameters;
            var insertIndex = needsThisParameter ? 1 : 0;
            parameters = parameters.InsertRange(insertIndex, injectedParameters);
            transformedMethod = transformedMethod.WithParameterList(transformedMethod.ParameterList.WithParameters(parameters));
        }

        if (nestedClassNames.Count > 0)
        {
            var nestedRewriter = new NestedClassRewriter(nestedClassNames, sourceClassName);
            transformedMethod = (MethodDeclarationSyntax)nestedRewriter.Visit(transformedMethod)!;
        }


        transformedMethod = AstTransformations.EnsureStaticModifier(transformedMethod);

        return EnsureMethodIsInternal(transformedMethod);
    }

    private static MethodDeclarationSyntax AddThisParameterToMethod(
        MethodDeclarationSyntax method,
        string sourceClassName,
        string parameterName)
    {
        var sourceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
            .WithType(SyntaxFactory.IdentifierName(sourceClassName));

        var parameters = method.ParameterList.Parameters.Insert(0, sourceParameter);
        var newParameterList = method.ParameterList.WithParameters(parameters);

        var updatedMethod = method.WithParameterList(newParameterList);
        updatedMethod = AstTransformations.ReplaceThisReferences(updatedMethod, parameterName);
        return updatedMethod;
    }

    private static MethodDeclarationSyntax RewriteMethodBody(
        MethodDeclarationSyntax method,
        string methodName,
        string thisParameterName,
        bool usesInstanceMembers,
        bool callsOtherMethods,
        bool isRecursive,
        HashSet<string> instanceMembers,
        HashSet<string> otherMethodNames)
    {
        var parameterName = thisParameterName;

        if (usesInstanceMembers)
        {
            var memberRewriter = new InstanceMemberRewriter(parameterName, instanceMembers);
            method = (MethodDeclarationSyntax)memberRewriter.Visit(method)!;
        }

        if (callsOtherMethods)
        {
            var methodCallRewriter = new MethodCallRewriter(otherMethodNames, parameterName);
            method = (MethodDeclarationSyntax)methodCallRewriter.Visit(method)!;
            var methodRefRewriter = new MethodReferenceRewriter(otherMethodNames, parameterName);
            method = (MethodDeclarationSyntax)methodRefRewriter.Visit(method)!;
        }

        if (isRecursive)
        {
            var recursiveCallRewriter = new MethodCallRewriter(new HashSet<string> { methodName }, parameterName);
            method = (MethodDeclarationSyntax)recursiveCallRewriter.Visit(method)!;
            var recursiveRefRewriter = new MethodReferenceRewriter(new HashSet<string> { methodName }, parameterName);
            method = (MethodDeclarationSyntax)recursiveRefRewriter.Visit(method)!;
        }

        return method;
    }

    private static MethodDeclarationSyntax EnsureMethodIsInternal(MethodDeclarationSyntax method)
    {
        if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.InternalKeyword)))
            return method;

        var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) &&
                                              !m.IsKind(SyntaxKind.ProtectedKeyword));

        if (method.Modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            // Keep the protected modifier for overrides to avoid reducing
            // accessibility but add internal for cross-class access
            mods = mods.Append(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword));
        }

        return method.WithModifiers(SyntaxFactory.TokenList(mods)
            .Add(SyntaxFactory.Token(SyntaxKind.InternalKeyword)));
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
        var methodArgs = method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)))
            .ToList();

        var invocationArgs = new List<ArgumentSyntax>();

        if (needsThisParameter)
        {
            invocationArgs.Add(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
        }

        foreach (var fieldName in fieldArguments)
        {
            invocationArgs.Add(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(fieldName)));
        }

        invocationArgs.AddRange(methodArgs);

        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(invocationArgs));

        ExpressionSyntax accessExpression = SyntaxFactory.IdentifierName(accessMemberName);

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

    private static MethodDeclarationSyntax CreateBaseWrapper(MethodDeclarationSyntax method)
    {
        var wrapperName = "Base" + method.Identifier.ValueText;
        var invocationArgs = method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)));
        var invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.BaseExpression(),
                    SyntaxFactory.IdentifierName(method.Identifier)))
            .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(invocationArgs)));

        StatementSyntax call = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword)
            ? SyntaxFactory.ExpressionStatement(invocation)
            : SyntaxFactory.ReturnStatement(invocation);

        var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.OverrideKeyword)
                                              && !m.IsKind(SyntaxKind.VirtualKeyword)
                                              && !m.IsKind(SyntaxKind.AbstractKeyword));

        return method.WithIdentifier(SyntaxFactory.Identifier(wrapperName))
            .WithModifiers(SyntaxFactory.TokenList(mods))
            .WithBody(SyntaxFactory.Block(call))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);
    }

    private static SyntaxNode UpdateSourceClassWithStub(
        ClassDeclarationSyntax sourceClass,
        MethodDeclarationSyntax originalMethod,
        MethodDeclarationSyntax stubMethod,
        MemberDeclarationSyntax? accessMember,
        Dictionary<string, MethodDeclarationSyntax>? dependencyUpdates = null,
        MethodDeclarationSyntax? baseWrapper = null)
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
            if (baseWrapper != null)
            {
                originMembers.Insert(methodIndex + 1, baseWrapper);
            }
        }
        else if (baseWrapper != null)
        {
            originMembers.Add(baseWrapper);
        }

        if (dependencyUpdates != null)
        {
            for (int i = 0; i < originMembers.Count; i++)
            {
                if (originMembers[i] is MethodDeclarationSyntax m &&
                    dependencyUpdates.TryGetValue(m.Identifier.ValueText, out var updated))
                {
                    originMembers[i] = updated;
                }
            }
        }

        var newOriginClass = sourceClass.WithMembers(SyntaxFactory.List(originMembers));
        return sourceClass.SyntaxTree.GetRoot().ReplaceNode(sourceClass, newOriginClass);
    }

    private static int FindAccessMemberInsertionIndex(List<MemberDeclarationSyntax> members)
    {
        var fieldIndex = members.FindLastIndex(m => m is FieldDeclarationSyntax or PropertyDeclarationSyntax);
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
            var adjustedMethod = method;
            if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                var mods = method.Modifiers
                    .Where(m => !m.IsKind(SyntaxKind.OverrideKeyword));
                adjustedMethod = method.WithModifiers(SyntaxFactory.TokenList(mods));
            }

            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(adjustedMethod.WithLeadingTrivia());
            var compilationUnit = (CompilationUnitSyntax)targetRoot;

            var nsDecl = compilationUnit.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (nsDecl != null)
            {
                var updatedNs = nsDecl.AddMembers(newClass);
                return compilationUnit.ReplaceNode(nsDecl, updatedNs);
            }
            else if (!string.IsNullOrEmpty(namespaceName))
            {
                var ns = SyntaxFactory.FileScopedNamespaceDeclaration(
                        SyntaxFactory.ParseName(namespaceName))
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
            if (targetClassDecl.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                !method.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                throw new McpException(
                    $"Error: Cannot move instance method '{method.Identifier.ValueText}' to static class '{targetClass}'");
            }

            var updatedClass = targetClassDecl.AddMembers(method.WithLeadingTrivia());
            return targetRoot.ReplaceNode(targetClassDecl, updatedClass);
        }
    }

    public static SyntaxNode PropagateUsings(
        SyntaxNode sourceRoot,
        SyntaxNode targetRoot,
        string? namespaceName = null)
    {
        var sourceCompilationUnit = sourceRoot as CompilationUnitSyntax ?? throw new InvalidOperationException("Expected compilation unit");
        var sourceUsings = sourceCompilationUnit.Usings.ToList();

        var targetCompilationUnit = targetRoot as CompilationUnitSyntax ?? throw new InvalidOperationException("Expected compilation unit");
        var targetUsingNames = targetCompilationUnit.Usings
            .Select(u => u.Name.ToString())
            .ToHashSet();
        var missingUsings = sourceUsings
            .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
            .Where(u => namespaceName == null || u.Name.ToString() != namespaceName)
            .ToArray();

        if (missingUsings.Length > 0)
        {
            return targetCompilationUnit.AddUsings(missingUsings);
        }

        return targetRoot;
    }
}
