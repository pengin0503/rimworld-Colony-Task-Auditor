# Colony Task Auditor

[日本語](#日本語) | [English](#english)

## 日本語

Colony Task Auditor は RimWorld 1.7 用の診断MODです。止まったままになっている作業指示を見つけ、原因を確信度付きで表示します。原因別の対処・作業者診断と重要作業の監視、一時的な優先依頼を支援します。既存Jobを強制中断したり、作業表を書き換えたりしません。

### 対策・重要作業・一時支援

一覧の「詳細・対策」で、原因別の候補対策、必要材料と許可・禁止在庫、技能・割当・到達性を確認できます。材料の選択肢は合算せず、搬送中や所持品まで確認できたとは断定しません。対象の選択と作業割当表への移動もできます。

「重要登録」した対象は、個別の短い警告閾値を設定し、「重要のみ」で絞り込めます。登録はマップ・対象IDごとに保存し、完了・削除時に解除します。

自動化設定の初期値は「確認付き」です。対応する実行可能なバニラ作業を、安全に待機中の割当済み入植者へ期限付きで優先依頼できます。直前に再検証し、手動指示・睡眠・徴兵・治療の必要・危険・通常の順番待ちを優先します。作業表の数値は変更しません。依頼は完成を保証しません。

自動支援には機能の「自動」、重要登録、対象ごとの明示許可が必要です。部分走査、未解明・推定原因、意図的停止、優先度管理MODとの既知の競合では自動操作を停止します。対象を絞った実作業の検証で実行可能性を確認した場合だけ、確定した支援候補として扱います。手動・他MODによる優先依頼の変更で管理を解除し、自動許可を撤回します。上限・クールダウン・履歴をマップ別に保持します。

MODを外す前に一時支援をOFFにしてください。所有している依頼は次Tickに解除します。新機能の通常ゲーム内UI、全マップのセーブ再読込、Harmony監視の実行、270以上のMODでの互換性・TPSは未検証です。

### 監視対象

- 建築（ブループリント・建設中フレーム）、採掘、解体、伐採・収穫などの指示
- 製造指示（無制限の繰り返し指示を含む）
- 栽培区画の未播種セル・収穫待ちの作物
- 未完成品
- 搾乳・毛刈りが可能な家畜

### 主な機能

- 初回観測・作業開始・最終進捗の時刻（Tick）を記録します。導入前からある指示の本当の作成時刻は分からないため、「初回観測」と表示します。
- 進捗値の変化と作業者の活動を観測して停滞を判定します。活動中の作業者は250 Tickごと、詳細診断は5,000 Tickごとです。製造の完了はHarmonyの通知用パッチ1か所で受け取ります。
- 作業として数えるのは、その作業を実際に行うJobだけです（採掘なら採掘、搾乳なら搾乳）。同じ対象への燃料補給・修理・運搬などは数えません。
- 入植者に加えて、プレイヤーのメカノイド（Biotech）も作業者として扱います。
- 原因の診断: 作業者不在、スキル不足（建築・製造・播種）、材料不足・未搬入（必要な材料をすべて確認）、製造指示の制限、予約、到達不能、物で塞がれた播種セル。
- 確信度は「確定」「推定」「不確実」の3段階です。原因を特定できない場合は「不明」と表示します。
- 次の場合は停滞とみなしません。
  - 停止中・目標数に達した製造指示、冬など播種できない時期の区画、プレイヤーが禁止した対象（意図的な保留）は、計測をリセットします。
  - 自律作業台（メカガステーターなど）で、実行中の指示の後ろに並んでいる指示も同様です。
  - 作業者全員が睡眠・徴兵・ダウン・精神状態中、またはマップに作業者が1人もいない間は、計測を一時停止します（リセットはしません）。
  - 同じ作業種が他の場所で進んでいる間の作業待ち（大規模な採掘・建築の順番待ち、使用中の作業台に並ぶ指示）も一時停止し、「順番待ち」と表示します。確定した原因がある場合は計測を続けます。
  - 採掘範囲の内側の、まだ到達できないセルは「掘削待ち」と表示し、計測を一時停止します。
  - 熟した作物や空きセルが増えても進捗とはみなさず、減ったときだけ進捗とします。
  - 新しく見つかった指示にも猶予があります。
- 停滞中の作業は画面右のアラートに件数を出します。クリックすると一覧ウィンドウが開き、開いている間はクリックのたびに各位置へ移動します。
- 通知メッセージは確信度「確定」の停滞1件につき1回だけ出ます。進捗があるまで再通知しません。この状態はセーブデータに保存されます。
- 一覧ウィンドウ（マップを見ながら使えます）とMOD設定画面で、種類別・要確認のみの絞り込み、位置への移動、再スキャンが使えます。
- 監視する種類、警告までの日数（1〜10日）、通知とアラートの有無を設定できます。

### 使い方

1. [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)（`brrainz.harmony`）を導入し、このMODより上に配置します。
2. リリースのZIPを展開した `ColonyTaskAuditor` フォルダを RimWorld の `Mods` フォルダに置き、有効化します。
3. 画面右のアラート、または「設定」→「MOD設定」→「Colony Task Auditor」→「作業一覧ウィンドウを開く」で一覧を確認します。

途中導入・途中削除ができます（記録はマップ単位でセーブに保存され、削除後は単に読み込まれなくなります）。

### 制限事項と未検証範囲

- **通常のゲームプレイでの長時間の動作確認は行っていません。** 確認した範囲は `docs/VALIDATION.md` を参照してください。
- 所持品・運搬中の材料は在庫に数えません。そのため材料関連の診断は「推定」です。到達性は一部の作業者で抽出して判定します。
- 観測間隔の間に終わった作業は、製造の完了を除いて見逃すことがあります。MODの独自の作業や完了処理は、作業として認識できない場合があります（その場合は「不明」のまま計測します）。
- 1回のスキャンには上限があります（指示・フレーム各5,000件、製造指示1,500件、区画セル合計40,000、物体12,000件、家畜1,000頭）。超えた分は次回以降のスキャンで順に検査し、その旨を一覧の下に表示します。調べられなかった対象は飛ばし、件数を表示します。
- 大規模マップでのスキャン時間・TPSへの影響と、多数のMODとの互換性は計測・検証していません。

## English

### Remedies, important tasks and temporary support

Use Details to inspect cause-specific advice, ingredient alternatives, permitted/forbidden stocks, skills, assignment and access. Ingredient alternatives must not be added together; these counts do not prove inventory or delivery status. You can select the target or open the work table.

Mark important tasks, assign shorter individual thresholds and filter to important entries. Registrations use map/task identities and are removed on completion or deletion.

Confirmation is the default. Supported executable vanilla work can receive a bounded priority request for a safe idle enabled colonist. Conditions are rechecked before execution; manual orders, sleep, drafting, medical needs, danger and normal queues take precedence. Work-table values are never changed, and completion is not guaranteed.

Auto requires the feature Auto mode, an important registration and target-specific permission. Partial scans, unknown/inferred causes, intentional pauses and known priority-management MOD conflicts block automatic changes. Only a targeted live work-giver proof can publish a confirmed executable support candidate. Manual/external priority writes release ownership and revoke automatic permission. Caps, cooldown and history are map-local.

Turn temporary support OFF before removing the mod; owned requests are cleared on the next tick. New normal-game UI, full-map save/reload, runtime Harmony observation and 270+ MOD/TPS compatibility remain unverified.


Colony Task Auditor is a diagnostic mod for RimWorld 1.7. It finds work orders that remain stalled and shows likely causes with an explicit confidence level. It adds cause-specific remedies, worker diagnostics, important-task monitoring and temporary priority requests. It does not force or interrupt current jobs or rewrite the work table.

### Monitored work

- Construction (blueprints and frames), mining, deconstruction, plant cutting/harvest and similar designations
- Production bills, including unlimited repeat bills
- Unsown cells and harvestable crops in growing zones
- Unfinished products
- Colony animals ready for milking or shearing

### Features

- Records first observation, first observed work and last progress ticks. The real creation time of orders that existed before installation is unknown and is labeled "first observation".
- Stalls are judged from observed progress values and worker activity. Active workers are sampled every 250 ticks; detailed diagnosis runs every 5,000 ticks. Production completions come from a single Harmony notification postfix.
- Only the job that actually performs a task counts as work on it (mining for a mining order, milking for milk). Refuelling, repairing or hauling on the same target does not.
- Player mechanoids (Biotech) count as workers alongside colonists.
- Diagnosed causes: no assigned worker, missing skill (construction, production, sowing), missing or undelivered materials (every required material is checked), bill restrictions, reservations, unreachable, sowing cells blocked by items.
- Confidence has three levels: confirmed, likely and uncertain. Causes that cannot be established are shown as unknown.
- The following are not treated as stalls:
  - Suspended or completed bills, zones that cannot be sown (for example in winter) and targets the player forbade (a deliberate pause) reset the timer.
  - Bills queued behind the active bill on an autonomous workbench (mech gestator, ...) are treated the same way.
  - Periods when every worker is asleep, drafted, downed or in a mental state, or when no worker is on the map, pause the timer; they do not reset it.
  - Entries waiting their turn while the same work type is progressing elsewhere (large mining or building queues, bills behind a workbench in use) are paused and labeled "waiting in queue". Confirmed blockers keep ageing.
  - Interior mining cells that are not reachable yet are labeled as such and paused.
  - More ripe crops or more empty cells are new work, not progress; only a decrease counts as progress.
  - Newly found orders get a grace period.
- Stalled tasks are counted in an alert on the right side of the screen. Clicking it opens the list window; while the window is open, each click jumps to the next location.
- A notification message is sent once per confirmed stall and is not repeated until progress occurs. This state is stored in the save.
- The list window (usable while watching the map) and the mod settings page provide category and "needs review only" filters, jump-to-location and manual rescan.
- Settings for monitored categories, warning threshold (1–10 days), notifications and the alert.

### Usage

1. Install [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`) and load it above this mod.
2. Extract the release ZIP, place the `ColonyTaskAuditor` folder in RimWorld's `Mods` folder and enable it.
3. Use the alert on the right, or Settings → Mod Settings → Colony Task Auditor → "Open task list window".

The mod can be added to or removed from an existing save. Records are stored per map; after removal they are simply no longer loaded.

### Limitations and unverified scope

- **Not yet tested in long normal gameplay.** See `docs/VALIDATION.md` for exactly what was checked.
- Inventory and in-transit materials are not counted, so material diagnoses are "likely". Reachability is checked for a sample of workers.
- Work that finishes between samples can be missed, except production completions. Modded work and completion paths may not be recognized as work; such tasks keep ageing with an unknown cause.
- Each scan has caps (5,000 designations/frames, 1,500 bills, 40,000 zone cells in total, 12,000 objects, 1,000 animals). Entries over the cap are inspected in later scans in turn, and a notice is shown below the list. Entries that cannot be inspected are skipped and counted.
- Scan time and TPS impact on large maps, and compatibility with large mod lists, have not been measured or verified.
