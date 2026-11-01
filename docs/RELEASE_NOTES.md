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
