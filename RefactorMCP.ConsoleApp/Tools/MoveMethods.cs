using ModelContextProtocol.Server;
using ModelContextProtocol;
using System.ComponentModel;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

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

[McpServerToolType]
public static class MoveMethodsTool
{
    private static bool HasInstanceMemberUsage(MethodDeclarationSyntax method, HashSet<string> knownMembers)
    {
        var usageChecker = new InstanceMemberUsageChecker(knownMembers);
        usageChecker.Visit(method);
        return usageChecker.HasInstanceMemberUsage;
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

    public static string MoveStaticMethodInSource(string sourceText, string methodName, string targetClass)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName &&
                                 m.Modifiers.Any(SyntaxKind.StaticKeyword));
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Static method '{methodName}' not found");

        // Prepare stub in the original class that delegates to the moved method
        var argumentList = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(
                method.ParameterList.Parameters
                    .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier)))));

        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(targetClass),
                SyntaxFactory.IdentifierName(methodName)),
            argumentList);

        bool isVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        StatementSyntax callStatement = isVoid
            ? SyntaxFactory.ExpressionStatement(invocation)
            : SyntaxFactory.ReturnStatement(invocation);

        var stubMethod = method.WithBody(SyntaxFactory.Block(callStatement))
            .WithExpressionBody(null)
            .WithSemicolonToken(default);

        var rootWithStub = root.ReplaceNode(method, stubMethod);

        var targetClassDecl = rootWithStub.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        SyntaxNode newRoot;
        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(method.WithLeadingTrivia());
            newRoot = ((CompilationUnitSyntax)rootWithStub).AddMembers(newClass);
        }
        else
        {
            var updatedClass = targetClassDecl.AddMembers(method.WithLeadingTrivia());
            newRoot = rootWithStub.ReplaceNode(targetClassDecl, updatedClass);
        }

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveInstanceMethodInSource(string sourceText, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var originClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Source class '{sourceClass}' not found");

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");

        // Check if method uses instance members
        var knownMembers = GetInstanceMemberNames(originClass);

        // Check if method uses any instance members
        bool usesInstanceMembers = HasInstanceMemberUsage(method, knownMembers);

        // Build call to the moved method for the stub
        var originalParameters = method.ParameterList.Parameters
            .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))).ToList();

        // Only add 'this' as the first argument if the method uses instance members
        if (usesInstanceMembers)
        {
            originalParameters.Insert(0, SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
        }

        var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(originalParameters));

        var accessExpression = SyntaxFactory.IdentifierName(accessMemberName);
        var invocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                accessExpression,
                SyntaxFactory.IdentifierName(methodName)),
            argumentList);

        bool isVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
        StatementSyntax callStatement = isVoid
            ? SyntaxFactory.ExpressionStatement(invocation)
            : SyntaxFactory.ReturnStatement(invocation);

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

        // Only add source class parameter if method uses instance members
        if (usesInstanceMembers)
        {
            var sourceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(sourceClass.ToLower()))
                .WithType(SyntaxFactory.IdentifierName(sourceClass));

            var newParameters = new[] { sourceParameter }.Concat(method.ParameterList.Parameters);
            var newParameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newParameters));
            movedMethod = movedMethod.WithParameterList(newParameterList);

            // Replace references to instance members with parameter access
            var memberRewriter = new InstanceMemberRewriter(sourceClass.ToLower(), knownMembers);
            movedMethod = (MethodDeclarationSyntax)memberRewriter.Visit(movedMethod)!;
        }

        // Ensure moved method is public in the target class
        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword));
            movedMethod = movedMethod.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        var rootWithStub = root.ReplaceNode(originClass, newOriginClass);

        var targetClassDecl = rootWithStub.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        SyntaxNode newRoot;
        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(movedMethod.WithLeadingTrivia());
            newRoot = ((CompilationUnitSyntax)rootWithStub).AddMembers(newClass);
        }
        else
        {
            var replaced = targetClassDecl.AddMembers(movedMethod.WithLeadingTrivia());
            newRoot = rootWithStub.ReplaceNode(targetClassDecl, replaced);
        }

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveMultipleInstanceMethodsInSource(string sourceText, string sourceClass, string[] methodNames, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var originClass = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            return RefactoringHelpers.ThrowMcpException($"Error: Source class '{sourceClass}' not found");

        // Find all methods to move
        var methodsToMove = new List<MethodDeclarationSyntax>();
        foreach (var methodName in methodNames)
        {
            var method = originClass.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);
            if (method == null)
                return RefactoringHelpers.ThrowMcpException($"Error: No method named '{methodName}' found");
            methodsToMove.Add(method);
        }

        // Get instance members for rewriting
        var knownMembers = GetInstanceMemberNames(originClass);

        // Create access member once if it doesn't already exist
        MemberDeclarationSyntax? accessMember = null;
        bool accessMemberExists = MemberExists(originClass, accessMemberName);
        if (!accessMemberExists)
        {
            accessMember = CreateAccessMember(accessMemberType, accessMemberName, targetClass);
        }

        // Process all methods and create stubs
        var originMembers = originClass.Members.ToList();
        var movedMethods = new List<MethodDeclarationSyntax>();

        foreach (var method in methodsToMove)
        {
            // Check if method uses instance members
            bool usesInstanceMembers = HasInstanceMemberUsage(method, knownMembers);

            // Build call to the moved method for the stub
            var originalParameters = method.ParameterList.Parameters
                .Select(p => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(p.Identifier))).ToList();

            // Only add 'this' as the first argument if the method uses instance members
            if (usesInstanceMembers)
            {
                originalParameters.Insert(0, SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
            }

            var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(originalParameters));

            var accessExpression = SyntaxFactory.IdentifierName(accessMemberName);
            var invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    accessExpression,
                    SyntaxFactory.IdentifierName(method.Identifier.ValueText)),
                argumentList);

            bool isVoid = method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword);
            StatementSyntax callStatement = isVoid
                ? SyntaxFactory.ExpressionStatement(invocation)
                : SyntaxFactory.ReturnStatement(invocation);

            var stubMethod = method.WithBody(SyntaxFactory.Block(callStatement))
                .WithExpressionBody(null)
                .WithSemicolonToken(default);

            // Replace the original method with the stub
            var methodIndex = originMembers.FindIndex(m => m == method);
            if (methodIndex >= 0)
            {
                originMembers[methodIndex] = stubMethod;
            }

            // Prepare the moved method
            var movedMethod = method;

            // Only add source class parameter if method uses instance members
            if (usesInstanceMembers)
            {
                var sourceParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(sourceClass.ToLower()))
                    .WithType(SyntaxFactory.IdentifierName(sourceClass));

                var newParameters = new[] { sourceParameter }.Concat(method.ParameterList.Parameters);
                var newParameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(newParameters));
                movedMethod = movedMethod.WithParameterList(newParameterList);

                // Replace references to instance members with parameter access
                var memberRewriter = new InstanceMemberRewriter(sourceClass.ToLower(), knownMembers);
                movedMethod = (MethodDeclarationSyntax)memberRewriter.Visit(movedMethod)!;
            }

            // Ensure moved method is public in the target class
            if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            {
                var mods = method.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword));
                movedMethod = movedMethod.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
            }

            movedMethods.Add(movedMethod);
        }

        // Insert the access member at the correct position if needed
        var fieldIndex = originMembers.FindLastIndex(m => m is FieldDeclarationSyntax || m is PropertyDeclarationSyntax);
        var insertIndex = fieldIndex >= 0 ? fieldIndex + 1 : 0;
        if (accessMember != null)
        {
            originMembers.Insert(insertIndex, accessMember);
        }

        var newOriginClass = originClass.WithMembers(SyntaxFactory.List(originMembers));
        var rootWithStub = root.ReplaceNode(originClass, newOriginClass);

        // Add all moved methods to target class
        var targetClassDecl = rootWithStub.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);

        SyntaxNode newRoot;
        if (targetClassDecl == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(movedMethods.Select(m => m.WithLeadingTrivia()).ToArray());
            newRoot = ((CompilationUnitSyntax)rootWithStub).AddMembers(newClass);
        }
        else
        {
            var replaced = targetClassDecl.AddMembers(movedMethods.Select(m => m.WithLeadingTrivia()).ToArray());
            newRoot = rootWithStub.ReplaceNode(targetClassDecl, replaced);
        }

        var formatted = Formatter.Format(newRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static async Task<(SyntaxNode sourceRoot, SyntaxNode targetRoot, string successMessage)> MoveInstanceMethodCore(
        SyntaxNode sourceSyntaxRoot,
        string sourceClass,
        string methodName,
        string targetClass,
        string accessMemberName,
        string accessMemberType,
        SyntaxNode? targetSyntaxRoot = null,
        string? targetPath = null)
    {
        var originClass = sourceSyntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (originClass == null)
            throw new McpException($"Error: Source class '{sourceClass}' not found");

        var method = originClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (method == null)
            throw new McpException($"Error: No method named '{methodName}' found");

        var targetClassDecl = sourceSyntaxRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        SyntaxNode workingSourceRoot = sourceSyntaxRoot;

        // Create target class if it doesn't exist in source
        if (targetClassDecl == null)
        {
            targetClassDecl = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            if (originClass.Parent is NamespaceDeclarationSyntax ns)
            {
                var updatedNs = ns.AddMembers(targetClassDecl);
                workingSourceRoot = workingSourceRoot.ReplaceNode(ns, updatedNs);
            }
            else
            {
                workingSourceRoot = ((CompilationUnitSyntax)workingSourceRoot).AddMembers(targetClassDecl);
            }
        }

        // Get the updated reference to the origin class after any modifications to workingSourceRoot
        var currentOriginClass = workingSourceRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == sourceClass);
        if (currentOriginClass == null)
            throw new McpException($"Error: Could not find updated reference to source class '{sourceClass}'");

        // Find the method in the current origin class reference
        var currentMethod = currentOriginClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
        if (currentMethod == null)
            throw new McpException($"Error: No method named '{methodName}' found in updated class reference");

        // Remove method from source class
        ClassDeclarationSyntax newOriginClass = currentOriginClass.RemoveNode(currentMethod, SyntaxRemoveOptions.KeepNoTrivia);

        // Add access member to source class if it doesn't already exist
        if (!MemberExists(newOriginClass, accessMemberName))
        {
            if (accessMemberType == "field")
            {
                var field = SyntaxFactory.FieldDeclaration(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.ParseTypeName(targetClass),
                            SyntaxFactory.SeparatedList(new[] { SyntaxFactory.VariableDeclarator(accessMemberName)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(targetClass))
                                        .WithArgumentList(SyntaxFactory.ArgumentList()))) }))
                    ).AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

                newOriginClass = newOriginClass.AddMembers(field);
            }
            else if (accessMemberType == "property")
            {
                var prop = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(targetClass), accessMemberName)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                    .AddAccessorListAccessors(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
                newOriginClass = newOriginClass.AddMembers(prop);
            }
        }

        // Prepare the method for moving (make it public)
        var updatedMethod = currentMethod;
        if (!currentMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            var mods = currentMethod.Modifiers.Where(m => !m.IsKind(SyntaxKind.PrivateKeyword) && !m.IsKind(SyntaxKind.ProtectedKeyword) && !m.IsKind(SyntaxKind.InternalKeyword));
            updatedMethod = currentMethod.WithModifiers(SyntaxFactory.TokenList(mods).Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword)));
        }

        // Update source root with modified origin class
        var finalSourceRoot = workingSourceRoot.ReplaceNode(currentOriginClass, newOriginClass);

        // Handle target root
        SyntaxNode finalTargetRoot;
        if (targetSyntaxRoot != null)
        {
            // Cross-file scenario - work with separate target root
            finalTargetRoot = targetSyntaxRoot;

            // Handle using statements propagation
            var sourceCompilationUnit = (CompilationUnitSyntax)sourceSyntaxRoot;
            var sourceUsings = sourceCompilationUnit.Usings.ToList();

            var targetCompilationUnit = (CompilationUnitSyntax)finalTargetRoot;
            var targetUsingNames = targetCompilationUnit.Usings
                .Select(u => u.Name.ToString())
                .ToHashSet();
            var missingUsings = sourceUsings
                .Where(u => !targetUsingNames.Contains(u.Name.ToString()))
                .ToArray();
            if (missingUsings.Length > 0)
            {
                targetCompilationUnit = targetCompilationUnit.AddUsings(missingUsings);
                finalTargetRoot = targetCompilationUnit;
            }
        }
        else
        {
            // Same-file scenario
            finalTargetRoot = finalSourceRoot;
        }

        // Add method to target class
        var newTargetClass = finalTargetRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == targetClass);
        if (newTargetClass == null)
        {
            var newClass = SyntaxFactory.ClassDeclaration(targetClass)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(updatedMethod.WithLeadingTrivia());
            finalTargetRoot = ((CompilationUnitSyntax)finalTargetRoot).AddMembers(newClass);
        }
        else
        {
            var replaced = newTargetClass.AddMembers(updatedMethod.WithLeadingTrivia());
            finalTargetRoot = finalTargetRoot.ReplaceNode(newTargetClass, replaced);
        }

        var successMessage = $"Successfully moved {sourceClass}.{methodName} instance method to {targetClass}" +
            (targetPath != null ? $" in {targetPath}" : "");

        return (finalSourceRoot, finalTargetRoot, successMessage);
    }

    private static async Task<string> MoveInstanceMethodWithSolution(Document document, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync();

        var (finalSourceRoot, finalTargetRoot, successMessage) = await MoveInstanceMethodCore(
            syntaxRoot!,
            sourceClass,
            methodName,
            targetClass,
            accessMemberName,
            accessMemberType);

        // Format and write using Document API
        var formatted = Formatter.Format(finalTargetRoot, document.Project.Solution.Workspace);
        var newDocument = document.WithSyntaxRoot(formatted);
        var newText = await newDocument.GetTextAsync();
        await File.WriteAllTextAsync(document.FilePath!, newText.ToString());

        return successMessage + $" in {document.FilePath}";
    }

    private static async Task<string> MoveInstanceMethodSingleFile(string filePath, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var sourceText = await File.ReadAllTextAsync(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText);
        var sourceSyntaxRoot = await syntaxTree.GetRootAsync();

        SyntaxNode? targetSyntaxRoot = null;
        if (!sameFile)
        {
            if (File.Exists(targetPath))
            {
                var targetText = await File.ReadAllTextAsync(targetPath);
                targetSyntaxRoot = await CSharpSyntaxTree.ParseText(targetText).GetRootAsync();
            }
            else
            {
                targetSyntaxRoot = SyntaxFactory.CompilationUnit();
            }
        }

        var (finalSourceRoot, finalTargetRoot, successMessage) = await MoveInstanceMethodCore(
            sourceSyntaxRoot,
            sourceClass,
            methodName,
            targetClass,
            accessMemberName,
            accessMemberType,
            targetSyntaxRoot,
            targetPath);

        // Handle file I/O
        if (sameFile)
        {
            var formatted = Formatter.Format(finalTargetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formatted.ToFullString());
        }
        else
        {
            var formattedSource = Formatter.Format(finalSourceRoot, RefactoringHelpers.SharedWorkspace);
            await File.WriteAllTextAsync(filePath, formattedSource.ToFullString());

            var formattedTarget = Formatter.Format(finalTargetRoot, RefactoringHelpers.SharedWorkspace);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, formattedTarget.ToFullString());
        }

        return successMessage;
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
                return await MoveInstanceMethodSingleFile(filePath, sourceClass, methodList[0], targetClass, accessMemberName, accessMemberType, targetFilePath);
            }
            else
            {
                return await MoveBulkInstanceMethodsSingleFile(filePath, sourceClass, methodList, targetClass, accessMemberName, accessMemberType, targetFilePath);
            }
        }
    }

    private static async Task<string> MoveBulkInstanceMethodsSingleFile(string filePath, string sourceClass, string[] methodNames, string targetClass, string accessMemberName, string accessMemberType, string? targetFilePath)
    {
        if (!File.Exists(filePath))
            return RefactoringHelpers.ThrowMcpException($"Error: File {filePath} not found (current dir: {Directory.GetCurrentDirectory()})");

        var targetPath = targetFilePath ?? filePath;
        var sameFile = targetPath == filePath;

        var sourceText = await File.ReadAllTextAsync(filePath);

        if (sameFile)
        {
            // Same file operation
            var result = MoveMultipleInstanceMethodsInSource(sourceText, sourceClass, methodNames, targetClass, accessMemberName, accessMemberType);
            await File.WriteAllTextAsync(filePath, result);
            return $"Successfully moved {methodNames.Length} methods from {sourceClass} to {targetClass} in {filePath}";
        }
        else
        {
            // Cross-file operation - for now, fall back to individual moves
            // TODO: Implement efficient cross-file bulk move
            var results = new List<string>();
            foreach (var methodName in methodNames)
            {
                results.Add(await MoveInstanceMethodSingleFile(filePath, sourceClass, methodName, targetClass, accessMemberName, accessMemberType, targetFilePath));
            }
            return string.Join("\n", results);
        }
    }
}
