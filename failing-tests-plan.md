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

## S7. ExtractMethod parameter and return inference — `fix/extract-method-inference`

- [ ] **The extracted method takes no parameters and returns void.**
      `ExtractMethodRewriter.cs:28-32` hardcodes `private void <name>()`, so the
      extracted body references identifiers that no longer exist. The documented
      example (`Examples/MethodTransformation/ExtractMethod.md`) promises
      `private async Task<OrderResult?> ValidateOrderAsync(Order order)`. Fix:
      infer parameters and a return type, which also means giving the single-file
      path a semantic model; update `ExtractMethodToolTests`' expected output.
      Test: `ExampleVerificationTests.ExtractMethodExample_RefactoringWorks`

### Still open in this area

`ValidateRange` bounds the *end* column but not the start column, so a range that
starts past the end of its line is still accepted and resolves into the next line.
`ExtractMethodToolTests.ExtractMethod_CreatesNewMethod` ("6:9-9:10", starting at
column 9 of a five-character line) depends on that, so fixing it means correcting
that range too.

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
