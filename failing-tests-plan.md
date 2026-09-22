# Plan: fixing the failing tests

The suite is 422 tests with 31 failures. None of them were caused by the CLI and
daemon work; they are pre-existing, and most are real defects in the refactoring
tools rather than bad expectations. `BUG-REPORT.md` describes many of them, but
it is neither complete nor always right — the notes below were checked against
the code as it stands.

This file is a worklist: delete an item when it lands, and delete a stream when
it is empty.

## How the work is organised

Streams group by the files they touch, so each one can be done in its own
worktree and branch (`fix/<stream>`) and merged back without fighting over the
same file. Nothing here changes the plan in `plan.md` (the CLI, daemon and MCP
surface); that work is complete.

Ordering is mechanical first, then the ones that change behaviour, so the suite
is greener before the riskier changes land.

## S6. RenameSymbol locals and parameters — `fix/rename-locals`

- [ ] **Name-only rename cannot reach a local or a parameter.**
      `RenameSymbolTool.cs:84` asks `FindDeclarationsAsync`, which returns
      declarations of types and members only. Fix: fall back to a document-scoped
      syntax search resolved through the semantic model, renaming when exactly
      one match and otherwise asking for line/column. Tests:
      `RenameSymbolToolTests.RenameSymbol_LocalVariable_RenamesVariableAndUsages`,
      `..._Parameter_...`

## S7. ExtractMethod parameter and return inference — `fix/extract-method-inference`

- [ ] **The extracted method takes no parameters and returns void.**
      `ExtractMethodRewriter.cs:28-32` hardcodes `private void <name>()`, so the
      extracted body references identifiers that no longer exist. The documented
      example (`Examples/MethodTransformation/ExtractMethod.md`) promises
      `private async Task<OrderResult?> ValidateOrderAsync(Order order)`. Fix:
      infer parameters and a return type, which also means giving the single-file
      path a semantic model; update `ExtractMethodToolTests`' expected output.
      Test: `ExampleVerificationTests.ExtractMethodExample_RefactoringWorks`

## S8. Test-only staleness — `fix/test-ranges`

- [ ] **MakeFieldReadonly workflow: the range is one line out.**
      `AnalyzeThenRefactorTests.cs` uses `"8:16-8:18"` for `30` on line 7 of its
      fixture. Fix the range and the comment. Test:
      `AnalyzeThenRefactorTests.Workflow_IntroduceFieldThenMakeReadonly`
- [ ] **IntroduceVariable example: the range straddles a line into an object
      initializer.** Use `"20:35-21:96"`, which is exactly the
      `transactions.Where(...)` chain. Test:
      `ExampleVerificationTests.IntroduceVariableExample_RefactoringWorks`
- [ ] **`ValidateRange` never bounds columns against the line length**, which is
      why the range above silently crossed a newline instead of failing. Reject
      columns past the end of a line so such a range is reported, not guessed at.

## Open pull requests

- **#304** — done: cherry-picked into `fix/cleanup-usings` with the contributor's
  authorship intact, and verified against the workflow test it unblocks. The PR
  itself can be closed on GitHub once this branch lands, referencing the commit.
- **#298** — superseded: the dispatcher normalises tool names in any spelling, and
  the playback path it patched no longer exists. Its other two changes are
  trailing-whitespace only. Recommend closing.
- **#306** — a hand-written grouped MCP surface that replaces attribute discovery
  with seven switch methods and a parallel operation catalog, and renames call
  parameters (`name` for `methodName`). It conflicts with the dispatcher, the CLI
  and the daemon, and would leave the CLI seeing only the grouped seven. Recommend
  closing; if a smaller MCP surface is still wanted, build it as a grouping table
  over the attribute metadata.

## Out of scope

Other `BUG-REPORT.md` findings with no failing test (for example the
name-based reference counting in `SafeDeleteTool` reaching false positives, and
the load-time diagnostics in `CleanupUsingsTool`'s single-file path).
