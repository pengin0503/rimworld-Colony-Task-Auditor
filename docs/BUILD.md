# Build / ビルド
CI uses the public reference-only package Krafs.Rimworld.Ref 1.7.4985-unstable, not private copyrighted game files.

CI は公開参照アセンブリ Krafs.Rimworld.Ref 1.7.4985-unstable を使い、ゲーム本体のDLLは含めません。

`dotnet build Source/ColonyTaskAuditor.csproj -c Release`

To target an exact local game build, pass `-p:RimWorldManaged=/path/to/RimWorld`. The referenced directory must contain Assembly-CSharp.dll, Waylith.dll, UnityStubs.dll.

特定のゲーム本体でビルドする場合、対応するDLLを含むディレクトリを指定してください。

Only publish a game-ready release after manual RimWorld 1.7 runtime and save/reload tests.
ゲーム内の動作とセーブ・ロードを実際に検証するまでは正式リリースしないでください。
