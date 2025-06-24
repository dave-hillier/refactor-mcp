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
using System.IO;
using RefactorMCP.ConsoleApp.SyntaxWalkers;

namespace RefactorMCP.ConsoleApp.Move;

public static partial class MoveMethodAst
{
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
        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);

        var formatted = Formatter.Format(finalRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveInstanceMethodInSource(string sourceText, string sourceClass, string methodName, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var moveResult = MoveInstanceMethodAst(
            root,
            sourceClass,
            methodName,
            targetClass,
            accessMemberName,
            accessMemberType,
            Array.Empty<string>());
        var finalRoot = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);

        var formatted = Formatter.Format(finalRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    public static string MoveMultipleInstanceMethodsInSource(string sourceText, string sourceClass, string[] methodNames, string targetClass, string accessMemberName, string accessMemberType)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        foreach (var methodName in methodNames)
        {
            var moveResult = MoveInstanceMethodAst(
                root,
                sourceClass,
                methodName,
                targetClass,
                accessMemberName,
                accessMemberType,
                Array.Empty<string>());
            root = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);
        }

        var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static string? GetSimpleTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            GenericNameSyntax g => g.Identifier.ValueText,
            _ => null
        };
    }

    private static HashSet<string> GetInstanceMemberNames(ClassDeclarationSyntax originClass)
    {
        var root = originClass.SyntaxTree.GetRoot();

        var classCollector = new ClassCollectorWalker();
        classCollector.Visit(root);

        var interfaceCollector = new InterfaceCollectorWalker();
        interfaceCollector.Visit(root);

        var queue = new Queue<MemberDeclarationSyntax>();
        var visited = new HashSet<string>();

        queue.Enqueue(originClass);
        visited.Add(originClass.Identifier.ValueText);

        var walker = new InstanceMemberNameWalker();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            walker.Visit(current);

            if (current is ClassDeclarationSyntax cls && cls.BaseList != null)
            {
                foreach (var bt in cls.BaseList.Types)
                {
                    var name = GetSimpleTypeName(bt.Type);
                    if (name == null || !visited.Add(name))
                        continue;

                    if (classCollector.Classes.TryGetValue(name, out var bClass))
                        queue.Enqueue(bClass);
                    if (interfaceCollector.Interfaces.TryGetValue(name, out var iface))
                        queue.Enqueue(iface);
                }
            }
            else if (current is InterfaceDeclarationSyntax iface && iface.BaseList != null)
            {
                foreach (var bt in iface.BaseList.Types)
                {
                    var name = GetSimpleTypeName(bt.Type);
                    if (name == null || !visited.Add(name))
                        continue;

                    if (interfaceCollector.Interfaces.TryGetValue(name, out var nestedIface))
                        queue.Enqueue(nestedIface);
                }
            }
        }

        return walker.Names;
    }

    // New: Get method names in the class
    private static HashSet<string> GetMethodNames(ClassDeclarationSyntax originClass)
    {
        var root = originClass.SyntaxTree.GetRoot();

        var classCollector = new ClassCollectorWalker();
        classCollector.Visit(root);

        var interfaceCollector = new InterfaceCollectorWalker();
        interfaceCollector.Visit(root);

        var queue = new Queue<MemberDeclarationSyntax>();
        var visited = new HashSet<string>();

        queue.Enqueue(originClass);
        visited.Add(originClass.Identifier.ValueText);

        var walker = new MethodNameWalker();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            walker.Visit(current);

            if (current is ClassDeclarationSyntax cls && cls.BaseList != null)
            {
                foreach (var bt in cls.BaseList.Types)
                {
                    var name = GetSimpleTypeName(bt.Type);
                    if (name == null || !visited.Add(name))
                        continue;

                    if (classCollector.Classes.TryGetValue(name, out var bClass))
                        queue.Enqueue(bClass);
                    if (interfaceCollector.Interfaces.TryGetValue(name, out var iface))
                        queue.Enqueue(iface);
                }
            }
            else if (current is InterfaceDeclarationSyntax iface && iface.BaseList != null)
            {
                foreach (var bt in iface.BaseList.Types)
                {
                    var name = GetSimpleTypeName(bt.Type);
                    if (name == null || !visited.Add(name))
                        continue;

                    if (interfaceCollector.Interfaces.TryGetValue(name, out var nestedIface))
                        queue.Enqueue(nestedIface);
                }
            }
        }

        return walker.Names;
    }

    // New: Get static field names in the class
    private static HashSet<string> GetStaticFieldNames(ClassDeclarationSyntax originClass)
    {
        var walker = new StaticFieldNameWalker();
        walker.Visit(originClass);
        return walker.Names;
    }

    private static HashSet<string> GetNestedClassNames(ClassDeclarationSyntax originClass)
    {
        var walker = new NestedClassNameWalker(originClass);
        walker.Visit(originClass);
        return walker.Names;
    }

    private static Dictionary<string, TypeSyntax> GetPrivateFieldInfos(ClassDeclarationSyntax originClass)
    {
        var walker = new PrivateFieldInfoWalker();
        walker.Visit(originClass);
        return walker.Infos;
    }

    private static HashSet<string> GetUsedPrivateFields(MethodDeclarationSyntax method, HashSet<string> privateFieldNames)
    {
        var walker = new PrivateFieldUsageWalker(privateFieldNames);
        walker.Visit(method);
        return walker.UsedFields;
    }

    private static HashSet<string> GetImplicitInstanceMembers(MethodDeclarationSyntax method)
    {
        var walker = new ImplicitInstanceMemberWalker();
        walker.Visit(method);
        return walker.Members;
    }

    private static bool MemberExists(ClassDeclarationSyntax classDecl, string memberName)
    {
        var walker = new InstanceMemberNameWalker();
        walker.Visit(classDecl);
        return walker.Names.Contains(memberName);
    }

    internal static string GenerateAccessMemberName(IEnumerable<string> existingNames, string targetClass)
    {
        var baseName = "_" + char.ToLower(targetClass[0]) + targetClass.Substring(1);
        var name = baseName;
        var counter = 1;
        var nameSet = new HashSet<string>(existingNames);
        while (nameSet.Contains(name))
        {
            name = baseName + counter;
            counter++;
        }
        return name;
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


}
