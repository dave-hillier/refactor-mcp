# The failing tests: what was wrong and what was done

The suite went from 422 tests with 31 failures to 433 with none. None of the 31
were caused by the CLI and daemon work; they were pre-existing, and most were
real defects in the refactoring tools rather than bad expectations.
`BUG-REPORT.md` described many of them, but it was neither complete nor always
right — every note here was checked against the code.

Each stream was done in its own worktree and branch (`fix/<stream>`) and merged
back, so a stream's diff can still be read on its own with
`git log --oneline fix/<stream>`.

Nothing here changed the plan in `plan.md` (the CLI, daemon and MCP surface).

## What was fixed

- **SafeDelete** refused nothing when a symbol was referenced exactly once: the
  declaration is not among `SymbolFinder`'s locations, so subtracting one
  under-counted by one, and the single-file paths compared against one rather
  than zero. `this.Helper()` was not counted at all.
- **Member walkers** — `UnusedMembersWalker` scored a field's declaration as a
  reference in reverse and ignored `this.Helper()`; `MethodAnalysisWalker` could
  not see `this._field`; `InstanceMemberNameWalker` reported static and const
  members as instance ones; `PrivateFieldInfoWalker` missed implicitly private
  fields.
- **Expression bodies** — `BodyOmitter` crashed the `summary://` resource on any
  file containing `=>` (and the doc described output it never produced);
  `InlineInvocationRewriter` crashed on an expression-bodied target;
  `ExtractInterface` replaced the class's whole base list (data loss) and emitted
  an invalid accessor for an expression-bodied property.
- **Setter and field rewrites** — `SetterToInitRewriter` dropped an access
  modifier such as `private set;`; `ReadonlyFieldRewriter` dropped a field's
  initializer when the class had no constructor.
- **FeatureFlagRewriter** — the strategy classes did not implement the interface
  (`Apply` was private), and injection only happened when the flag check preceded
  the constructor. Five tests asserted on unformatted rewriter output.
- **RenameSymbol** could not reach a local or a parameter by name; it now does,
  and asks for line and column when the name is ambiguous.
- **ExtractMethod** produced a method with no parameters that always returned
  void, so the documented example could not compile; it also dropped every
  extracted statement but the first. It now infers parameters and the return.
- **Selection ranges** that ran past the end of their line were accepted and
  resolved into the next line. Both ends are now bounded, and the tests that
  leaned on the old behaviour have honest ranges.
- **CleanupUsings** applied project-wide diagnostics to one document's tree
  (PR #304, cherry-picked with the contributor's authorship).

## Open decisions

Two defects were found by the agents that fixed the above, and neither is worth
guessing at:

1. **ExtractMethod, local escaping the block.** A local declared inside the
   extracted block and used *after* the call site now yields code that does not
   compile. The old removal bug hid this by leaving the statements behind. The
   choices are to refuse the extraction with an explanation, or to return the
   local from the new method as well.
2. **FeatureFlagRewriter, no constructor to inject into.** When the class holding
   the flag check has no constructor at all, the generated strategy field is
   never assigned (a warning, then null at runtime). The choices are to synthesise
   a constructor or a default initialiser.

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
