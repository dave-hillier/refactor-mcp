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

    public static string MoveMultipleMethodsInSource(
        string sourceText, 
        string[] sourceClasses,
        string[] methodNames,
        string[] targetClasses,
        string[] accessMembers,
        string[] accessMemberTypes,
        bool[] isStatic)
    {
        if (sourceClasses.Length == 0 || methodNames.Length == 0 || targetClasses.Length == 0 || 
            accessMembers.Length == 0 || accessMemberTypes.Length == 0 || isStatic.Length == 0)
            return RefactoringHelpers.ThrowMcpException("Error: No operations provided");

        if (sourceClasses.Length != methodNames.Length || methodNames.Length != targetClasses.Length || 
            targetClasses.Length != accessMembers.Length || accessMembers.Length != accessMemberTypes.Length || 
            accessMemberTypes.Length != isStatic.Length)
            return RefactoringHelpers.ThrowMcpException("Error: All arrays must have the same length");

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var resultRoot = MoveMultipleMethodsAst(root, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);
        var formatted = Formatter.Format(resultRoot, RefactoringHelpers.SharedWorkspace);
        return formatted.ToFullString();
    }

}
