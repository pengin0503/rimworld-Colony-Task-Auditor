# Colony Task Auditor Review Remediation Implementation Plan
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`). **This plan is not implementation.**
**Goal:** Close issues #25–#30 with safe, fair priority assistance, current cause-specific actions and measured lower overhead.
**Architecture:** Separate manual-write ownership logic, fair watch scheduling, watch-vs-assist mode semantics, UI snapshot refresh and safe non-writing remedies. Optimize scanner and UI only after baseline performance and equivalence tests.
**Tech Stack:** C#, .NET 10, RimWorld 1.7 `PriorityWork` and Harmony, GitHub Actions/public reference assemblies.
**Spec:** `docs/superpowers/specs/2026-11-02-review-remediation-design.md`

## Global Constraints
- Initial baseline `726c2c8206d4cfc5c0038d5de41262ebb16bf103`; existing 250-tick sampler / 5000-tick detailed scan, safety proof and partial handling preserved.
- Off/Confirm/Auto modes preserved; Watch and AutoAssist distinct; real manual/other-mod changes win in same-tick cases.
- Code tests/CI allowed frequently. Do not embed game DLL/credentials; `RimWorld-for-mod-test` optional.
- Maintain JA/EN equivalence, existing Scribe data shape, no forced Job, no permanent work-priority edits, no unwarranted release claims.
- Issue #7 tracks real-game acceptance; this plan does not change its status without actual evidence.

## Review focus: failures to pin to tests
- [ ] Fifth watch receives Ready proof and assistance eligibility with first four still watched (#25).
- [ ] Manual same-tick, same-value Set/Clear cannot be cleared by ownership logic (#28).
- [ ] Vanilla natural end is distinguishable or at least safe and transparently recorded (#28).
- [ ] Assistance OFF still monitors important tasks without executing jobs (#26).
- [ ] Confirm UI updates after scan but does not re-run a full scan every GUI frame (#29).

---
### Task 1: Correct priority ownership detection and reporting (#28)
**Files:** Modify `Source/TaskPriorityHooks.cs`, `Source/TaskActionState.cs`, possibly `Source/TemporaryWorkAssistance.cs`; Test `Tests/AssistanceTests.cs`, `Tests.Game/Program.cs`.
**Interfaces:** Preserve `TaskPriorityHooks.Set/Clear/ExternalWrite` and `TaskActionState.ExternalPriorityWrite` outwardly until migration reviewed; create a pure classification input struct for the source/cause and existing expected state, with an explicit safe Uncertain outcome.
- [ ] Trace vanilla `PriorityWork.Set`/`Clear` call sites in the audited 1.7 DLL (and Harmony-related semantics) to determine whether natural end/expiration invokes patched methods. Record evidence, do not assume.
- [ ] Add failing test: ordinary task completes and native Clear runs; differs from explicit manual Clear and mod-owned Clear if the API supports discrimination.
- [ ] Add failing tests: same-tick same-value manual Set/Clear, other-MOD overwrite, draft, game load, mod uninstalled with stored action; when ambiguous preserve manual order and disable writes.
- [ ] Apply minimal classification/hook fix that does not grant Auto without evidence; record loss of permission reasons without per-tick spam.
- [ ] Run tests, compile and review; commit with `Fixes #28` only if original cause proved.

### Task 2: Fair round-robin for watched tasks (#25)
**Files:** Modify `Source/TemporaryWorkAssistance.cs`, `Source/TaskActionState.cs`, `Source/TaskAuditor.cs`; Test `Tests/WatchTests.cs`, `Tests/AssistanceTests.cs`.
**Interfaces:** `TaskActionState` owns saved watch iteration cursor (or stable ID successor). `PublishReadyFacts` and `TickActions` share one bounded picker; keep per-scan `ProofBudget` = 64 and max four watched tasks / eight workers.
- [ ] Failing test: 5 and 100 watched targets, first 4 permanently present; every target becomes eligible for proof/Auto inspection within finite rounds.
- [ ] Add tests for deletion/prune/reorder, save/load, partial category scan, stale/invalid `WorkGiver`, no Ready publication without live proof.
- [ ] Implement bounded rotating selection and save cursor; do not spend proof budget repeatedly in one Tick.
- [ ] Run targeted/full tests, build and review. Commit `Fixes #25`.

### Task 3: Separate watch warnings from assistance Off (#26)
**Files:** Modify `Source/TaskWatchlist.cs`, `Source/TaskAuditor.cs`, `Source/TaskAutomationSettingsUI.cs`; Test `Tests/WatchTests.cs`, `Tests.Game/Program.cs`.
**Interfaces:** `TaskWatchlist.Threshold` takes watch-monitoring eligibility rather than `AssistanceMode`, or removes the mode argument while preserving compatibility within repo.
- [ ] Failing test: existing important watch with AssistanceMode=Off still uses its shorter threshold; no auto requests.
- [ ] Failing test: watch monitoring explicitly disabled (if separately added) falls back to ordinary threshold; old-save migration remains safe.
- [ ] Implement distinct watch vs assistance setting and UI; update translations. Commit after tests/CI with `Fixes #26`.

### Task 4: Fresh remedy details in Confirm mode (#29)
**Files:** Modify `Source/TaskRemedyUI.cs`, `Source/TaskDetails.cs`; Test `Tests/RemedyTests.cs`, `Tests.Game/Program.cs`.
- [ ] Failing test: audit scan changes record state or task disappears while Confirm window remains open; old preview invalidated.
- [ ] Refresh from already scanned `audit.Records` when `LastScanTick` changes, not by calling full `audit.Scan` from frame draw.
- [ ] Keep live revalidation at Execute and manual refresh button. Validate JA/EN strings, build and commit `Fixes #29`.

### Task 5: Add safe cause-specific remedy actions (#27)
**Files:** Modify `Source/TaskRemedyPlanner.cs`, `Source/TaskRemedyUI.cs`, `Source/TaskDetails.cs`, optionally `Source/TaskAuditor.cs`; Test `Tests/RemedyTests.cs`, `Tests.Game/Program.cs`.
**Interfaces:** Distinct action types: `Navigate` (no world write), `Confirm` (explicit preview + live validation), `Auto` (only if separately proven safe/permitted); do not silently reinterpret existing `CanExecute`.
- [ ] Define for each root cause the safe and meaningful action: material stock or recipe location, worker selection and work tab, unreachable/forbidden/pause status explanation. Confirm there is a real target before presenting a click.
- [ ] Failing tests: materials absent, incorrect skills, no workers, forbidden, unreachable, paused bill, duplicate/suspended job; no action may clear the forbidden flag or change work priorities without explicit authorization.
- [ ] Build cause-specific navigation, optionally verified confirmation actions for explicitly safe operations; keep all unsupported causes advisory only. Do **not** add arbitrary direct Job dispatch to simulate completion.
- [ ] Translation and in-game UI tests, regression and review. Commit `Fixes #27`.

### Task 6: Evidence-driven scan/UI optimizations (#30)
**Files:** Modify `Source/TaskAuditor.cs`, `Source/TaskDetails.cs`, `Source/TaskAuditUI.cs` only where benchmarks support; Test `Tests/Program.cs`, `Tests/WatchTests.cs`; Document `docs/VALIDATION.md`.
- [ ] Record baseline according to `docs/review/2026-11-02-remediation-benchmark-matrix.md`. Separate pure model ms from live TPS/GC.
- [ ] Add equivalence tests: partial rotating scans, task removal, ProgressClock eligible time, material blockers, stable sorting, watch/notification exactness.
- [ ] Reuse existing `FindRecord` and stable indexes in detail window; virtualize scroll loop to visible start/end; avoid unnecessary record deep-copy/re-sort only where safe.
- [ ] Benchmark 100/1k/5k/10k tasks, UI open/closed, watch count 0/100; revert any optimization that changes diagnosis or increases false positives.
- [ ] CI/test/review; commit `Fixes #30` only when measured and equivalent.

### Task 7: Final CI, docs, readiness
**Files:** `docs/VALIDATION.md`, README/RELEASE_NOTES, translations, existing `.github/workflows/build.yml` only if needed.
- [ ] Run existing tests + new regressions, public-reference Release build, metadata/translation/package validation and Tests.Game **compile-only**.
- [ ] Validate with real RimWorld when feasible: PriorityWork Set/Clear, confirmation, watch fairness, save/load. Do not mislabel A-level load as gameplay. If unavailable, record under existing #7.
- [ ] Run performance comparison, static code review, PR CI, merge. Close each Issue only after evidence satisfies acceptance criteria.
- [ ] Report SHAs/PR, result per Issue, tests vs actual gameplay, performance before/after vs not measured. No release tag unless requested.

**CI commands:** `python3 tools/validate_package.py`; `dotnet run --project Tests/Tests.csproj -c Release`; `dotnet restore Source/ColonyTaskAuditor.csproj`; `dotnet build Source/ColonyTaskAuditor.csproj -c Release --no-restore`; `dotnet build Tests.Game/Tests.Game.csproj -c Release`; `python3 tools/package_dev.py`.
