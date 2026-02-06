# Bug Report: RefactorMCP Comprehensive Code Review

**Date:** 2026-02-06
**Scope:** Full codebase review (~199 C# files)
**Methodology:** File-by-file static analysis across 5 areas: Tools, SyntaxRewriters, SyntaxWalkers, Infrastructure, and Tests

---

## Summary

| Severity | Count |
|----------|-------|
| Critical | 8     |
| High     | 20    |
| Medium   | 30    |
| Low      | 20    |
| **Total** | **78** |

---

## CRITICAL Bugs

### C1. InlineMethodTool: Same-file inline changes silently overwritten (DATA LOSS)

**File:** `Tools/InlineMethodTool.cs:50-58`

`InlineReferences` writes inlined call-site changes to disk. Then `document.GetSyntaxRootAsync()` returns the **original** immutable root (Roslyn `Document` objects are immutable). The method declaration is removed from this old root and written back to disk, **overwriting** all same-file inlining work.

### C2. ConvertToExtensionMethodTool: Stale node reference causes extension class to be lost (DATA LOSS)

**File:** `Tools/ConvertToExtensionMethodTool.cs:102-108, 200-208`

After `ReplaceNode(method, wrapperMethod)`, every ancestor is recreated with new identity. `classDecl.Parent` yields the **old** namespace node. `newRoot.ReplaceNode(ns, updatedNs)` silently returns `newRoot` unchanged because `ns` is not found in the new tree (Roslyn uses reference identity). The extension class is never added to the output.

### C3. ExtractInterfaceTool: Existing base list completely overwritten (DATA LOSS)

**File:** `Tools/ExtractInterfaceTool.cs:92-96`

`WithBaseList` **replaces** the entire base list. If the class already inherits from a base class or implements other interfaces (`class Foo : Bar, IDisposable`), they are all silently removed.

### C4. SafeDeleteTool: Off-by-one allows deletion of referenced symbols

**File:** `Tools/SafeDeleteTool.cs:109-111, 167-168, 288-289`

`SymbolFinder.FindReferencesAsync` returns `Locations` that contain **reference** locations (uses), not declarations. The `- 1` subtraction assumes the declaration is in `Locations`, but it typically is not. A symbol with exactly one reference will have `count - 1 = 0`, pass the safety check, and be deleted despite being used. Appears in three places (fields, methods, variables).

### C5. BodyOmitter: Returns wrong node type, crashes on expression-bodied members

**File:** `SyntaxRewriters/BodyOmitter.cs:13-14`

`VisitArrowExpressionClause` returns a `BlockSyntax`, but the Roslyn rewriter expects an `ArrowExpressionClauseSyntax`. The default `VisitMethodDeclaration` casts: `var expressionBody = (ArrowExpressionClauseSyntax?)Visit(node.ExpressionBody)`. This throws `InvalidCastException` at runtime for all expression-bodied members.

### C6. ExtractMethodRewriter: Stale node references when removing statements from mutated tree

**File:** `SyntaxRewriters/ExtractMethodRewriter.cs:39-41`

After `body.ReplaceNode(statements.First(), methodCall)`, the remaining nodes in `_statements.Skip(1)` are references from the **original** tree. `RemoveNode` uses reference identity, so these stale references are not found -- silently leaving extracted statements duplicated in both the original and new method.

### C7. MSBuildWorkspace disposed while cached Solution still in use

**File:** `Infrastructure/LoadSolutionTool.cs:45`, `Infrastructure/RefactoringHelpers.cs:77-81`

The workspace is created with `using`, meaning it is disposed at end of scope. But the `Solution` is stored in cache and used afterward. Operations requiring workspace services (semantic models, metadata references) may fail with `ObjectDisposedException`.

### C8. AnalyzeThenRefactorTests: Calls non-existent method (compilation error)

**File:** `Tests/Workflows/AnalyzeThenRefactorTests.cs:51, 135, 138`

Three tests call `SafeDeleteTool.SafeDelete(...)` which does not exist. The class only exposes `SafeDeleteField`, `SafeDeleteMethod`, `SafeDeleteParameter`, and `SafeDeleteVariable`. This is a compilation error.

---

## HIGH Bugs

### H1. MethodMetricsWalker: False "make-static" suggestions

**File:** `SyntaxWalkers/MethodMetricsWalker.cs:34-35`

Only detects `this` expressions and `MemberAccessExpressionSyntax` for instance access. Completely misses bare `IdentifierNameSyntax` (e.g., `_count++`), which is the dominant C# style. Generates incorrect refactoring suggestions for virtually any method that accesses fields without `this.` prefix.

### H2. CleanupUsingsTool: Project-wide diagnostics applied to single document

**File:** `Tools/CleanupUsingsTool.cs:50-55`

`compilation.GetDiagnostics()` returns diagnostics for the **entire project**. `root.FindNode(d.Location.SourceSpan)` with a span from a different file will either throw `ArgumentOutOfRangeException` or return a wrong node.

### H3. Thread-unsafe static HashSet in concurrent MCP server

**File:** `Tools/MoveMethodTool.cs:22`

`HashSet<string>` accessed from multiple async operations without synchronization. `HashSet<T>` is not thread-safe; concurrent reads/writes can cause corruption or infinite loops.

### H4. AddObserverTool: NullReferenceException on expression-bodied methods

**File:** `Tools/AddObserverTool.cs:82`

`method.Body!` is null for expression-bodied methods (`void Foo() => DoSomething()`). The null-forgiving operator suppresses warnings but not runtime crashes.

### H5. MoveMethodTool: null! default parameters cause NRE downstream

**File:** `Tools/MoveMethodTool.cs:238-239`

`string[] constructorInjections = null!` and `parameterInjections = null!` -- these are actually null at runtime. Downstream `foreach` on null `IEnumerable` throws `NullReferenceException`.

### H6. MoveTypeToFileTool: Non-atomic file operations risk data loss

**File:** `Tools/MoveTypeToFileTool.cs:52-79`

Source file is modified (type removed) at line 55, but validation and target file creation happen later. If the process crashes or write fails, the type is permanently lost.

### H7. FeatureFlagRewriter: Generated strategy methods lack public modifier

**File:** `SyntaxRewriters/FeatureFlagRewriter.cs:94-114`

`Apply` methods created with empty modifier list, defaulting to `private`. Generated code fails to compile because interface implementation method is implicitly private.

### H8. StaticFieldRewriter: InvalidCastException on already-qualified static fields

**File:** `SyntaxRewriters/StaticFieldRewriter.cs:27-34`

Returns `MemberAccessExpressionSyntax` where Roslyn expects `SimpleNameSyntax`. Causes `InvalidCastException` when static field already appears in a member access expression.

### H9. ParameterRewriter: Does not replace parameter on left side of member access

**File:** `SyntaxRewriters/ParameterRewriter.cs:37-38`

When inlining `void Foo(MyClass obj) { obj.DoSomething(); }` called as `Foo(myInstance)`, the parameter `obj` on the left side of `obj.DoSomething()` is not replaced. Produces code with unresolved identifiers.

### H10. MethodCallRewriter: Does not recursively visit arguments

**File:** `SyntaxRewriters/MethodCallRewriter.cs:19-33`

Both matched branches return without calling `base.VisitInvocationExpression`. Arguments containing nested method calls that need rewriting are never processed.

### H11. InstanceMemberNameWalker: Collects static members

**File:** `SyntaxWalkers/InstanceMemberNameWalker.cs:8-20`

Named `InstanceMemberNameWalker` but collects ALL fields and properties including static ones. Leads to incorrect downstream analysis.

### H12. PrivateFieldInfoWalker: Misses implicitly private fields

**File:** `SyntaxWalkers/PrivateFieldInfoWalker.cs:14`

Only detects fields with explicit `private` keyword. In C#, fields without access modifiers are implicitly private (e.g., `int _count;`). Misses the majority of private fields in real code.

### H13. ImplicitInstanceMemberWalker: Parameter/local scoping bug across methods

**File:** `SyntaxWalkers/ImplicitInstanceMemberWalker.cs:10-25`

`_parameters` and `_locals` accumulated across entire walk. Parameters from Method A leak into scope of Method B, causing incorrect exclusion of instance member references.

### H14. MethodAnalysisWalker: `this.member` access not detected

**File:** `SyntaxWalkers/MethodAnalysisWalker.cs:29-34`

For `this.field`, the identifier `field` is the `Name` (right side) of the member access. The condition `ma.Expression == node` is false because `Expression` is `this`, not `field`. Code using `this.` prefix is not detected as instance member usage.

### H15. UnusedMembersWalker: `this.Method()` calls not counted

**File:** `SyntaxWalkers/UnusedMembersWalker.cs:36-44`

Only `IdentifierNameSyntax` is checked. `this.Method()` has `MemberAccessExpressionSyntax` as expression. Methods called exclusively via `this.Method()` falsely reported as unused.

### H16. RefreshFileMetrics never actually refreshes

**File:** `Infrastructure/MetricsProvider.cs:70-73`

Delegates to `GetFileMetrics` which checks cache first. Cache never invalidated before call. Metrics always stale after refactoring.

### H17. Race condition in ClearAllCaches

**File:** `Infrastructure/RefactoringHelpers.cs:28-36`

Dispose-and-replace pattern on shared static fields without synchronization. Between `SolutionCache.Dispose()` and `SolutionCache = new MemoryCache(...)`, concurrent readers get `ObjectDisposedException`.

### H18. TargetInvocationException not unwrapped in JSON mode

**File:** `Program.cs:134-137`

`method.Invoke()` wraps exceptions in `TargetInvocationException`. Error message shows unhelpful "Exception has been thrown by the target" instead of actual error.

### H19. Tool name lookup doesn't handle kebab-case

**File:** `Program.cs:79`

JSON mode compares incoming tool name against `MethodInfo.Name` (PascalCase). Documented kebab-case names (`load-solution`) never match `LoadSolution`. The `--json` CLI mode is non-functional with documented names.

### H20. MoveInstanceMethodToolTests: Expected outputs defined but never asserted

**File:** `Tests/Tools/MoveInstanceMethodToolTests.cs` (all tests)

Every test defines `expectedSource` and `expectedTarget` constants that are never used in assertions. Tests only use weak `Assert.Contains` checks. Tool could produce structurally incorrect output and tests would still pass.

---

## MEDIUM Bugs

### M1. SafeDeleteTool: Incomplete method reference detection in single-file mode
**File:** `Tools/SafeDeleteTool.cs:194-196` -- Only detects `MethodName()`, misses `this.MethodName()`, method groups, delegates.

### M2. InlineMethodTool: Single-file mode rejects methods with parameters
**File:** `Tools/InlineMethodTool.cs:76` -- Solution mode has no restriction; inconsistent behavior.

### M3. FeatureFlagRefactorTool: JSON injection via unescaped file paths
**File:** `Tools/FeatureFlagRefactorTool.cs:77` -- Backslashes in Windows paths produce invalid JSON.

### M4. IntroduceParameterTool: Type hardcoded as "object" in single-file mode
**File:** `Tools/IntroduceParameterTool.cs:82` -- Solution mode correctly resolves type via semantic model.

### M5. MoveMethodAst: Generic type arguments dropped when needsThisParameter is false
**File:** `Tools/MoveMethodAst.cs:664-666` -- Stub method delegation call omits type arguments.

### M6. MoveMethodAst: NRE on using directives with null Name
**File:** `Tools/MoveMethodAst.cs:833-837` -- Modern Roslyn `UsingDirectiveSyntax.Name` can be null.

### M7. SafeDeleteTool: Name-based reference counting has false positives
**File:** `Tools/SafeDeleteTool.cs:140-141` -- Counts all identifiers with same name, not just field references.

### M8. ConstructorInjectionTool: Fragile string-based error signaling
**File:** `Tools/ConstructorInjectionTool.cs:41,57,70` -- Uses `StartsWith("Error:")` instead of exceptions.

### M9. MoveMethodFileService: String equality for file path comparison
**File:** `Tools/MoveMethodFileService.cs:34,116` -- Fails on case-insensitive file systems (Windows).

### M10. ExtensionMethodRewriter: Does not qualify LHS instance members in member access
**File:** `SyntaxRewriters/ExtensionMethodRewriter.cs:71` -- `_field.Property` should become `param._field.Property` but doesn't.

### M11. InstanceMemberQualifierRewriter: Same LHS qualification bug
**File:** `SyntaxRewriters/InstanceMemberQualifierRewriter.cs:39` -- Same issue as M10.

### M12. SetterToInitRewriter: Loses setter body, modifiers, and attributes
**File:** `SyntaxRewriters/SetterToInitRewriter.cs:25-27` -- Creates init accessor from scratch, discarding original setter data.

### M13. ConstructorInjectionRewriter: Modifies ALL constructors and classes in tree
**File:** `SyntaxRewriters/ConstructorInjectionRewriter.cs:63-128` -- Should be scoped to specific class.

### M14. StaticConversionRewriter: Replaces parameter list with original, losing visited changes
**File:** `SyntaxRewriters/StaticConversionRewriter.cs:65-66` -- Uses `method.ParameterList` (unvisited) instead of `result.ParameterList`.

### M15. ExtractMethodRewriter: NRE if containingMethod has expression body
**File:** `SyntaxRewriters/ExtractMethodRewriter.cs:38` -- `containingMethod.Body!` is null for `int Foo() => 42;`.

### M16. InlineInvocationRewriter: Assumes method has block body
**File:** `SyntaxRewriters/InlineInvocationRewriter.cs:57` -- Expression-bodied void methods crash the inliner.

### M17. InlineInvocationRewriter: Incomplete parameter mapping with optional/params
**File:** `SyntaxRewriters/InlineInvocationRewriter.cs:52-54` -- `Zip` stops at shorter sequence.

### M18. IdentifierRenameRewriter: Dictionary uses default ISymbol equality
**File:** `SyntaxRewriters/IdentifierRenameRewriter.cs:27` -- Should use `SymbolEqualityComparer.Default`.

### M19. ComplexityWalker: Switch sections vs case labels miscounting
**File:** `SyntaxWalkers/ComplexityWalker.cs:65-66` -- Counts sections, not case labels. Fall-through cases undercounted.

### M20. ComplexityWalker: Missing SwitchExpression handling
**File:** `SyntaxWalkers/ComplexityWalker.cs` -- C# 8+ switch expressions not counted.

### M21. TrackedNameWalker: Child nodes skipped when TryRecordInvocation returns true
**File:** `SyntaxWalkers/TrackedNameWalker.cs:71-75` -- Arguments of recorded invocations never visited.

### M22. UnusedMembersWalker: Only skips public methods, not protected/internal
**File:** `SyntaxWalkers/UnusedMembersWalker.cs:79,105` -- Protected/internal members falsely reported as unused.

### M23. UseInterfaceWalker: Interface member matching only checks by name
**File:** `SyntaxWalkers/UseInterfaceWalker.cs:39` -- Doesn't verify signature, kind, or generic arity.

### M24. MethodAndMemberVisitor: Overloaded methods silently dropped
**File:** `SyntaxWalkers/MethodAndMemberVisitor.cs:27` -- Dictionary keyed by name only; first overload wins.

### M25. MethodStaticWalker: Same overload clobbering
**File:** `SyntaxWalkers/MethodStaticWalker.cs:22` -- Last overload wins; order-dependent.

### M26. MethodDependencyWalker: False positives from member access on other objects
**File:** `SyntaxWalkers/MethodDependencyWalker.cs:22-23` -- Cannot distinguish `this.Process()` from `someOtherObject.Process()`.

### M27. AdhocWorkspace leaked every call in SummaryResources
**File:** `Infrastructure/SummaryResources.cs:28` -- Not disposed; cumulative memory leak in long-running server.

### M28. Directory.SetCurrentDirectory is process-global in server context
**File:** `Infrastructure/LoadSolutionTool.cs:36`, `Infrastructure/RefactoringHelpers.cs:74,80` -- Races in concurrent MCP server.

### M29. MetricsProvider cache never cleared by ClearAllCaches
**File:** `Infrastructure/MetricsProvider.cs:14` -- Stale metrics after solution reload.

### M30. AstTransformations: Incorrectly skips qualifying LHS of member access
**File:** `Infrastructure/AstTransformations.cs:31,43` -- Same pattern as M10/M11.

---

## LOW Bugs

### L1. MoveMethodAst: Wrong overload replaced in dependency updates (`Tools/MoveMethodAst.cs:254-261`)
### L2. FeatureFlagRefactorTool: Silent exception swallowing in Log (`Tools/FeatureFlagRefactorTool.cs:74-83`)
### L3. SafeDeleteTool: Name-based variable reference check unreliable (`Tools/SafeDeleteTool.cs:319-321`)
### L4. MoveMultipleMethodsTool: Partial failure cascades to subsequent moves (`Tools/MoveMultipleMethodsTool.cs:155-178`)
### L5. MoveMethodTool: Null-unsafe project access (`Tools/MoveMethodTool.cs:490`)
### L6. MoveTypeToFileTool: Nested types matched instead of top-level (`Tools/MoveTypeToFileTool.cs:41-44`)
### L7. NestedClassRewriter: Double-qualifies identifiers in QualifiedNameSyntax (`SyntaxRewriters/NestedClassRewriter.cs:23-55`)
### L8. InstanceMemberRewriter: WhenNotNull not visited when Expression changes (`SyntaxRewriters/InstanceMemberRewriter.cs:32-42`)
### L9. DeclarationRemovalRewriter: SeparatedList loses separator trivia (`SyntaxRewriters/DeclarationRemovalRewriter.cs:56`)
### L10. ExpressionIntroductionRewriter: Replaces ALL structurally equivalent expressions (`SyntaxRewriters/ExpressionIntroductionRewriter.cs:33-36`)
### L11. ReadonlyFieldRewriter: Unconditionally strips initializer even if replacement is null (`SyntaxRewriters/ReadonlyFieldRewriter.cs:25,37`)
### L12. NestedClassNameWalker: Missing struct, record, and interface nested types (`SyntaxWalkers/NestedClassNameWalker.cs`)
### L13. UnusedMembersWalker: _fieldRefs counts ALL identifiers, not just field references (`SyntaxWalkers/UnusedMembersWalker.cs:55-61`)
### L14. RefactoringOpportunityWalker: Duplicate suggestions on repeated PostProcessAsync calls (`SyntaxWalkers/RefactoringOpportunityWalker.cs:32-39`)
### L15. Fire-and-forget async silently loses exceptions (`Infrastructure/RefactoringHelpers.cs:94`)
### L16. ToKebabCase mishandles acronyms -- `HTMLParser` becomes `h-t-m-l-parser` (`Program.cs:161-172`)
### L17. MetricsProvider: Bare catch swallows all exceptions (`Infrastructure/MetricsProvider.cs:58-60`)
### L18. VersionTool: Fails for single-file deployments (`Infrastructure/VersionTool.cs:14`)
### L19. TestBase: TestOutputRoot directory never cleaned up (`Tests/Tools/TestBase.cs:10-11`)
### L20. Duplicate GetSolutionPath() implementations (`Tests/Tools/TestHelpers.cs` and `TestUtilities.cs`)

---

## Top Priority Fixes

The following bugs should be addressed first due to their potential for **data loss** or **silent incorrect behavior**:

1. **C1 (InlineMethodTool)** -- Same-file inlining overwrites its own changes
2. **C4 (SafeDeleteTool)** -- Off-by-one allows deletion of actively used symbols
3. **C6 (ExtractMethodRewriter)** -- Stale node references leave duplicated code
4. **C7 (MSBuildWorkspace)** -- Disposed workspace backing cached solutions
5. **H1 (MethodMetricsWalker)** -- False make-static suggestions for nearly all methods
6. **H2 (CleanupUsingsTool)** -- Cross-file diagnostics crash or corrupt output
7. **H19 (Program.cs)** -- JSON CLI mode doesn't work with documented tool names

---

## Patterns Observed

Several bug patterns recur throughout the codebase:

1. **Stale Roslyn node references after tree mutation** (C1, C2, C6, M14) -- After `ReplaceNode`/`RemoveNode`, old node references are used in the new tree where they don't exist.

2. **Missing expression-body handling** (C5, H4, M15, M16) -- Many tools assume `method.Body != null` and crash on expression-bodied members (`=> expr`).

3. **Incorrect MemberAccessExpression side detection** (H9, H14, M10, M11, M30) -- Multiple rewriters/walkers exclude all children of `MemberAccessExpressionSyntax` instead of only the `Name` (right) side, failing to process the `Expression` (left) side.

4. **Name-based instead of symbol-based matching** (M7, M24, M25, M26, L3) -- Using string name matching instead of semantic model symbols leads to false positives/negatives with overloads and shadowing.

5. **Inconsistent error handling** (M8, H18) -- Mix of string-prefix error signaling and exceptions makes error handling fragile and unpredictable.
