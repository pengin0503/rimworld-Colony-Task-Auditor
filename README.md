# Colony Task Auditor

[日本語](#日本語) | [English](#english)

## 日本語

Colony Task Auditorは、RimWorld 1.7で長時間実行されていない作業を一覧表示するMODです。建築・採掘などの作業指示、製造指示、栽培、未完成品、搾乳・毛刈りを監視します。

MOD設定で監視対象と警告までの日数を指定でき、種類別に絞り込み、検出した作業の位置へ移動できます。進捗値の変化と作業者の活動を観測し、禁止・作業者設定・製造指示制限・到達性を確信度付きで表示します。睡眠・徴兵等で作業者が一時不在の場合や停止中の指示では判定を保留します。指示を強制実行・削除したり、入植者の優先度を変更することはありません。

**現在は開発版で、ゲーム内の動作は未確認です。** 原因を特定できない作業は「不明」です。建築・製造材料、予約、必要スキルを診断します。所持品・運搬中の材料は推定に含めず、不明・推定として表示します。観測間隔中の作業を見逃す場合があり、一覧の経過時間だけでは異常の証明になりません。

Harmonyが必要です。製造完了の観測に1か所の通知用パッチを使い、ジョブや指示は変更しません。250 Tickごとに活動中の作業者だけを観測し、詳細診断は5,000 Tick間隔です。初回観測・作業開始観測・最終進捗のTickを表示します。導入前の作成時刻は分からないため初回観測と表記します。

### 利用方法

RimWorld 1.7用のビルドをMODフォルダへ配置し、有効化してください。「設定」→「MOD設定」→「Colony Task Auditor」で監視条件、検出一覧、位置表示を確認できます。安定性が確認されるまで重要なセーブデータでは使用しないでください。

## English

Colony Task Auditor is a RimWorld 1.7 mod for listing work that has remained unattended for an extended period. It monitors construction/mining designations, production bills, growing zones, unfinished products, and animal milk/shearing work.

In mod settings, choose monitored categories and alert thresholds, filter categories and jump to affected map locations. It samples progress changes and worker activity, and displays forbidden targets, work settings, bill restrictions and reachability with confidence levels. Monitoring pauses when workers are temporarily unavailable (sleep/drafting) or instructions are suspended. It does not force, delete or reorder work or change pawn priorities.

**This is a development build and has not been tested during gameplay.** Unestablished causes remain unknown; construction/recipe materials, reservations and skill requirements are diagnosed with uncertainty for inventories and in-transit supplies. Activity between samples may be missed, and elapsed observation time alone does not establish an abnormal stall.

Harmony is required for a single completion-observation postfix. Work orders and jobs are not modified. Active workers are sampled every 250 ticks; detailed diagnosis runs every 5,000 ticks. First observation, first sampled work and last progress ticks are shown. Creation times before installation are unavailable and are labeled first observation.

### Usage

Install a RimWorld 1.7 build to your Mods folder and enable it. Open Settings → Mod Settings → Colony Task Auditor to configure monitoring and review detected tasks or their locations. Avoid important save files until the mod has been validated.

