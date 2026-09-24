using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using Xunit;

namespace RefactorMCP.Tests.Tools;

public class SymbolSearchToolTests : RefactorMCP.Tests.TestBase
{
    private bool _loaded;

    /// <summary>Adds a file to the solution, loaded afresh for the test's first file.</summary>
    private async Task<string> AddFile(string name, string code)
    {
        if (!_loaded)
            await LoadSolutionTool.LoadSolution(SolutionPath, null, CancellationToken.None);
        _loaded = true;
        var path = Path.Combine(TestOutputPath, name);
        await TestUtilities.CreateTestFile(path, code);
        var solution = await RefactoringHelpers.GetOrLoadSolution(SolutionPath);
        RefactoringHelpers.AddDocumentToProject(solution.Projects.First(), path);
        return path;
    }

    private static int Occurrences(string text, string value) => Regex.Matches(text, Regex.Escape(value)).Count;

    private static (int Line, int Column) Caret(string code, int line, string token)
    {
        var text = code.Replace("\r\n", "\n").Split('\n')[line - 1];
        return (line, text.IndexOf(token) + 1);
    }

    private const string Overloads = """
public class SearchCalc
{
    public int Add(int a) => a;
    public int Add(int a, int b) => a + b;
    public int Use() => Add(1) + Add(1, 2) + Add(3);
}
""";

    [Fact]
    public async Task FindReferences_OverloadChosenByLine_ListsOnlyItsCalls()
    {
        var file = await AddFile("SearchCalc.cs", Overloads);

        var result = await SymbolSearchTool.FindReferences(SolutionPath, file, "Add", line: 3);

        Assert.StartsWith("2 references to method SearchCalc.Add(int) in 1 file", result);
        Assert.Contains("SearchCalc.cs", result);
        Assert.Contains("5:25  public int Use()", result);
        Assert.Contains("5:46  public int Use()", result);
        Assert.DoesNotContain("5:34", result);
    }

    [Fact]
    public async Task FindReferences_AmbiguousName_ListsCandidates()
    {
        var file = await AddFile("SearchCalcAmbiguous.cs", Overloads);

        var error = await Assert.ThrowsAsync<McpException>(() =>
            SymbolSearchTool.FindReferences(SolutionPath, file, "Add"));

        Assert.Contains("Multiple symbols named 'Add'", error.Message);
        Assert.Contains("method SearchCalc.Add(int)", error.Message);
        Assert.Contains("method SearchCalc.Add(int, int)", error.Message);
    }

    [Fact]
    public async Task FindReferences_Property_MarksWrites()
    {
        var file = await AddFile("SearchCounter.cs", """
public class SearchCounter
{
    public int Count { get; set; }
    public void Bump() { Count = Count + 1; Count++; this.Count += 2; }
    public int Read() => Count;
}
""");

        var result = await SymbolSearchTool.FindReferences(SolutionPath, file, "Count");

        Assert.StartsWith("5 references to property SearchCounter.Count", result);
        Assert.Equal(3, Occurrences(result, "[write]"));
        Assert.Contains("5:26  public int Read()", result);
    }

    [Fact]
    public async Task FindReferences_LocalAtCaret_FindsItsUses()
    {
        const string code = """
public class SearchLocals
{
    public int First() { var total = 1; total += 2; return total; }
    public int Second() { var total = 5; return total; }
}
""";
        var file = await AddFile("SearchLocals.cs", code);
        var (line, column) = Caret(code, 3, "total");

        var result = await SymbolSearchTool.FindReferences(SolutionPath, file, "total", line, column);

        Assert.StartsWith("2 references to local total", result);
        Assert.Equal(1, Occurrences(result, "[write]"));
        Assert.DoesNotContain("Second", result);
    }

    [Fact]
    public async Task FindReferences_IncludeDeclarations_ListsTheDeclaration()
    {
        var file = await AddFile("SearchDeclared.cs", """
public class SearchDeclared
{
    private int _unused;
}
""");

        var none = await SymbolSearchTool.FindReferences(SolutionPath, file, "_unused");
        var withDeclaration = await SymbolSearchTool.FindReferences(SolutionPath, file, "_unused", includeDeclarations: true);

        Assert.Equal("No references to field SearchDeclared._unused", none);
        Assert.Contains("3:17  [declaration] private int _unused;", withDeclaration);
    }

    [Fact]
    public async Task FindReferences_Type_IncludesConstructionAcrossFiles()
    {
        var widget = await AddFile("SearchWidget.cs", """
public class SearchWidget
{
    public SearchWidget() { }
}
""");
        await AddFile("SearchWidgetUser.cs", """
public class SearchWidgetUser
{
    public SearchWidget Make() => new SearchWidget();
}
""");

        var result = await SymbolSearchTool.FindReferences(SolutionPath, widget, "SearchWidget");

        Assert.Contains("in 1 file", result);
        Assert.Contains("SearchWidgetUser.cs", result);
        Assert.Contains("3:12  public SearchWidget Make() => new SearchWidget();", result);
        Assert.Contains("3:39  public SearchWidget Make() => new SearchWidget();", result);
        Assert.DoesNotContain("via", result);
    }

    [Fact]
    public async Task FindReferences_Implementation_MarksUsesThroughTheInterface()
    {
        const string code = """
public interface ISearchShape { double Area(); }
public class SearchCircle : ISearchShape { public double Area() => 3; }
public class SearchSquare : ISearchShape { public double Area() => 4; }
public class SearchShapes { public double Total(ISearchShape s, SearchCircle c) => s.Area() + c.Area(); }
""";
        var file = await AddFile("SearchShapes.cs", code);

        var result = await SymbolSearchTool.FindReferences(SolutionPath, file, "Area", line: 2);

        Assert.Contains("[via method ISearchShape.Area()]", result);
        Assert.Equal(2, Occurrences(result, "4:"));
    }

    [Fact]
    public async Task FindReferences_MaxResults_SaysHowManyMore()
    {
        var file = await AddFile("SearchMany.cs", """
public class SearchMany
{
    private int _n;
    public int A() => _n;
    public int B() => _n;
    public int C() => _n;
}
""");

        var result = await SymbolSearchTool.FindReferences(SolutionPath, file, "_n", maxResults: 1);

        Assert.StartsWith("3 references", result);
        Assert.Contains("4:23", result);
        Assert.DoesNotContain("5:23", result);
        Assert.EndsWith("... 2 more not shown; raise maxResults to see them", result);
    }

    [Fact]
    public async Task FindImplementations_Interface_ListsImplementingTypes()
    {
        var file = await AddFile("SearchImplementations.cs", """
public interface ISearchPrinter { void Print(); }
public class SearchInk : ISearchPrinter { public void Print() { } }
public class SearchLaser : ISearchPrinter { public void Print() { } }
public class SearchColourLaser : SearchLaser { }
""");

        var types = await SymbolSearchTool.FindImplementations(SolutionPath, file, "ISearchPrinter");
        var members = await SymbolSearchTool.FindImplementations(SolutionPath, file, "Print", line: 1);

        Assert.StartsWith("3 implementations of interface ISearchPrinter", types);
        Assert.Contains("SearchImplementations.cs:2:14  class SearchInk", types);
        Assert.Contains("class SearchColourLaser", types);
        Assert.StartsWith("2 implementations of method ISearchPrinter.Print()", members);
        Assert.Contains("method SearchLaser.Print()", members);
    }

    [Fact]
    public async Task FindImplementations_ClassMember_IsRefused()
    {
        var file = await AddFile("SearchNotInterface.cs", """
public class SearchPlain { public virtual void Run() { } }
""");

        var error = await Assert.ThrowsAsync<McpException>(() =>
            SymbolSearchTool.FindImplementations(SolutionPath, file, "Run"));

        Assert.Contains("use find-overrides", error.Message);
    }

    private const string Animals = """
public abstract class SearchAnimal { public abstract string Speak(); public string Name() => "animal"; }
public class SearchDog : SearchAnimal { public override string Speak() => "woof"; }
public class SearchPuppy : SearchDog { public override string Speak() => "yip"; }
""";

    [Fact]
    public async Task FindOverrides_ListsEveryLevel()
    {
        var file = await AddFile("SearchAnimals.cs", Animals);

        var result = await SymbolSearchTool.FindOverrides(SolutionPath, file, "Speak", line: 1);

        Assert.StartsWith("2 overrides of method SearchAnimal.Speak()", result);
        Assert.Contains("SearchAnimals.cs:2:64  method SearchDog.Speak()\n", result);
        Assert.Contains("method SearchPuppy.Speak() [overrides SearchDog.Speak()]", result);
    }

    [Fact]
    public async Task FindOverrides_NonVirtualMember_IsRefused()
    {
        var file = await AddFile("SearchAnimalsSealed.cs", Animals);

        var error = await Assert.ThrowsAsync<McpException>(() =>
            SymbolSearchTool.FindOverrides(SolutionPath, file, "Name"));

        Assert.Contains("is not virtual, abstract or an override", error.Message);
    }

    [Fact]
    public async Task FindCallers_GroupsCallSitesByCaller()
    {
        var file = await AddFile("SearchService.cs", """
public class SearchService
{
    public void Log(string m) { }
    public void A() { Log("a"); Log("b"); }
    public void B() => Log("c");
}
""");

        var result = await SymbolSearchTool.FindCallers(SolutionPath, file, "Log");
        var truncated = await SymbolSearchTool.FindCallers(SolutionPath, file, "Log", maxResults: 1);

        Assert.StartsWith("3 calls to method SearchService.Log(string) from 2 callers", result);
        Assert.Contains("\nmethod SearchService.A()\n", result);
        Assert.Contains("SearchService.cs:4:23  public void A()", result);
        Assert.Contains("SearchService.cs:5:24  public void B()", result);
        Assert.EndsWith("... 2 more not shown; raise maxResults to see them", truncated);
    }

    [Fact]
    public async Task FindCallers_MarksCallsThroughTheInterface()
    {
        var file = await AddFile("SearchCallersThrough.cs", """
public interface ISearchClock { int Now(); }
public class SearchClock : ISearchClock { public int Now() => 1; }
public class SearchTimer { public int Wait(ISearchClock a, SearchClock b) => a.Now() + b.Now(); }
""");

        var result = await SymbolSearchTool.FindCallers(SolutionPath, file, "Now", line: 2);

        Assert.Contains("method SearchTimer.Wait(ISearchClock, SearchClock) [through method ISearchClock.Now()]", result);
        Assert.Contains("\nmethod SearchTimer.Wait(ISearchClock, SearchClock)\n", result);
    }

    [Fact]
    public async Task FindCallers_ExtensionMethod_FindsReducedCalls()
    {
        var file = await AddFile("SearchExtensions.cs", """
public static class SearchExtensions { public static int Twice(this int x) => x * 2; }
public class SearchExtensionUser { public int M() => 3.Twice(); }
""");

        var result = await SymbolSearchTool.FindCallers(SolutionPath, file, "Twice");

        Assert.StartsWith("1 call to method SearchExtensions.Twice(int) from 1 caller", result);
        Assert.Contains("method SearchExtensionUser.M()", result);
    }

    [Fact]
    public async Task FindCallers_Field_IsRefused()
    {
        var file = await AddFile("SearchCallersField.cs", """
public class SearchCallersField { private int _value; }
""");

        var error = await Assert.ThrowsAsync<McpException>(() =>
            SymbolSearchTool.FindCallers(SolutionPath, file, "_value"));

        Assert.Contains("use find-references", error.Message);
    }

    [Fact]
    public async Task FindReferences_ThroughDispatcher_BindsOptionalArguments()
    {
        var file = await AddFile("SearchDispatch.cs", """
public class SearchDispatch
{
    private int _x;
    public int Get() => _x;
}
""");

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(new
        {
            solutionPath = SolutionPath,
            filePath = file,
            symbolName = "_x",
            includeDeclarations = true,
        }))!;
        var result = await ToolDispatcher.Default.InvokeAsync("find-references", arguments);

        Assert.False(result.IsError, result.Text);
        Assert.StartsWith("2 references to field SearchDispatch._x", result.Text);
    }
}
