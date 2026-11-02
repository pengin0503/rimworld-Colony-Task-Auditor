# Colony Task Auditor Actionable Automation Implementation Plan
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`). This is a plan, NOT an implementation.
**Goal:** 原因に応じた確認付き改善操作、重要作業の個別監視、安全な一時作業支援。
**Architecture:** `TaskAuditComponent`の診断を事実情報源に、提案エンジンと変更権限を検証する実行層を独立。ModSettingsは全体許可、MapComponentは重要対象・一時操作・履歴を保存。
**Tech Stack:** C#, .NET 10, RimWorld 1.7 API, Harmony（既存依存のみ）, existing `Tests`/`Tests.Game`.
**Spec:** `docs/superpowers/specs/2026-11-02-actionable-automation-design.md`

## Global Constraints
- ユーザー申告で既存3MODのプレイは正常。既存の安全な検出・通知仕様を維持。
- Confirm既定・Autoは機能別に別途許可・部分スキャンではAuto禁止。
- ユーザーの手動変更や他MODの管理を尊重。危険・強制Job・全作業表の書換えは禁止。
- R2メタデータと実物DLLを参照し、未検証API使用を前提にしない。
- 日英翻訳、途中導入/削除、セーブ/ロードでの互換性を維持。

## Review Focus
- [ ] 何もする必要のない順番待ち・冬季・一時停止を誤って自動介入しない（Tasks 1, 3）。
- [ ] 材料が在庫にあっても禁止/未搬入なら「不足確定」と誤表示しない（Task 1）。
- [ ] 重要対象が消えた/別マップに移動した場合、古い作業に指示を出さない（Task 3）。
- [ ] Work Tab/WorkRolesが手動値を変更した場合に復元処理がそれを上書きしない（Task 4）。
- [ ] 危険・徴兵・医療・手動Jobや部分走査を優先のため中断しない（Task 4）。

### Task 1: アクション候補と根拠モデル
**Files:** Create `Source/TaskRemedyPlanner.cs`; Modify `Source/BlockerEvidence.cs`, `Source/TaskAuditor.cs` only as required; Test `Tests/Program.cs`.
- [ ] 原因・確信度・完了済み/一時停止/部分走査の入力から、実行可能/提案のみ/禁止を区別する失敗テストを追加。
- [ ] 事実・推定・ユーザー許可を分離した`TaskRemedy`モデルと純粋な候補生成ロジックを実装。
- [ ] 在庫・技能・作業種・到達性の情報を追加する場合も、1回の計算量と既存スキャン予算を維持。
- [ ] 全テスト/レビュー後、タスク単位コミット。

### Task 2: 対処プレビューと原因UI
**Files:** Modify `Source/TaskAuditUI.cs`, `Source/TaskAuditor.cs` / language XMLs; Test `Tests/Program.cs`, `Tests.Game/Program.cs`.
- [ ] 各原因に異なる解決メニューと「自動化できない理由」を表示する失敗テストを追加。
- [ ] 既存非モーダル一覧に詳細・不足材料数・作業可能者・操作プレビューを追加。
- [ ] Confirmで承認された時点で対象と条件を再検証。再検証失敗時に書換えなし。ローカライズとUIを確認してコミット。

### Task 3: 重要作業登録と個別閾値
**Files:** Create `Source/TaskWatchlist.cs`; Modify `Source/TaskAuditUI.cs`, `Source/TaskAuditor.cs`; Test `Tests/Program.cs`, `Tests.Game/Program.cs`.
- [ ] 重要フラグ・閾値・対象消滅・マップの違い・再ロードの失敗テストを追加。
- [ ] 作業またはBill固有IDとマップ情報を保存し、現在の監視レコードに対応付ける。重要対象だけ警告閾値を短縮。
- [ ] 完了/消失で登録解除、マップ切替と再ロードで誤再登録が起こらないことを確認。手動で選択された重要対象だけ初期通知する。
- [ ] 全テスト/レビュー後コミット。

### Task 4: 安全な一時作業支援
**Files:** Create `Source/TemporaryWorkAssistance.cs`, `Source/TaskActionState.cs`; Modify `Source/TaskAuditUI.cs` / `AuditorSettings` / translations; Test `Tests/Program.cs`, `Tests.Game/Program.cs`.
- [ ] 優先操作中に徴兵/危険/手動作業/他MODによる優先度変更が発生しても介入・上書きしない失敗テストを追加。
- [ ] 実物APIの `Verse.PriorityWork`、`RimWorld.Pawn_WorkSettings.GetPriority/SetPriority`、Jobの実行可否を照合して、対象限定のConfirmを実装。
- [ ] Autoでは重要監視・明示許可・クールダウン内・安全な対象に限定。恒久的な作業優先度変更より一時指示を優先し、元の値の条件付き復元（比較してから戻す）を徹底。
- [ ] WorkRoles/Work Tabなど優先度を制御する他MODがいる場合はAuto停止または検証済み互換経路だけ許可。例外時も安全に後始末。
- [ ] 変更履歴・セーブ/ロード・繰返し抑止を確認しコミット。

### Task 5: 統合検証・成果物
**Files:** Existing `Tests*`, `README.md`, `docs/VALIDATION.md`, `docs/RELEASE_NOTES.md`, `.github/workflows/*.yml`（必要時）。
- [ ] 既存・新規の自動テスト、Releaseビルド、翻訳とScribeの確認。
- [ ] R2・LinuxホストでAPI/Job/優先度の実体確認、必要なら通常ゲームで操作、セーブ/ロード、途中削除。
- [ ] 270以上のMODと大規模作業指示で基準版とのTPS・スキャン時間・GC割当の差を計測。
- [ ] PR、コードレビュー、CI、ドキュメント更新。未検証事項は合格扱いしない。検証できた変更だけマージする。

**検証コマンド（予定）:** `dotnet run --project Tests/Tests.csproj -c Release`; `dotnet build Source/ColonyTaskAuditor.csproj -c Release`; 実物DLL指定は `-p:RimWorldManaged=/path/to/RimWorld`。
