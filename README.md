# Colony Task Auditor

[日本語](#日本語) | [English](#english)

## 日本語

Colony Task Auditor は RimWorld 1.7 用の診断MODです。止まったままになっている作業指示を見つけ、原因を確信度付きで表示します。作業の強制・指示の削除・優先度や設定の変更は一切行いません。

### 監視対象

- 建築（ブループリント・建設中フレーム）、採掘、解体、伐採・収穫の指示
- 製造指示（無制限の繰り返し指示を含む）
- 栽培区画の未播種セル・収穫待ちの作物
- 未完成品
- 搾乳・毛刈りが可能な家畜

### 主な機能

- 初回観測・作業開始・最終進捗の時刻（Tick）を記録します。導入前からある指示の本当の作成時刻は分からないため、「初回観測」と表示します。
- 進捗値の変化と作業者の活動を観測して停滞を判定します。活動中の作業者は250 Tickごと、詳細診断は5,000 Tickごとです。製造の完了はHarmonyの通知用パッチ1か所で受け取ります。
- 原因の診断: 作業者不在、スキル不足、材料不足・未搬入、禁止、製造指示の制限、予約、到達不能。
- 確信度は「確定」「推定」「不確実」の3段階です。原因を特定できない場合は「不明」と表示します。
- 次の場合は停滞とみなしません。
  - 停止中・目標数に達した製造指示、冬など播種できない時期の区画は、計測をリセットします。
  - 作業者全員が睡眠・徴兵・ダウン・精神状態中の間は、計測を一時停止します（リセットはしません）。
  - 新しく見つかった指示にも猶予があります。
- 通知は確信度「確定」の停滞1件につき1回だけ出ます。進捗があるまで再通知しません。この状態はセーブデータに保存されます。
- 一覧表示、種類別フィルター、該当位置へのカメラ移動、再スキャンが使えます。
- 監視する種類、警告までの日数（1〜10日）、通知の有無を設定できます。

### 使い方

1. [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)（`brrainz.harmony`）を導入し、このMODより上に配置します。
2. リリースのZIPを展開した `ColonyTaskAuditor` フォルダを RimWorld の `Mods` フォルダに置き、有効化します。
3. 「設定」→「MOD設定」→「Colony Task Auditor」で、監視条件・検出一覧・位置移動を使います。

途中導入・途中削除ができます（記録はマップ単位でセーブに保存され、削除後は単に読み込まれなくなります）。

### 制限事項と未検証範囲

- **通常のゲームプレイでの動作確認は行っていません。** 公開前に確認したのは次の範囲です。
  - 実物のゲームDLLを参照したビルド
  - 静的な参照照合
  - モデル単体の確認
  - 限定的なセーブ形式の確認
- 所持品・運搬中の材料は在庫に数えません。そのため材料関連の診断は「推定」です。到達性は一部の作業者で抽出して判定します。
- 観測間隔の間に終わった作業は、製造の完了を除いて見逃すことがあります。バニラの完了処理を通らないMODの作業は検出できない場合があります。
- 大規模マップでのスキャン時間・TPSへの影響と、多数のMODとの互換性は計測・検証していません。対象が上限（指示・フレーム各5,000件、物体12,000件）を超える場合は分割して検査し、その旨を一覧の下に表示します。

## English

Colony Task Auditor is a diagnostic mod for RimWorld 1.7. It finds work orders that remain stalled and shows likely causes with an explicit confidence level. It never forces jobs, deletes orders or changes priorities or settings.

### Monitored work

- Construction (blueprints and frames), mining, deconstruction, plant cutting/harvest designations
- Production bills, including unlimited repeat bills
- Unsown cells and harvestable crops in growing zones
- Unfinished products
- Colony animals ready for milking or shearing

### Features

- Records first observation, first observed work and last progress ticks. The real creation time of orders that existed before installation is unknown and is labeled "first observation".
- Stalls are judged from observed progress values and worker activity. Active workers are sampled every 250 ticks; detailed diagnosis runs every 5,000 ticks. Production completions come from a single Harmony notification postfix.
- Diagnosed causes: no assigned worker, missing skill, missing or undelivered materials, forbidden, bill restrictions, reservations, unreachable.
- Confidence has three levels: confirmed, likely and uncertain. Causes that cannot be established are shown as unknown.
- The following are not treated as stalls:
  - Suspended or completed bills and zones that cannot be sown (for example in winter) reset the timer.
  - Periods when every worker is asleep, drafted, downed or in a mental state pause the timer; they do not reset it.
  - Newly found orders get a grace period.
- A notification is sent once per confirmed stall and is not repeated until progress occurs. This state is stored in the save.
- Task list, category filter, jump-to-location and manual rescan.
- Settings for monitored categories, warning threshold (1–10 days) and notifications.

### Usage

1. Install [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`) and load it above this mod.
2. Extract the release ZIP, place the `ColonyTaskAuditor` folder in RimWorld's `Mods` folder and enable it.
3. Open Settings → Mod Settings → Colony Task Auditor to configure monitoring, review the list and jump to locations.

The mod can be added to or removed from an existing save. Records are stored per map; after removal they are simply no longer loaded.

### Limitations and unverified scope

- **Not yet tested in normal gameplay.** Before release, only the following were checked:
  - a build against the real game DLLs
  - static reference matching
  - model unit checks
  - limited save-format checks
- Inventory and in-transit materials are not counted, so material diagnoses are "likely". Reachability is checked for a sample of workers.
- Work that finishes between samples can be missed, except production completions. Modded work that bypasses vanilla completion may not be detected.
- Scan time and TPS impact on large maps, and compatibility with large mod lists, have not been measured or verified. When targets exceed the caps (5,000 designations/frames, 12,000 objects), scans are split across intervals and a notice is shown below the list.
