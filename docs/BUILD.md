# Build / ビルド
CI uses the public reference-only package Krafs.Rimworld.Ref 1.7.4985-unstable, not private copyrighted game files.

CI は公開参照アセンブリ Krafs.Rimworld.Ref 1.7.4985-unstable を使い、ゲーム本体のDLLは含めません。

`dotnet build Source/ColonyTaskAuditor.csproj -c Release`

To target an exact local game build, pass `-p:RimWorldManaged=/path/to/RimWorld`. The referenced directory must contain Assembly-CSharp.dll, Waylith.dll, UnityStubs.dll.

特定のゲーム本体でビルドする場合、対応するDLLを含むディレクトリを指定してください。

Releases are published by the manually dispatched `Release` workflow on `main`. It builds with the public reference package and attaches the packaged ZIP. Release notes come from `docs/RELEASE_NOTES.md`.

リリースは `main` から手動で起動する `Release` ワークフローで公開します。公開参照パッケージでビルドしたZIPを添付し、説明文は `docs/RELEASE_NOTES.md` から取ります。
