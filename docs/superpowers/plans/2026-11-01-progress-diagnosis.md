# Progress diagnosis implementation plan

**Goal:** Replace task age warnings with conservative, persisted progress observations; retain Issue #1 until gameplay evidence exists.
**Architecture:** A game-independent clock tracks first observation, first sampled job, last change and eligible observation time. Game adapters supply progress samples and blockers without issuing jobs. Keep current C# 7.3 / net10.0 project and isolated repository.
**Spec:** User request dated 2026-11-01 and Issue #1.

## Constraints and review focus
- No game binaries, decompiled sources, saves or private indexes in GitHub.
- No forced tasks or modifications of work settings. English/Japanese parity.
- Sleeping/drafted workers, intentional pauses, observation gaps, old saves and partial scans must not become age-based warnings.
- Unknown diagnoses remain explicitly unknown; sampled jobs prove activity, not completed work.
- No v1.0 without gameplay, save/load, compatibility and measured performance evidence.

## Tasks
- [ ] Add console tests (no external test dependencies) for new task grace, actual progress reset, pauses, observation gaps, legacy migration, repeated notifications and tick rollback; run RED.
- [ ] Implement `ProgressClock.Observe(int now, float progress, bool working, bool eligible, int maxGap)` and `IsStalled(int threshold)`; run GREEN.
- [ ] Integrate serialized clocks, actual frame/unfinished/crop observations, job-target activity, confidence-aware diagnoses and category filtering. Preserve unobserved records on partial scans; measure scan milliseconds.
- [ ] Compile against the exact private 1.7.4985 DLLs; validate bilingual keys, run regression tests, review diff and document remaining unsupported causes.
- [ ] Commit and push branch through GitHub, open PR, run final CI; merge only after checks pass. Keep Issue #1 open and no release while runtime acceptance is incomplete.

## Execution ledger
Ruling: Execute inline without design approval round or subagents because the user supplied a detailed implementation scope and requested continuous implementation.
Baseline: package metadata and existing 18 translation keys pass. No behavioral tests existed. Previous A-level evidence applies to the previous DLL only.

Checkpoint: clock, observation adapters, bilingual UI, pure tests and packaging implemented. Exact-DLL build and 12 assertions pass. Scribe integration fails on stock CLR / game ObjectPool version mismatch. Initial RED was environment-blocked; gap-reset mutation subsequently verified regression sensitivity. Material/reservation completeness and gameplay acceptance remain open. Self-review used because user requested no subagents.

Follow-up: add likely construction-material and reservation diagnosis with five test-first assertions; cache counts per Def. RED: missing BlockerEvidence source. GREEN: 17 model assertions; exact-DLL build and real Scribe regression pass. Populated workload acceptance remains pending. No release claim.
