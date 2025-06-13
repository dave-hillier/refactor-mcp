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

    private static Dictionary<string, HashSet<string>> BuildDependencies(SyntaxNode sourceRoot, IEnumerable<MoveOperation> ops)
    {
        // Build map keyed by "Class.Method" to support duplicate method names in different classes
        var opSet = ops.Select(o => ($"{o.SourceClass}.{o.Method}")).ToHashSet();
        var map = sourceRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SelectMany(cls => cls.Members.OfType<MethodDeclarationSyntax>()
                .Select(m => new { Key = $"{cls.Identifier.ValueText}.{m.Identifier.ValueText}", Method = m }))
            .Where(x => opSet.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Method);

        var deps = new Dictionary<string, HashSet<string>>();
        foreach (var op in ops)
        {
            var key = $"{op.SourceClass}.{op.Method}";
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
                .Select(name => $"{op.SourceClass}.{name}")
                .Where(n => map.ContainsKey(n))
                .ToHashSet();

            deps[key] = called;
        }

        return deps;
    }

    private static List<MoveOperation> OrderOperations(SyntaxNode sourceRoot, List<MoveOperation> ops)
    {
        var deps = BuildDependencies(sourceRoot, ops);
        return ops.OrderBy(o => deps.TryGetValue($"{o.SourceClass}.{o.Method}", out var d) ? d.Count : 0).ToList();
    }
}
