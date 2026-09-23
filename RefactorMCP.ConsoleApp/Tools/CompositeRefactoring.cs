using Microsoft.CodeAnalysis;

/// <summary>
/// Runs the steps of a composite refactoring as a single change. Steps that write
/// files as they go, such as other tools the composite calls, are undone when a
/// later step refuses, so a refusal leaves every file and the session as they were.
/// </summary>
internal static class CompositeRefactoring
{
    public static async Task<T> RunAsync<T>(string solutionPath, Func<Task<T>> steps, CancellationToken cancellationToken = default)
    {
        var original = await RefactoringHelpers.GetOrLoadSolution(solutionPath, cancellationToken);
        try
        {
            return await steps();
        }
        catch
        {
            await RestoreAsync(solutionPath, original);
            throw;
        }
    }

    /// <summary>
    /// Writes back every document the steps changed, deletes the files they created
    /// and recreates those they deleted, then makes the original solution current.
    /// </summary>
    private static async Task RestoreAsync(string solutionPath, Solution original)
    {
        var current = await RefactoringHelpers.GetOrLoadSolution(solutionPath);
        if (current == original)
            return;

        await SolutionEdits.WriteAsync(current, original);
        foreach (var projectChange in original.GetChanges(current).GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetAddedDocuments())
            {
                var document = original.GetDocument(documentId)!;
                var text = await document.GetTextAsync();
                await File.WriteAllTextAsync(document.FilePath!, text.ToString());
            }
        }

        SessionRegistry.GetOrCreate(solutionPath).Replace(original);
    }
}
