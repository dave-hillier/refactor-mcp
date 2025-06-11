using Xunit;

namespace RefactorMCP.Tests;

public class RoslynTransformationTests
{
    [Fact]
    public void IntroduceVariableInSource_AddsVariable()
    {
        var input = "class Test\n{\n    void M()\n    {\n        Console.WriteLine(1 + 2);\n    }\n}\n";
        var expected = "class Test\n{\n    void M()\n    {\n        var result = Console.WriteLine(1 + 2);\n        result;\n    }\n}\n";
        var output = RefactoringTools.IntroduceVariableInSource(input, "5:27-5:31", "result");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void IntroduceParameterInSource_AddsParameter()
    {
        var input = "class Test\n{\n    int Add(int x, int y)\n    {\n        return x + y;\n    }\n}\n";
        var expected = "class Test\n{\n    int Add(int x, int y, object value)\n    {\n        return x + y;\n    }\n}\n";
        var output = RefactoringTools.IntroduceParameterInSource(input, "Add", "5:16-5:20", "value");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void MakeFieldReadonlyInSource_MakesReadonly()
    {
        var input = "class Test\n{\n    private string format = \"Currency\";\n\n    public Test()\n    {\n    }\n}\n";
        var expected = "class Test\n{\n    private readonly string format;\n\n    public Test()\n    {\n        format = \"Currency\";\n    }\n}\n";
        var output = RefactoringTools.MakeFieldReadonlyInSource(input, "format");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void TransformSetterToInitInSource_ReplacesSetter()
    {
        var input = "class Test\n{\n    public string Name { get; set; } = \"Default\";\n}\n";
        var expected = "class Test\n{\n    public string Name { get; init; } = \"Default\";\n}\n";
        var output = RefactoringTools.TransformSetterToInitInSource(input, "Name");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void ConvertToExtensionMethodInSource_TransformsMethod()
    {
        var input = "class C{void G(){}}";
        var expected = "class C { }\n\npublic static class CExtensions\n{\n    static void G(this C c)\n    { }\n}";
        var output = RefactoringTools.ConvertToExtensionMethodInSource(input, "G", null);
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithInstanceInSource_TransformsMethod()
    {
        var input = "class C{int f; int M(){return f;}}";
        var expected = "class C { int f; static int M(C i) { return i.f; } }";
        var output = RefactoringTools.ConvertToStaticWithInstanceInSource(input, "M", "i");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ConvertToStaticWithParametersInSource_TransformsMethod()
    {
        var input = "class C{int f; int M(){return f;}}";
        var expected = "class C { int f; static int M(int f) { return f; } }";
        var output = RefactoringTools.ConvertToStaticWithParametersInSource(input, "M");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void ExtractMethodInSource_CreatesMethod()
    {
        var input = "class T\n{\n    void M()\n    {\n        Console.WriteLine(\"hi\");\n    }\n}\n";
        var expected = "class T\n{\n    void M()\n    {\n        NewMethod();\n    }\n\n    private void NewMethod()\n    {\n        Console.WriteLine(\"hi\");\n    }\n}\n";
        var output = RefactoringTools.ExtractMethodInSource(input, "5:9-5:34", "NewMethod");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void IntroduceFieldInSource_AddsField()
    {
        var input = "class T\n{\n    int M(){return 1 + 2;}\n}\n";
        var expected = "class T\n{\n    int M() { return sum; }\n}\n";
        var output = RefactoringTools.IntroduceFieldInSource(input, "3:20-3:24", "sum", "private");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void SafeDeleteFieldInSource_RemovesField()
    {
        var input = "class C{int f;}";
        var expected = "class C { }";
        var output = RefactoringTools.SafeDeleteFieldInSource(input, "f");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteMethodInSource_RemovesMethod()
    {
        var input = "class C{void M(){}}";
        var expected = "class C { }";
        var output = RefactoringTools.SafeDeleteMethodInSource(input, "M");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteParameterInSource_RemovesParameter()
    {
        var input = "class C{void M(int x,int y){}}";
        var expected = "class C { void M(int x) { } }";
        var output = RefactoringTools.SafeDeleteParameterInSource(input, "M", "y");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void SafeDeleteVariableInSource_RemovesVariable()
    {
        var input = "class C{void M(){int x=1;}}";
        var expected = "class C { void M() { } }";
        var output = RefactoringTools.SafeDeleteVariableInSource(input, "1:18-1:25");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveInstanceMethodInSource_MovesMethod()
    {
        var input = "class A{void M(){}} class B{}";
        var expected = "class A\n{\n    private B b = new B();\n}\nclass B { public void M() { } }";
        var output = RefactoringTools.MoveInstanceMethodInSource(input, "A", "M", "B", "b", "field");
        Assert.Equal(expected, output.Trim());
    }

    [Fact]
    public void MoveStaticMethodInSource_MovesMethod()
    {
        var input = "class A{static void S(){}}";
        var expected = "class A { }\n\npublic class B\n{\n    static void S() { }\n}";
        var output = RefactoringTools.MoveStaticMethodInSource(input, "S", "B");
        Assert.Equal(expected, output.Trim());
    }
}
