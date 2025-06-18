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

public static partial class MoveMethodsTool
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

        var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
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
            var moveResult = MoveInstanceMethodAst(root, sourceClass, methodName, targetClass, accessMemberName, accessMemberType);
            root = AddMethodToTargetClass(moveResult.NewSourceRoot, targetClass, moveResult.MovedMethod, moveResult.Namespace);
        }

        var formatted = Formatter.Format(root, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

    private static HashSet<string> GetInstanceMemberNames(ClassDeclarationSyntax originClass)
    {
        var knownMembers = new HashSet<string>();
        var root = originClass.SyntaxTree.GetRoot();

        while (originClass != null)
        {
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

            var baseIdentifier = originClass.BaseList?.Types
                .Select(t => t.Type)
                .OfType<IdentifierNameSyntax>()
                .FirstOrDefault();

            originClass = baseIdentifier != null
                ? root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == baseIdentifier.Identifier.ValueText)
                : null;
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

    private static HashSet<string> GetNestedClassNames(ClassDeclarationSyntax originClass)
    {
        return originClass.Members
            .OfType<ClassDeclarationSyntax>()
            .Select(c => c.Identifier.ValueText)
            .ToHashSet();
    }

    private static Dictionary<string, TypeSyntax> GetPrivateFieldInfos(ClassDeclarationSyntax originClass)
    {
        var infos = new Dictionary<string, TypeSyntax>();
        foreach (var member in originClass.Members.OfType<FieldDeclarationSyntax>())
        {
            if (member.Modifiers.Any(SyntaxKind.PrivateKeyword))
            {
                foreach (var variable in member.Declaration.Variables)
                {
                    infos[variable.Identifier.ValueText] = member.Declaration.Type;
                }
            }
        }
        return infos;
    }

    private static HashSet<string> GetUsedPrivateFields(MethodDeclarationSyntax method, HashSet<string> privateFieldNames)
    {
        var walker = new PrivateFieldUsageWalker(privateFieldNames);
        walker.Visit(method);
        return walker.UsedFields;
    }

    private static bool MemberExists(ClassDeclarationSyntax classDecl, string memberName)
    {
        return classDecl.Members.OfType<FieldDeclarationSyntax>()
                   .Any(f => f.Declaration.Variables.Any(v => v.Identifier.ValueText == memberName))
               || classDecl.Members.OfType<PropertyDeclarationSyntax>()
                   .Any(p => p.Identifier.ValueText == memberName);
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
