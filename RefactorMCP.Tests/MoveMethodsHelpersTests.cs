using Xunit;
using System.Collections.Generic;
using RefactorMCP.ConsoleApp.Move;

namespace RefactorMCP.Tests;

public class MoveMethodsHelpersTests
{
    [Fact]
    public void GenerateAccessMemberName_UnusedName_ReturnsBaseName()
    {
        var existing = new HashSet<string> { "field1", "field2" };

        var result = MoveMethodAst.GenerateAccessMemberName(existing, "TargetClass");

        Assert.Equal("_targetClass", result);
        Assert.DoesNotContain(result, existing);
    }

    [Fact]
    public void GenerateAccessMemberName_ExistingNamesForceNumericSuffixes_ReturnsUniqueName()
    {
        var existing = new HashSet<string> { "_targetClass", "_targetClass1" };

        var result = MoveMethodAst.GenerateAccessMemberName(existing, "TargetClass");

        Assert.Equal("_targetClass2", result);
        Assert.DoesNotContain(result, existing);
    }
}
