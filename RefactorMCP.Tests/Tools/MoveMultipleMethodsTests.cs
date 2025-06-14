using Xunit;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

public class MoveMultipleMethodsTests
{
    private static string MoveMultipleMethodsInSource(
        string source,
        string[] sourceClasses,
        string[] methodNames,
        string[] targetClasses,
        string[] accessMembers,
        string[] accessMemberTypes,
        bool[] isStatic)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var orderedIndices = MoveMultipleMethodsTool.OrderOperations(root, sourceClasses, methodNames);

        foreach (var i in orderedIndices)
        {
            if (isStatic[i])
            {
                var moveResult = MoveMethodsTool.MoveStaticMethodAst(root, methodNames[i], targetClasses[i]);
                root = MoveMethodsTool.AddMethodToTargetClass(moveResult.NewSourceRoot, targetClasses[i], moveResult.MovedMethod);
            }
            else
            {
                var moveResult = MoveMethodsTool.MoveInstanceMethodAst(root, sourceClasses[i], methodNames[i], targetClasses[i], accessMembers[i], accessMemberTypes[i]);
                root = MoveMethodsTool.AddMethodToTargetClass(moveResult.NewSourceRoot, targetClasses[i], moveResult.MovedMethod);
            }
        }

        var workspace = new AdhocWorkspace();
        var formattedRoot = Formatter.Format(root, workspace);
        return formattedRoot.ToFullString();
    }

    [Fact]
    public void MoveMultipleMethods_WithStaticMethods_ShouldMoveCorrectly()
    {
        var source = @"
using System;

public class SourceClass
{
    public static int Method1() { return 1; }
    public static int Method2() { return 2; }
}

public class TargetClass
{
}";

        var sourceClasses = new[] { "SourceClass", "SourceClass" };
        var methodNames = new[] { "Method1", "Method2" };
        var targetClasses = new[] { "TargetClass", "TargetClass" };
        var accessMembers = new[] { "", "" };
        var accessMemberTypes = new[] { "", "" };
        var isStatic = new[] { true, true };

        var result = MoveMultipleMethodsInSource(
            source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

        var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
        var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

        Assert.Contains("public static int Method1()", targetClassCode);
        Assert.Contains("public static int Method2()", targetClassCode);
        Assert.DoesNotContain("public static int Method1() { return 1; }", sourceClassCode);
        Assert.DoesNotContain("public static int Method2() { return 2; }", sourceClassCode);
        Assert.Contains("return TargetClass.Method1()", sourceClassCode);
        Assert.Contains("return TargetClass.Method2()", sourceClassCode);
    }

    [Fact]
    public void MoveMultipleMethods_WithInstanceMethods_ShouldMoveCorrectly()
    {
        var source = @"
using System;

public class SourceClass
{
    private int field1 = 1;
    public int Method1() { return field1; }
    public int Method2() { return field1 + 1; }
}

public class TargetClass
{
    private int field1 = 1;
}";

        var sourceClasses = new[] { "SourceClass", "SourceClass" };
        var methodNames = new[] { "Method1", "Method2" };
        var targetClasses = new[] { "TargetClass", "TargetClass" };
        var accessMembers = new[] { "field1", "field1" };
        var accessMemberTypes = new[] { "field", "field" };
        var isStatic = new[] { false, false };

        var result = MoveMultipleMethodsInSource(
            source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

        var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
        var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

        Assert.Contains("public int Method1(SourceClass @this)", targetClassCode);
        Assert.Contains("public int Method2(SourceClass @this)", targetClassCode);
        Assert.DoesNotContain("public int Method1() { return field1; }", sourceClassCode);
        Assert.DoesNotContain("public int Method2() { return field1 + 1; }", sourceClassCode);
        Assert.Contains("return this.field1.Method1(this)", sourceClassCode);
        Assert.Contains("return this.field1.Method2(this)", sourceClassCode);
    }

    [Fact]
    public void MoveMultipleMethods_WithMixedMethods_ShouldMoveCorrectly()
    {
        var source = @"
using System;

public class SourceClass
{
    private int field1 = 1;
    public static int Method1() { return 1; }
    public int Method2() { return field1; }
}

public class TargetClass
{
    private int field1 = 1;
}";

        var sourceClasses = new[] { "SourceClass", "SourceClass" };
        var methodNames = new[] { "Method1", "Method2" };
        var targetClasses = new[] { "TargetClass", "TargetClass" };
        var accessMembers = new[] { "", "field1" };
        var accessMemberTypes = new[] { "", "field" };
        var isStatic = new[] { true, false };

        var result = MoveMultipleMethodsInSource(
            source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

        var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
        var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

        Assert.Contains("public static int Method1()", targetClassCode);
        Assert.Contains("public int Method2(SourceClass @this)", targetClassCode);
        Assert.DoesNotContain("public static int Method1() { return 1; }", sourceClassCode);
        Assert.DoesNotContain("public int Method2() { return field1; }", sourceClassCode);
        Assert.Contains("return TargetClass.Method1()", sourceClassCode);
        Assert.Contains("return this.field1.Method2(this)", sourceClassCode);
    }
}
