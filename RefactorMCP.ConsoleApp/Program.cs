using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RefactorMCP.Tests")]

// MSBuild has to be located before anything loads a Microsoft.Build type, so
// it happens at process start rather than on the first solution load.
RefactoringHelpers.EnsureMsBuildRegistered();

return await Cli.RunAsync(args);
