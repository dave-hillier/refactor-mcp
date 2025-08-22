using Xunit;

// Tests share a single AdhocWorkspace via RefactoringHelpers.SharedWorkspace.
// Running them in parallel causes interference, so we disable parallelization.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
