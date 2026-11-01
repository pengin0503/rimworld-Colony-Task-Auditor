Colony Task Auditor v1.0.0 for RimWorld 1.7 (built against 1.7.4985). Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) (`brrainz.harmony`).

## 日本語

止まったままになっている作業指示を見つけ、原因を確信度付きで表示する診断MODです。作業の強制・指示の削除・設定の変更は行いません。

**主な機能**
- 監視対象: 建築・採掘・解体・伐採の指示、製造指示（無制限を含む）、栽培・収穫、未完成品、搾乳・毛刈り
- 初回観測・作業開始・最終進捗の記録と、実際の進捗を考慮した停滞判定
- 原因診断: 作業者不在、スキル不足、材料不足・未搬入、禁止、製造指示の制限、予約、到達不能。確信度は確定・推定・不確実の3段階
- 睡眠・徴兵中は計測を一時停止し、停止中の指示・播種できない時期・新しい指示は停滞とみなさない
- 一覧、種類別フィルター、位置への移動、重複のない通知、監視種類と期間の設定

**導入**: ZIPを展開し、`ColonyTaskAuditor` フォルダを `Mods` に置き、Harmonyより下で有効化します。

**未検証範囲・既知の制限**
- 通常のゲームプレイでの動作は確認していません。確認済みなのは、実物ゲームDLLを参照したビルド、静的な参照照合、モデル単体の確認、限定的なセーブ形式の確認です。
- 所持品・運搬中の材料は数えないため、材料の診断は推定です。観測間隔の間に終わった作業は、製造の完了を除いて見逃すことがあります。
- 大規模マップでのスキャン時間・TPS、多数MOD環境での互換性は未計測・未検証です。
- このZIPに含まれるDLLは、公開参照アセンブリ（Krafs.Rimworld.Ref 1.7.4985）でCIビルドしたものです。

## English

A diagnostic mod that finds stalled work orders and shows likely causes with a confidence level. It never forces jobs, deletes orders or changes settings.

**Features**
- Monitors construction, mining, deconstruction and cutting designations, production bills (including unlimited), sowing/harvest, unfinished products, milking and shearing
- Records first observation, first observed work and last progress, and judges stalls from actual progress
- Diagnoses: no worker, missing skill, missing/undelivered materials, forbidden, bill restrictions, reservations, unreachable; confidence levels are confirmed, likely and uncertain
- Sleep and drafting pause the timer; suspended bills, unsowable seasons and new orders are not treated as stalls
- Task list, category filter, jump to location, de-duplicated notifications, configurable categories and threshold

**Installation**: extract the ZIP, place the `ColonyTaskAuditor` folder in `Mods`, and enable it below Harmony.

**Unverified scope and known limitations**
- Not tested in normal gameplay. Checked: a build against the real game DLLs, static reference matching, model unit checks and limited save-format checks.
- Inventory and in-transit materials are not counted, so material diagnoses are "likely". Work finishing between samples can be missed, except production completions.
- Scan time/TPS on large maps and compatibility with large mod lists are not measured or verified.
- The DLL in this ZIP was built in CI against public reference assemblies (Krafs.Rimworld.Ref 1.7.4985).
