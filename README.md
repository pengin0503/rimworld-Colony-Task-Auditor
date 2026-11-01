# Colony Task Auditor

[日本語](#日本語) | [English](#english)

## 日本語
RimWorld 1.7用の作業監査MOD。建築・採掘などの作業指示、製造指示、栽培ゾーン、未完成品、搾乳・毛刈りの放置を検出します。通知と対象別の監視設定を切り替えられます。マップ位置へ移動できます。

**状態: 開発版。コンパイルとゲーム内動作は未検証です。** 経過時間だけで判断するため、意図的な停止を含む場合があります。指示の削除や強制作業はしません。

### ビルド
.NET 10 SDKとRimWorld 1.7の同じビルドのDLLが必要です。 `dotnet build Source/ColonyTaskAuditor.csproj -c Release /p:RimWorldManaged=/path/to/RimWorld` 。DLLは同梱していません。

## English
A RimWorld 1.7 task auditing utility. Detects potentially stalled construction/mining designations, production bills, growing zones, unfinished items and animal gathering work. Monitor types and notifications can be toggled. You can jump to affected map positions.

**Status: Development source. Compilation and gameplay are not yet verified.** Time-based alerts can include intentionally paused tasks. The mod does not cancel orders or force work.

### Build
.NET 10 SDK and reference DLLs from the matching RimWorld 1.7 build are required. Run `dotnet build Source/ColonyTaskAuditor.csproj -c Release /p:RimWorldManaged=/path/to/RimWorld`. No game DLLs are distributed.
