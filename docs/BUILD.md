# Build & test

Use the .NET 10 SDK and RimWorld 1.7 game root containing Assembly-CSharp.dll, Waylith.dll and UnityStubs.dll.

`dotnet build Source/ColonyTaskAuditor.csproj -c Release /p:RimWorldManaged=/path/to/RimWorld`

The build should create `1.7/Assemblies/ColonyTaskAuditor.dll`.

Manual tests required: (1) 2-day abandoned building order; (2) finish order and verify removal; (3) production bill shortage, suspend, fulfilled target, active worker; (4) dormant/winter farming; (5) orphan unfinished product; (6) full-to-gather animal; (7) save/reload and notification once; (8) click map location; (9) UI with 75+ records; (10) map switching and high object count.

A potentially stalled task is not proven to be blocked. Do not publish a gameplay-ready release until tests pass in game.
