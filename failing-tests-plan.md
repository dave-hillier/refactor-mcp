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

## S1. SafeDelete soundness — `fix/safe-delete`

The tool must never delete something that is still referenced. Today it can, in
both modes.

- [ ] **Field used once is deleted (single-file).** `SafeDeleteTool.cs:140-141`
      counts `IdentifierNameSyntax` and refuses only when `references > 1`; the
      declaration is a declarator token, so one real use scores 1 and passes.
      Fix: refuse when `references > 0`. Test:
      `BugHuntTests.SafeDeleteFieldInSource_FieldUsedOnce_ShouldNotDelete`
- [ ] **Method called as `this.Helper()` is deleted.** `SafeDeleteTool.cs:194-196`
      only recognises a bare `IdentifierNameSyntax` invocation. Fix: count name
      occurrences, as the field path does. Test:
      `BugHuntTests.SafeDeleteMethodInSource_MethodCalledViaThis_ShouldNotDelete`
- [ ] **Solution mode has the same off-by-one.** `Count() - 1` at
      `SafeDeleteTool.cs:110`, `:167`, `:288` assumes the declaration is among
      `FindReferencesAsync`'s locations; it is not, so one reference scores 0.
      Same at `:320` (`> 1` for variables). Measured, not inferred. Fix: drop the
      subtraction. No test covers a one-reference refusal — add one.

## S2. Member walkers — `fix/member-walkers`

- [ ] **UnusedMembersWalker flags a field used once.** `:115` uses `count <= 1`
      where the method check above it uses `== 0`. Tests:
      `BugHuntTests.UnusedMembersWalker_FieldUsedOnce_ShouldNotBeFlaggedAsUnused`,
      `UnusedMembersWalkerTests.UnusedMembersWalker_DoesNotFlagUsedField`,
      `RefactoringOpportunityWalkerTests.RefactoringOpportunityWalker_NoSuggestionsForCleanCode`
- [ ] **UnusedMembersWalker misses `this.Helper()`.** `:36-44` counts bare
      invocations only. Test:
      `BugHuntTests.UnusedMembersWalker_MethodCalledViaThis_ShouldNotBeFlaggedAsUnused`
- [ ] **MethodAnalysisWalker misses `this._field`.** `:29-34` compares the
      member access's `Expression` with the identifier, which is `this`, not the
      name. Moved methods that use `this.` currently emit non-compiling code.
      Tests: `MethodAnalysisWalker_ThisDotField_ShouldDetectInstanceMemberUsage`,
      `..._ThisDotFieldRead_...`
- [ ] **InstanceMemberNameWalker collects statics (and consts).** `:8-19` adds
      every member, so a moved method can emit `@this.StaticMember` (CS0176). The
      generated-access-member collision guard has to keep seeing static names, so
      feed `MemberExists` instance plus static names. The existing test
      `InstanceMemberNameWalker_IncludesStaticFields` asserts today's behaviour
      and must be inverted. Tests:
      `BugHuntTests.InstanceMemberNameWalker_ShouldExcludeStaticFields` and
      `..._ShouldExcludeStaticProperties`
- [ ] **PrivateFieldInfoWalker misses implicitly private fields.** `:14` looks
      for the `private` keyword; a field with no modifier is private too, and
      today it is not captured as a moved method's parameter. The existing test
      `PrivateFieldInfoWalkerTests.cs:12-20` asserts the old behaviour. Test:
      `BugHuntTests.PrivateFieldInfoWalker_ImplicitlyPrivateField_ShouldBeDetected`

## S3. Expression bodies and structural rewrites — `fix/expression-bodies`

- [ ] **BodyOmitter crashes on expression-bodied members.** `BodyOmitter.cs:12-15`
      returns a `Block` where the base rewriter casts back to
      `ArrowExpressionClause`, so `summary://` throws for any file containing
      `=>`. Decided: leave the expression intact (delete the override) rather
      than invent a placeholder. `EXAMPLES.md` claims bodies render `// ...`;
      block bodies actually render `{}`, so correct the doc. Tests:
      `BugHuntTests.BodyOmitter_ExpressionBodiedMethod_ShouldNotThrow`, `..._Property_...`
- [ ] **InlineInvocationRewriter crashes on expression-bodied methods.**
      `:57` dereferences `_method.Body`. Fix: emit the expression as a single
      statement when the target is arrow-bodied. Test:
      `BugHuntTests.InlineInvocationRewriter_ExpressionBodiedMethod_ShouldNotThrow`
- [ ] **ExtractInterface destroys the existing base list (data loss).**
      `ExtractInterfaceTool.cs:95-99` builds a new `BaseList`, dropping a base
      class and any other interfaces; skip when the interface is already listed,
      or a second run emits CS0528. Test:
      `BugHuntTests.ExtractInterface_ClassWithExistingBaseClass_ShouldPreserveIt`
- [ ] **ExtractInterface emits an invalid accessor for arrow properties.**
      Same file, `:57-61`: an arrow property yields `int Count { } => x;`. Fix:
      synthesise a `get;` accessor and clear the expression body; the method
      branch has the same latent defect. Test:
      `BugHuntTests.ExtractInterface_ExpressionBodiedProperty_ShouldProduceValidAccessor`

## S4. Setter and field rewrites — `fix/setter-readonly`

- [ ] **SetterToInitRewriter drops the setter's modifiers, attributes and body.**
      `SetterToInitRewriter.cs:25-26` builds the init accessor from scratch.
      Tests: `BugHuntTests.SetterToInitRewriter_PrivateSetter_ShouldPreserveAccessModifier`,
      `..._ProtectedSetter_...`
- [ ] **ReadonlyFieldRewriter drops the initializer when there is no
      constructor.** `ReadonlyFieldRewriter.cs:22` strips it, and the constructor
      visitor never puts it back, so `private int _x = 30;` becomes
      `private readonly int _x;` (value lost) while the tool reports success.
      Reached by the workflow in S8; tighten that test to assert the value
      survives.

## S5. FeatureFlagRewriter — `fix/feature-flag`

- [ ] **Constructor injection depends on declaration order.**
      `FeatureFlagRewriter.cs:73` guards on `_done`/`_targetIf`, which are not yet
      set when the constructor is visited first, so the conventional layout
      generates a class calling `.Apply()` on a field that is never assigned.
      Fix: resolve the target `if` before visiting members. Test:
      `FeatureFlagRewriterTests.FeatureFlagRewriter_AddsConstructorParameter`
- [ ] **Generated `Apply` methods are private.** `:97` uses an empty modifier
      list on a node shared with both strategy classes, so they do not implement
      the interface (CS0535). Fix: add `public` to the two classes; leave the
      interface member idiomatic. Test:
      `BugHuntTests.FeatureFlagRewriter_StrategyClasses_ApplyMethodShouldBePublic`
- [ ] **Five tests assert on unformatted text.** The rewriter builds trivia-free
      nodes by design and the tool formats the result, so `ToFullString()` is
      `publicinterfaceITestStrategy{...}`. Decided: normalise in the tests, as
      `BugHuntTests` already does, rather than change what the rewriter emits.
      Tests: `AddsStrategyField`, `GeneratesStrategyInterface`,
      `GeneratesEnabledStrategy`, `GeneratesDisabledStrategy`, `HandlesNoElseBranch`

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
