using Xunit;
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;

namespace RefactorMCP.Tests.Roslyn
{
    public partial class MoveMethodsTests
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
                    root = MoveMethodsTool.AddMethodToTargetClass(moveResult.NewSourceRoot, targetClasses[i], moveResult.MovedMethod, moveResult.Namespace);
                }
                else
                {
                    var moveResult = MoveMethodsTool.MoveInstanceMethodAst(root, sourceClasses[i], methodNames[i], targetClasses[i], accessMembers[i], accessMemberTypes[i]);
                    root = MoveMethodsTool.AddMethodToTargetClass(moveResult.NewSourceRoot, targetClasses[i], moveResult.MovedMethod, moveResult.Namespace);
                }
            }

            var workspace = new AdhocWorkspace();
            var formattedRoot = Formatter.Format(root, workspace);
            return formattedRoot.ToFullString();
        }

        [Fact]
        public void MoveMultipleMethods_WithDependencies_ShouldMoveInCorrectOrder()
        {
            var source = @"
using System;

public class SourceClass
{
    public static int Method1() { return 1; }
    public static int Method2() { return Method1() + 1; }
    public static int Method3() { return Method2() + 1; }
}

public class TargetClass
{
}";

            var sourceClasses = new[] { "SourceClass", "SourceClass", "SourceClass" };
            var methodNames = new[] { "Method1", "Method2", "Method3" };
            var targetClasses = new[] { "TargetClass", "TargetClass", "TargetClass" };
            var accessMembers = new[] { "", "", "" };
            var accessMemberTypes = new[] { "", "", "" };
            var isStatic = new[] { true, true, true };

            var result = MoveMultipleMethodsInSource(
                source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

            var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
            var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

            Assert.Contains("public static int Method1()", targetClassCode);
            Assert.Contains("public static int Method2()", targetClassCode);
            Assert.Contains("public static int Method3()", targetClassCode);
            Assert.Contains("return Method1() + 1", targetClassCode);
            Assert.Contains("return Method2() + 1", targetClassCode);

            Assert.DoesNotContain("public static int Method1() { return 1; }", sourceClassCode);
            Assert.DoesNotContain("public static int Method2() { return Method1() + 1; }", sourceClassCode);
            Assert.DoesNotContain("public static int Method3() { return Method2() + 1; }", sourceClassCode);

            Assert.Contains("return TargetClass.Method1()", sourceClassCode);
            Assert.Contains("return TargetClass.Method2()", sourceClassCode);
            Assert.Contains("return TargetClass.Method3()", sourceClassCode);
        }

        [Fact]
        public void MoveMultipleMethods_WithCrossFileDependencies_ShouldMoveInCorrectOrder()
        {
            var source = @"
using System;

public class SourceClass
{
    public static int Method1() { return 1; }
    public static int Method2() { return Method1() + 1; }
    public static int Method3() { return Method2() + 1; }
}

public class TargetClass
{
}";

            var sourceClasses = new[] { "SourceClass", "SourceClass", "SourceClass" };
            var methodNames = new[] { "Method1", "Method2", "Method3" };
            var targetClasses = new[] { "TargetClass", "TargetClass", "TargetClass" };
            var accessMembers = new[] { "", "", "" };
            var accessMemberTypes = new[] { "", "", "" };
            var isStatic = new[] { true, true, true };

            var result = MoveMultipleMethodsInSource(
                source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

            var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
            var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

            Assert.Contains("public static int Method1()", targetClassCode);
            Assert.Contains("public static int Method2()", targetClassCode);
            Assert.Contains("public static int Method3()", targetClassCode);
            Assert.Contains("return Method1() + 1", targetClassCode);
            Assert.Contains("return Method2() + 1", targetClassCode);

            Assert.DoesNotContain("public static int Method1() { return 1; }", sourceClassCode);
            Assert.DoesNotContain("public static int Method2() { return Method1() + 1; }", sourceClassCode);
            Assert.DoesNotContain("public static int Method3() { return Method2() + 1; }", sourceClassCode);

            Assert.Contains("return TargetClass.Method1()", sourceClassCode);
            Assert.Contains("return TargetClass.Method2()", sourceClassCode);
            Assert.Contains("return TargetClass.Method3()", sourceClassCode);
        }

        [Fact]
        public void MoveMultipleMethods_WithInstanceDependencies_ShouldMoveInCorrectOrder()
        {
            var source = @"
using System;

public class SourceClass
{
    private int field1 = 1;
    public int Method1() { return field1; }
    public int Method2() { return Method1() + 1; }
    public int Method3() { return Method2() + 1; }
}

public class TargetClass
{
    private int field1 = 1;
}";

            var sourceClasses = new[] { "SourceClass", "SourceClass", "SourceClass" };
            var methodNames = new[] { "Method1", "Method2", "Method3" };
            var targetClasses = new[] { "TargetClass", "TargetClass", "TargetClass" };
            var accessMembers = new[] { "field1", "field1", "field1" };
            var accessMemberTypes = new[] { "field", "field", "field" };
            var isStatic = new[] { false, false, false };

            var result = MoveMultipleMethodsInSource(
                source, sourceClasses, methodNames, targetClasses, accessMembers, accessMemberTypes, isStatic);

            var targetClassCode = result.Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[1];
            var sourceClassCode = result.Split(new[] { "public class SourceClass" }, StringSplitOptions.None)[1].Split(new[] { "public class TargetClass" }, StringSplitOptions.None)[0];

            Assert.Contains("public int Method1(int field1)", targetClassCode);
            Assert.Contains("public int Method2(SourceClass @this)", targetClassCode);
            Assert.Contains("public int Method3(SourceClass @this)", targetClassCode);
            Assert.Contains("return @this.Method1() + 1", targetClassCode);
            Assert.Contains("return @this.Method2() + 1", targetClassCode);

            Assert.DoesNotContain("public int Method1() { return field1; }", sourceClassCode);
            Assert.DoesNotContain("public int Method2() { return Method1() + 1; }", sourceClassCode);
            Assert.DoesNotContain("public int Method3() { return Method2() + 1; }", sourceClassCode);

            Assert.Contains("return field1.Method1(field1)", sourceClassCode);
            Assert.Contains("return field1.Method2(this)", sourceClassCode);
            Assert.Contains("return field1.Method3(this)", sourceClassCode);
        }

        [Fact]
        public void MoveInstanceMethod_PrivateFieldInjectedAsParameter()
        {
            var source = @"public class SourceClass
{
    private int _value = 1;
    public int GetValue() { return _value + 2; }
}

public class TargetClass
{
}";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "SourceClass",
                "GetValue",
                "TargetClass",
                "_target",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "TargetClass", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("public int GetValue(int value)", formatted);
            Assert.Contains("return value + 2", formatted);
            Assert.Contains("return _target.GetValue(value)", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_InsertsDependenciesBeforeOptionalParam()
        {
            var source = @"public class SourceClass
{
    private int _value = 1;
    public int GetValue(int n = 5) { return _value + n; }
}

public class TargetClass
{
}";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "SourceClass",
                "GetValue",
                "TargetClass",
                "_target",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "TargetClass", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("public int GetValue(int value, int n = 5)", formatted);
            Assert.Contains("_target.GetValue(value, n)", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_OmitsUnusedThisParameter()
        {
            var source = @"public class SourceClass
    {
        public void Say() { System.Console.WriteLine(1); }
    }

public class TargetClass
{
}";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "SourceClass",
                "Say",
                "TargetClass",
                "_target",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "TargetClass", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("public void Say()", formatted);
            Assert.DoesNotContain("@this", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_PrefixesEventHandlerWithThis()
        {
            var source = @"using System;
public class ResProduct { public event EventHandler? Modified; }
public class SourceClass
{
    public void AddResProduct(ResProduct resProduct)
    {
        resProduct.Modified += Child_Modified;
    }

    private void Child_Modified(object? sender, EventArgs e) { }
}

public class TargetClass
{
}";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "SourceClass",
                "AddResProduct",
                "TargetClass",
                "_target",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "TargetClass", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("resProduct.Modified += @this.Child_Modified", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_QualifiesBaseProperty()
        {
            var source = @"using System;
class Base
{
    public string Name { get; set; }
}

class Derived : Base
{
    public void Method1()
    {
        Console.WriteLine(Name);
    }
}

class Target { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "Derived",
                "Method1",
                "Target",
                "_t",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "Target", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("@this.Name", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_RewritesThisArgument()
        {
            var source = @"public class A
{
    public void MethodBefore() { var m = new T(this); }
}
public class T { public T(A a){} }
public class Extracted { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "A",
                "MethodBefore",
                "Extracted",
                "_extracted",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "Extracted", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("_extracted.MethodBefore(this)", formatted);
            Assert.Contains("new T(@this)", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_QualifiesNestedClass()
        {
            var source = @"public class Outer
{
    internal class Helper { }
    public void Do() { Helper h = new Helper(); }
}

public class Target { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "Outer",
                "Do",
                "Target",
                "_t",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "Target", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("Outer.Helper", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_QualifiesNestedClassReturn()
        {
            var source = @"class A
{
    public C Method1()
    {
        return new C { Name = ""John"" };
    }

    public class C { public string Name { get; set; } }
}

class B { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "A",
                "Method1",
                "B",
                "_b",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "B", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("A.C Method1()", formatted);
            Assert.Contains("new A.C", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_UpdatesPrivateDependencyAccess()
        {
            var source = @"public class A
{ 
    private void Helper() { }
    public void Do() { Helper(); }
}

public class Target { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "A",
                "Do",
                "Target",
                "_t",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "Target", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("internal void Helper()", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_QualifiesInterfaceMethod()
        {
            var source = @"
using System;
interface IName { string GetName(); }
class Source : IName
{
    public string GetName() => ""N"";
    public string Print() => GetName();
}

class Target { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "Source",
                "Print",
                "Target",
                "_t",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "Target", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("@this.GetName()", formatted);
        }

        [Fact]
        public void MoveInstanceMethod_QualifiesBaseAfterInterface()
        {
            var source = @"
interface IThing { }
class Base { public int Value; }
class Derived : IThing, Base
{
    public int Read() => Value;
}

class Target { }";

            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            var result = MoveMethodsTool.MoveInstanceMethodAst(
                root,
                "Derived",
                "Read",
                "Target",
                "_t",
                "field");

            var finalRoot = MoveMethodsTool.AddMethodToTargetClass(result.NewSourceRoot, "Target", result.MovedMethod, result.Namespace);
            var formatted = Formatter.Format(finalRoot, new AdhocWorkspace()).ToFullString();

            Assert.Contains("@this.Value", formatted);
        }
    }
}

