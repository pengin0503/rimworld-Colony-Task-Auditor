# Validation / 検証記録

## Verified / 確認済み

- **Game build:** RimWorld 1.7.4985 rev1269, Linux x86_64 WaylithClr (private game snapshot).
- **Source commit tested:** `46ad2d3197c409d1fc2db287b6d3a764c4a6ffb0`.
- **Public-reference build:** `dotnet build` with .NET SDK 10 and `Krafs.Rimworld.Ref 1.7.4985-unstable` succeeded (0 warnings and 0 errors).
- **Real game-code load (A):** `status=passed`, `exitCode=0`, `reasonCodes=[]` for ColonyTaskAuditor.
- **Evidence:** https://github.com/pengin0503/RimWorld-for-mod-test/actions/runs/41275665138
- **Method:** isolated A-level comparison via `RimWorld-for-mod-test`. Original game content stayed in a private R2-backed validation snapshot; no game DLL was published.

## Not yet verified / 未確認

- This host **does not run the normal playable RimWorld application**; no in-game UI interaction, time-dependent job behavior or save/reload verification was performed.
- Progress-aware stall diagnosis, identified blockers, gameplay/UI/save-load verification and compatibility/performance tests.
- **A=passed does not mean that the mod is a completed, gameplay-verified release.** Avoid claiming v1.0 readiness until the remaining items are independently validated.

日本語: Linux版のゲーム本体（1.7.4985 rev1269）でAレベルのMOD読み込み検証に合格しました。通常のゲーム起動・画面操作・セーブとロード・長時間プレイでの動作は未検証です。完成版として扱わないでください。


## 2026-11-01 progress implementation (development)

- SDK 10.0.111 / .NET 10.0.11, Linux x86_64; exact private 1.7.4985 rev1269 assemblies: Release build passes, 0 warnings/errors.
- `dotnet run --project Tests/Tests.csproj -c Release`: 12 assertions pass. New-task grace, progress changes, active jobs, intentional pauses/resume, missed observations, legacy baseline, tick rollback, primitive-state continuity and invalid numeric samples are exercised.
- Regression sensitivity: removing the observation-gap reset makes `missed scans reset uncertain elapsed time` fail; restoring it returns the suite to green. The initial pre-implementation test execution was blocked by the missing SDK, so a strict test-first RED/GREEN cycle was not achieved for the initial clock implementation.
- Model microbenchmark: 2,000 clocks x 1,000 observations (2,000,000 calls), 11.25 ms in one Release run. This excludes maps, reachability, game jobs, GUI and other mods; it is **not** a scan/TPS measurement or evidence of an optimization over baseline.
- English/Japanese key parity and development ZIP structure pass (About, own DLL, both language files, README). No game DLLs are packaged.
- Self-review only, per request to avoid subagents. Fixed transactionality of observations, frame/blueprint discovery, disabled monitors during partial scans and UI header overlap.

### Runtime attempt / 実行検証の試行

Initial direct Scribe attempts failed. A game parser preflight exposed a missing `com.rlabrecque.steamworks.net` dependency; the game logger had masked it with an ObjectPool version error. After acquiring the matching private R2 dependency and supplying real default `PrefsData` for the record-only fixture, the Scribe record round trip and progress continuity pass. No fake serializer or assembly-version redirect was used. This is not full engine initialization or a whole-map save/load test.

The existing test host's `bounded-tick.v1` accepts only its registered fixture observations. It does not currently provide Task Auditor job-progress/save-load acceptance. At the initial local checkpoint, no host map/tick test had run. The final host evidence below supersedes that initial scope; the original A evidence at the top remains historical.

### Remaining release blockers / 正式版までの残作業

- Accurate material/reservation diagnostics and full per-task worker/skill mapping remain incomplete. Unknown reasons are explicitly labeled. Reachability is a bounded sample with likely confidence, never a confirmed defect.
- Bill progress is sampled through repeat-count changes and active bill jobs; unlimited bills completing between scans can be missed. Farm counts are aggregate observations; designation HP is not universal work progress. First discovery is not the actual creation time of pre-existing tasks.
- Crops under temporary weather conditions, individual job priorities/schedules and all modded task types require gameplay acceptance. Capped scans retain uncertain history and can under-detect stalls rather than asserting absent progress.
- Full-map serialization/removal, real job execution, UI layout, 200+/270+ MOD compatibility, large designation/farm scan timing and TPS overhead are unverified. No v1.0 release; Issue #1 stays open.

日本語: 今回は進捗追跡・原因の確信度表示・種類別フィルターと回帰テストを追加しました。実物DLL参照のビルドと12項目のモデルテストは合格しました。保存処理の実行試験はCLR依存の不一致で失敗しており、マップの保存・読み込み、通常プレイ、有限Tick、MOD互換性、実スキャン時間・TPSは未確認です。材料・予約の完全診断や全作業種別の進捗把握も未完了のため、正式版にはしません。

## Final private-runtime evidence (2026-11-01)

- [Pinned private-host run](https://github.com/pengin0503/RimWorld-for-mod-test/actions/runs/41278778403) completed successfully. Only safe rebuilt summaries were printed; raw game files, saves and private logs were not published.
- Fresh qualified **A=passed**, exit 0, no reason codes, for the source revision in [runtime-smoke.json](evidence/2026-11-01-runtime-smoke.json).
- Generated-map finite-tick smoke: actual game tick and independent registered `fixture.counter` both equal **0, 60, 600, 12,000**. **D stage=completed**, but **overall=incomplete** with `experimental`, `dNotQualified`, `mapNotQualified`. The host CI success means these limited expectations were met; it is not promotion to qualified D or game feature acceptance. No new errors/warnings were reported by that comparison.
- The registered observer measures its fixture counter, not our work diagnosis, feed accuracy, apparel effects or scan timing. The fixture map and Core/Japanese conditions do not represent a populated or 270+ MOD colony. No TPS claim is made.
- Local model benchmark conditions: Linux x86_64, AMD EPYC 9V74, 9 logical CPUs exposed, SDK 10.0.111 / .NET 10.0.11, Release, single-run measurements with game API/GUI/pathfinding excluded. No before/after optimization claim.

### Primitive Scribe integration

`dotnet run --project Tests.Game/Tests.Game.csproj -c Release -p:RimWorldManaged=<matching-private-game-dir> -p:GameExtraManaged=<matching-private-extra-dir>` exercises the real game Scribe under stock .NET for isolated primitive data. The extra directory contains only matching R2 ObjectPool and Steamworks managed dependencies. The fixture supplies the game's real default `PrefsData` because normal engine startup is absent. These files are test inputs only and are never committed or packaged.

**PASS:** audit record fields, notification state, eligible ticks and post-load progress continuity. This does not exercise a whole serialized map or completed jobs. Static index comparison: 119 member signatures and 69 types matched; 151 external-framework references remain outside the indexed assembly set, with no indexed game-reference mismatch.

**Still blocked for v1.0:** normal populated gameplay, full-map save/load and installation/removal, actual task/feeding/wear acceptance, real outfit-manager/race/DLC interactions, 270+ MOD compatibility and actual large-map scan/TPS measurement. Earlier implementation gaps and scenario limitations remain; Issue #1 is open and no v1.0 release is created.

日本語: 最新コミットで実ゲーム読み込みAは合格し、実ゲームTickと独立fixtureが12,000 Tickまで一致しました。ただしD・マップ生成は未適格化で総合判定はincompleteです。実物Scribeによる限定的なデータ往復も合格しましたが、通常プレイやマップ全体の保存・読み込み、実際の業務動作、270以上のMOD環境・実TPSの検証にはなりません。Issue #1と正式版の保留を維持します。

## Blocker follow-up

Adds likely frame-material delivery/stock-shortage evidence and reservation evidence. A reservation alone is never classified as a blocker: available workers must also fail reservation. Spawned non-forbidden material counts are cached by Def for the scan; inventory and in-transit contents remain unverified and are explicitly labeled. These diagnostics do not alter orders or reservations.

17 pure assertions pass (five added), exact private 1.7.4985 DLL Release build has zero warnings/errors, real Scribe record/continuity regression passes, and bilingual key checks pass. The earlier A/12,000-tick result applies to the earlier production revision, not automatically this follow-up. New populated-game acceptance is pending. Other implementation and release blockers above remain open.

## Implementation continuation: event and active-work tracking

Completed bill iterations are observed by one Harmony postfix on Bill_Production.Notify_IterationCompleted, covering unlimited bills that complete between scans. Harmony identity was verified in the private index: brrainz.harmony / 2009463077. No game or Harmony binary is committed. Actual job workLeft, frame work, unfinished work and designation target damage are sampled for active workers every 250 ticks; this is not an all-object scan. Completion resets the persisted last-progress clock without requiring a repeat-count change.

Added recipe/construction skill diagnosis, work-type resolution from WorkGiver definitions, bounded recipe-ingredient absence checks, blueprint materials, initial map-load refresh, per-category cleanup during partial scans, full virtualized list and explicit timing display. Pre-installation creation time remains unknowable; the UI labels first observation correctly. Modded completions that bypass the vanilla method and special job implementations can remain unknown.

Build/static checks and existing focused regression checks pass locally. New populated gameplay, Harmony execution, full-map save/removal and 270+ MOD performance remain unverified. Prior A/tick evidence is historical for previous production bytes. Test effort is limited to changed behavior and existing checks per the user's latest priority; no new broad test infrastructure is added.

## v1.0.0 release checks (2026-11-01)

Fixes made during release review (code review only; no new gameplay run):

- Stall timing used to reset to zero whenever every worker was asleep, drafted or downed at a scan. With a shared sleep schedule this happened every night, so the 1–10 day threshold could never be reached. Temporary worker absence now freezes the timer and does not reset it. The interval spanning the wake-up is not counted. Intentional pauses still reset it.
- Active-work sampling no longer overwrites the scan-level progress metric with a different value (hit points versus repeat count, fullness or crop counts). That overwrite produced false progress. Live samples are compared only with earlier live samples. Jobs on a workbench (refuel/repair) no longer count as bill work, and animal targets count only for gathering jobs.
- Install blueprints are no longer diagnosed as lacking construction materials or skill.
- Active-work sampling has its own exception guard. The settings list tolerates a missing map component. Designation labels fall back to the def name.

Checks performed:

- 22 model assertions pass (four new hold/resume assertions).
- `validate_package.py` passes.
- Release build has 0 warnings and 0 errors against the exact private 1.7.4985 rev1269 Assembly-CSharp/Waylith/UnityStubs.
- Release build with `Krafs.Rimworld.Ref 1.7.4985-unstable` also has 0 warnings and 0 errors.
- Static reference check of the public-reference DLL against the audited index: 168 members and 95 types matched, 0 mismatches. The 232 unverified references are BCL only (System.Runtime/Collections/Linq).
- `Bill_ProductionWithUft.Notify_IterationCompleted` calls the base `Bill_Production` method (IL), so the completion postfix also covers UFT bills.

Still unverified, and not claimed: normal populated gameplay, actual Harmony patch application in the running game, full-map save/load and removal, large-map scan timing/TPS, and compatibility with large mod lists. The earlier A-level and 12,000-tick evidence was produced on earlier DLL bytes.

## v1.0.1 (2026-11-01)

### Issues resolved

| Issue | Change |
|---|---|
| #8 | All construction costs are inspected; any shortage outranks "not yet delivered", independent of material order. |
| #9 | Capped categories rotate through their entries. Unseen records keep their clocks instead of being reset; the allowed observation gap is widened by the number of rotation windows. |
| #10 | Crop and empty-cell counts use an increasing-only progress mode: more pending work is not progress. |
| #11 | Activity is matched to the job that performs the task (designation job, FinishFrame/HaulToContainer/PlaceNoCostFrame, DoBill, Sow, Harvest, Milk/Shear by resource). |
| #12 | Per-scan budgets and rotating cursors for bills (1,500), zone cells (40,000 in total) and animals (1,000). Unfinished products are found by def instead of scanning every Thing. |
| #13 | Japanese and English About.xml descriptions carry the same scope. |
| #14 | Player mechanoids count as workers; mechanoid skill uses `mechFixedSkillLevel`, as vanilla `SkillRequirement` does. |
| #16 | Backlog hold: untouched entries wait without ageing while the same work type (or workbench) is busy elsewhere, unless the cause is confirmed. Interior mining cells are labeled and held. |
| #17 | Each scanned entry is isolated; failures are skipped, counted, shown and warned once per category and exception type. |
| #18 | Player-forbidden targets are treated as an intentional pause. |
| #19 | Jumping from the settings list closes the settings dialog. A non-modal list window was added. |
| #20 | No workers on the map pauses judgement instead of reporting "no worker (confirmed)". |
| #21 | Bills queued behind the active bill of an autonomous workbench are treated as paused. |
| #22 | Sowing diagnosis checks the plant's `sowMinSkill` and cells blocked by items. |

Also added: a standard alert for stalled tasks that opens the list window, and an alert on/off setting.

### Found by the in-game probe and fixed

For a Blueprint_Build, `Thing.Stuff` is null (the stuff lives in `stuffToUse`). The material check called `CostListAdjusted(null)`, which logged "Cannot get AdjustedCostList for Wall with null Stuff." on every scan for every stuffable blueprint. This was present since v1.0.0. The check now uses `IConstructible.TotalMaterialCost()`.

### Optimisation

- Non-bill tasks share per-scan worker lists keyed by work type.
- The list UI rebuilds its sorted view only when the scan, filter or threshold changes.

### Checks performed

- 29 model assertions pass, including increasing-only clocks, holds and multi-material evidence.
- `validate_package.py` passes (57 keys).
- Release build has 0 warnings and 0 errors against the exact private 1.7.4985 rev1269 DLLs, and also against `Krafs.Rimworld.Ref`.
- Static reference check: 220 members and 114 types matched, 0 mismatches. Unverified references are BCL only.
- Real game, Linux WaylithClr 1.7.4985 rev1269, private host `RimWorld-for-mod-test`. The probe mod is private and was not published.
  - **A+B (Japanese, with Harmony 2009463077): passed.** The postfix `BillProgressHooks.Completed` is registered on `Bill_Production.Notify_IterationCompleted`, and no new errors or warnings appeared.
  - **D bounded tick (2,400 ticks, Crashlanded, generated map): D stage completed. Overall status is incomplete (`dNotQualified`, `mapNotQualified`), as the host always reports.** A plasteel-wall blueprint with no plasteel on the map was diagnosed `CTA_MaterialMissing`. The probe called scans 300 ticks apart:
    - while workers were available, eligible time rose 300 → 600;
    - with every colonist drafted, the reason was `CTA_Waiting` and eligible time stayed 600 (held, not reset);
    - after undrafting it stayed 600 for the wake-up interval and then rose to 900.
    - No new errors appeared during ticks.
  - `Alert_TaskAuditOverdue.GetReport()` and `Window_TaskAudit` construct without exceptions in the real game.

Still unverified: long normal gameplay, sleep (it uses the same hold path as drafting but was not driven directly), mechanoid workers, full-map save/load and removal, rendering of the alert and window, large-map scan time/TPS and large mod lists.

Review follow-up (PR #23):
- Growing zones: the rotation window is decided before any zone is observed. The allowed gap is derived from the number of scans of one full budgeted rotation plus a boundary margin, not from this scan's count, so zones of different sizes are covered.
- The alert rechecks each cached overdue record against its current clock, so tasks that resumed since the scan are not reported. The map switch uses the same check.
- Thing-targeted jobs are also indexed by cell, and the 250-tick sampler matches cell-based orders. Re-running the bounded real-game probe with a mining designation next to a miner: the record was `CTA_Working` with a first-work tick at 1000 and 1300, `CTA_Waiting` while drafted, and `CTA_Working` again after undrafting. The other probe results were unchanged.

## 2026-11-02 actionable automation

- Public-reference Release mod and Tests.Game fixture compilation: zero warnings/errors.
- Pure regression suite: 29 progress assertions, 14 remedy-policy scenarios, 7 watchlist scenarios, 18 temporary-support safety scenarios. RED→GREEN observed for new models and the component-order freshness regression.
- R2 1.7.4985 metadata checked for PriorityWork.Set/Clear, private pawn/prioritizeTick fields, Pawn_WorkSettings.GetPriority/SetPriority and related recipe/bill types. Public reference compilation verifies signatures, not gameplay.
- The existing Scribe fixture now covers watch identities and new settings; it was compiled but **not executed** against real game DLLs in this session.
- Implementation uses native PriorityWork only; there is no forced Job assignment and no work-table priority mutation/restoration. CAS clearing checks giver/cell/timestamp and Harmony observes external Set/Clear to preserve identical manual writes within the same tick. If observation is unavailable, support is disabled.
- Confirmed Ready is published only after a targeted executable vanilla work-giver proof for an idle enabled safe colonist. Known blockers and held queues are not promoted. Auto requires this published fact, a watch and target opt-in.
- Extra proof budget: 64 per detailed scan, at most 4 watched tasks and 8 worker candidates each; lightweight resolution avoids repeating material scans. Watch lookup is indexed. Native 250/5000-tick sampling/scan cadence is retained. Automatic execution accepts a scan up to 250 ticks old and rechecks live conditions, avoiding MapComponent-order dependence.
- Inline whole-branch review used per instruction to avoid subagents.
- Unverified: actual Harmony patch execution, in-game priority scheduling/expiry, normal UI interactions, whole-map Scribe reference resolution and install/removal, Work Tab/WorkRoles and 270+ MOD compatibility, TPS/GC comparison. Earlier user play validation covers earlier features, not these additions.
- No private game files or credentials included in CI or packages.


## Review remediation (2026-11-02, branch: feat/remaining-review-fixes-20261102)
- Scope: #25, #26, #27, #29 and low-risk portions of #30. #28 intentionally excluded.
- Registered watched tasks are now selected through a persisted round-robin cursor (at most four per scan; original proof budget 64 maintained). Proof, notification and Auto all share the same scan batch.
- AssistanceMode=Off no longer removes important-watch alert thresholds; Confirm remedy details update on newer audit snapshots without invoking full scan per UI frame.
- Cause-specific UI navigation selects a real stock item/recipe bench/possible worker if still spawned on the map; it does not change Job priorities or forbidden flags.
- Task detail lookup uses the existing ID index; material stock counts use one bounded pass; list UI draws only visible rows.
- Public CI success: 18 remedy scenarios, 10 watchlist scenarios, 18 temporary-work safety scenarios, 29 progress assertions; Release source and game-fixture **compile only**, metadata/translation/package checks. Example run: https://github.com/pengin0503/rimworld-Colony-Task-Auditor/actions/runs/41384559159
- **Not verified:** native UI interaction, full map Scribe load, PriorityWork runtime hooks, TPS/GC before-after and 270+ MODs. Keep #7 and #30 for real performance acceptance. #28 untouched.

## Cause-specific confirmed bill resume (2026-11-02)
- A suspended production bill with current paused diagnosis can be resumed only from the remedy UI by an explicit confirmation. The callback reruns the audit and checks the same Bill instance, status, current map, and non-partial scan immediately before clearing `suspended`.
- The pure model's five safety cases were observed RED→GREEN in GitHub Actions; includes refusal without player confirmation, wrong reason or incomplete audit.
- No automatic bill resume, no PriorityWork API calls, no forced Job and no changes to the reserved #28 ownership logic.
- Actual game UI interaction and full save/load still require #7 acceptance. CI compile does not prove gameplay.

## Review remediation #28 (2026-11-02, branch: ccr-df0edeac-pkmfta)
Baseline main `1e05247`. Evidence: R2 `Game-Linux/Version.txt` = `1.7.4985 rev1269`; full ILSpy decompilation of `Assembly-CSharp.dll` searched for every `PriorityWork` use. Nothing from the game is committed or packaged.

### Vanilla call sites (the original hypothesis is confirmed)
- `Set`: only `Pawn_JobTracker.TryTakeOrderedJobPrioritizedWork` (an ordered "prioritize" job).
- `Clear`: `PriorityWork.IsPrioritized` getter after `Timeout`=30000 ticks; `JobGiver_Work.TryIssueJobPackage` (emergency branch) when no prioritized job remains; `Pawn_MindState.Reset`; `ThingCountTracker.InterruptEnroutePawns`; `ClearPrioritizedWorkAndJobQueue` from the `Drafted` setter, `LordToil_Ritual`, `JobDriver_EnterPortal` and the player's "clear prioritized work" gizmo.
- Cause: one postfix on Set and Clear withdrew Auto permission for every non-owned write, so vanilla's natural timeout and completion Clears were treated as manual.

### Fix
- Clear prefix reads the pre-clear state (timeout). Void prefixes + finalizers on JobGiver_Work (emergency only), Pawn_MindState.Reset, the Drafted setter and InterruptEnroutePawns attribute a Clear to that caller for the matching pawn.
- Natural/engine sources release the request and keep Auto permission. Every non-owned Set and every unidentified Clear (gizmo, other mods, ritual, portal, ordinary job search) is External: released, permission withdrawn. Nothing is rewritten or restored. The source is shown, localized, in the action history.

### Checks run
- `Tests`: 29 attribution assertions (natural timeout/no-work/reset/draft/target change keep permission; unidentified Clear and any non-owned Set — including same-tick/same-value and inside engine callers — withdraw it; EN/JA keys).
- Real-game fixture executed locally against the R2 DLLs: audit record/watch/settings Scribe round trip (its watch/settings load previously failed outside a running game in `ScribeExtractor`'s BackCompatibility lookup and had only been compiled; it now uses the same LoadingVars pass directly) and the PriorityWork API/caller signatures (Timeout literal 30000).
- Scratch probe (not committed) with Lib.Harmony 2.4.3 and 3.0.0-rc.4 on .NET 10 against the real DLL: all six patches apply via the mod's reflection installer; patched `Clear` runs with private-field injection; a frame is popped by the finalizer even when the patched `Pawn_MindState.Reset` throws. Lib.Harmony 2.4.1 refuses CoreCLR 10.
- Release build against public references and the real 1.7.4985 DLLs; public `Tests.Game` compile; metadata/translation/package checks.
- Review round 1 fixed: NoWork narrowed to the emergency giver; thread-static context; no duplicate history when our own `IsPrioritized` check runs vanilla's timeout Clear. Round 2: hot-path cost is one list push/pop per job search; no new save fields.
- **Not verified:** in-game priority orders end to end (actual think-tree runs, gizmo, draft, rituals), the Harmony version shipped by the Harmony mod for 1.7, other priority mods, 270+ MODs, TPS. Remains under #7.
