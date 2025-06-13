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
    // ===== LEGACY STRING-BASED METHODS (for backward compatibility) =====

    public static string MoveMultipleMethodsInSource(string sourceText, IEnumerable<MoveOperation> operations)
    {
        var ops = operations.ToList();
        if (ops.Count == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var resultRoot = MoveMultipleMethodsAst(root, ops);
        var formatted = Formatter.Format(resultRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

}
