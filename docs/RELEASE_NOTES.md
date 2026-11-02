# Colony Task Auditor v1.1.0 — RimWorld 1.7

## 日本語

**新機能**
- 原因別の解決案、材料の在庫・製造台・作業者診断と対応する対象への直接移動。
- 意図的に停止した製造指示の再開と、確認済みの禁止対象1件の解除。どちらも操作直前の再スキャンとプレイヤーの確認を必要とし、無断変更はしない。
- 重要作業の登録、個別警告閾値・絞り込み、最大4件ずつの公平な巡回監視。
- 対象別に許可する期限付きのバニラ優先依頼。デフォルトは確認付きで、自動モードは明示許可制。作業表の数値変更やJob強制発行は行わない。

**修正・改善**
- PriorityWorkのバニラによる自然終了と、プレイヤー・他MODの書き込みを区別。手動指示を優先し、不確かな場合は自動支援を停止。
- 重要作業が5件以上あっても後続の監視が飢餓しないよう修正。支援OFF時も監視側の警告閾値を保持。
- 確認付きモードの診断詳細を最新スキャンに追従させ、画面外行の描画を省略。
- スキャンの一時辞書・集合の再利用、毎Jobの配列生成・重複材料集計・不要な再ソートを削減。進捗の保存と検出結果を維持。

**検証範囲**
- 公開GitHub Actionsでモデル・回帰テスト、Releaseビルド、翻訳・配布構成を検証。
- 1.1.0の新機能の通常ゲーム操作、Harmonyの実動作、完全なセーブ・ロード、270以上のMODでの互換性、ゲームTPS/GCの比較は未検証。

## English

**Added**
- Cause-specific remedies with materials, available benches, worker diagnostics and navigation to relevant live targets.
- Explicitly confirmed resumption of a paused bill and unforbidding of one verified target only, with fresh scans immediately before each change.
- Important-task watches with individual warning thresholds, filters and bounded fair rotation across registrations.
- Separately authorized, time-limited native priority requests. Confirmation is the default; Auto is opt-in per watched target. No forced jobs or permanent work-table priority changes.

**Fixed and improved**
- Distinguished vanilla PriorityWork natural completion from manual and other-mod writes; ambiguous changes stop automation.
- Prevented watches beyond the first four from starving; watch alerts continue when assistance is Off.
- Refreshed confirmation-mode details on new scans and drew only visible rows.
- Reused scan scratch collections, avoided per-job target arrays, and reduced duplicate material counting and unnecessary sorting.

**Verification**
- Public GitHub Actions regression tests, Release compilation, translation and package validation passed.
- Normal in-game 1.1.0 behavior, runtime Harmony interaction, full save/reload, 270+ mod compatibility and actual TPS/GC improvement are **not verified**.

**Dependency:** Harmony (`brrainz.harmony`) must load first.

---
Colony Task Auditor v1.0.1 for RimWorld 1.7 (built against 1.7.4985). Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`).

## 日本語

**追加**
- 停滞中の作業を画面右のアラートで表示します。クリックで、マップを見ながら使える一覧ウィンドウを開きます。
- プレイヤーのメカノイドを作業者として扱います。

**修正**
- 素材を選ぶ建築（壁など）のブループリントがあると、スキャンのたびにエラーログが出ていました（v1.0.0から）。
- 作業として数えるのは、その作業を行うJobだけにしました（燃料補給・修理・運搬などは数えない）。
- 熟した作物や空きセルが増えても進捗とみなさないようにしました。
- 大規模な採掘・建築の順番待ち、採掘範囲内側の未到達セル、禁止された対象、自律作業台の順番待ち、作業者が誰もいないマップを、停滞として扱わないようにしました。
- 材料はすべて確認してから判定し、播種は必要スキルと物で塞がれたセルも診断します。
- 1件の例外でスキャン全体が止まらないようにし、製造指示・区画・家畜にも処理上限と巡回を設けました。
- 一覧から位置へ移動するときに設定画面を閉じます。

**確認した範囲**: 実ゲーム（Linux、1.7.4985 rev1269）で次を確認しました。長時間の通常プレイは未確認です。
- Harmonyパッチの登録（A+B 検証 passed）。
- 2,400 Tickの有限実行で、作業者不在の間は停滞時間を保持し、解除後に再開すること。
- 新しいエラーが出ないこと。

## English

**Added**
- An alert on the right side of the screen for stalled tasks. Clicking it opens a list window that can be used while watching the map.
- Player mechanoids count as workers.

**Fixed**
- A stuffable blueprint (walls, ...) logged an error on every scan (since v1.0.0).
- Only the job that performs a task counts as work on it (refuelling, repair or hauling do not).
- More ripe crops or empty cells no longer count as progress.
- Large mining/building queues, unreachable interior mining cells, forbidden targets, bills queued on autonomous workbenches and maps with no workers are no longer treated as stalls.
- Every material is checked before a diagnosis; sowing also checks the required skill and cells blocked by items.
- One exception no longer stops the whole scan; bills, zones and animals have per-scan budgets with rotation.
- Jumping from the settings list closes the settings dialog.

**Checked**: in the real game (Linux, 1.7.4985 rev1269) the following were checked. Long normal gameplay is not verified.
- Harmony patch registration (A+B validation passed).
- In a 2,400-tick bounded run, the stall timer is held while no worker is available and resumes afterwards.
- No new errors appeared.

The DLL in this ZIP was built in CI against public reference assemblies (Krafs.Rimworld.Ref 1.7.4985).
