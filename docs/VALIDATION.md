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

`Tests.Game` attempts a real Scribe primitive-record round trip. Execution **fails before successful load completion** under the stock .NET CLR: the game logging dependency requests `Microsoft.Extensions.ObjectPool, Version=11.0.0.0`, while the game relies on WaylithClr lax assembly binding. The R2-supplied ObjectPool assembly does not resolve this with the stock CLR. No fake serializer or version redirect was used to turn this into a pass. Run the fixture in the qualified game-runtime environment before accepting save/load.

The existing test host's `bounded-tick.v1` accepts only its registered fixture observations. It does not currently provide Task Auditor job-progress/save-load acceptance. This session did not initialize its full private WaylithClr runtime, generate a map, execute finite game ticks or repeat A-level loading for the new DLL. Earlier A evidence above is historical only.

### Remaining release blockers / 正式版までの残作業

- Accurate material/reservation diagnostics and full per-task worker/skill mapping remain incomplete. Unknown reasons are explicitly labeled. Reachability is a bounded sample with likely confidence, never a confirmed defect.
- Bill progress is sampled through repeat-count changes and active bill jobs; unlimited bills completing between scans can be missed. Farm counts are aggregate observations; designation HP is not universal work progress. First discovery is not the actual creation time of pre-existing tasks.
- Crops under temporary weather conditions, individual job priorities/schedules and all modded task types require gameplay acceptance. Capped scans retain uncertain history and can under-detect stalls rather than asserting absent progress.
- Full-map serialization/removal, real job execution, UI layout, 200+/270+ MOD compatibility, large designation/farm scan timing and TPS overhead are unverified. No v1.0 release; Issue #1 stays open.

日本語: 今回は進捗追跡・原因の確信度表示・種類別フィルターと回帰テストを追加しました。実物DLL参照のビルドと12項目のモデルテストは合格しました。保存処理の実行試験はCLR依存の不一致で失敗しており、マップの保存・読み込み、通常プレイ、有限Tick、MOD互換性、実スキャン時間・TPSは未確認です。材料・予約の完全診断や全作業種別の進捗把握も未完了のため、正式版にはしません。
