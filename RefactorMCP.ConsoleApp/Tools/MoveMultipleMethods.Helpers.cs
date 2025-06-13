using ModelContextProtocol.Server;
using ModelContextProtocol;
using System;
using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public static partial class MoveMultipleMethodsTool
{
    // ===== HELPER METHODS =====

    private static Dictionary<string, HashSet<string>> BuildDependencies(
        SyntaxNode sourceRoot, 
        string[] sourceClasses,
        string[] methodNames)
    {
        // Build map keyed by "Class.Method" to support duplicate method names in different classes
        var opSet = sourceClasses.Zip(methodNames, (c, m) => $"{c}.{m}").ToHashSet();
        var map = sourceRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SelectMany(cls => cls.Members.OfType<MethodDeclarationSyntax>()
                .Select(m => new { Key = $"{cls.Identifier.ValueText}.{m.Identifier.ValueText}", Method = m }))
            .Where(x => opSet.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Method);

        var deps = new Dictionary<string, HashSet<string>>();
        for (int i = 0; i < sourceClasses.Length; i++)
        {
            var key = $"{sourceClasses[i]}.{methodNames[i]}";
            if (!map.TryGetValue(key, out var method))
            {
                deps[key] = new HashSet<string>();
                continue;
            }

            var called = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(inv => inv.Expression switch
                {
                    IdentifierNameSyntax id => id.Identifier.ValueText,
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
                    _ => null
                })
                .Where(n => n != null)
                .Select(name => $"{sourceClasses[i]}.{name}")
                .Where(n => map.ContainsKey(n))
                .ToHashSet();

            deps[key] = called;
        }

        return deps;
    }

    private static List<int> OrderOperations(
        SyntaxNode sourceRoot, 
        string[] sourceClasses,
        string[] methodNames,
        string[] targetClasses,
        string[] accessMembers,
        string[] accessMemberTypes,
        bool[] isStatic)
    {
        var deps = BuildDependencies(sourceRoot, sourceClasses, methodNames);
        var indices = Enumerable.Range(0, sourceClasses.Length).ToList();
        return indices.OrderBy(i => deps.TryGetValue($"{sourceClasses[i]}.{methodNames[i]}", out var d) ? d.Count : 0).ToList();
    }
}
