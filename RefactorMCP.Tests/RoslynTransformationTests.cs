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
}
