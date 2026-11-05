# FPSPrototype contributor rules

## Workflow
- Read the relevant GitHub Issue before editing. Use `issue-<number>-<description>` branches.
- Implement and test one dependency phase at a time; submit a PR and wait for user review. Never auto-merge.
- Document observed results separately from unexecuted checks. A compiled prototype is not a verified multiplayer game.
- Preserve original arena/art design. No copied game maps, characters, or paid assets.

## Architecture
- Client: Unity 6000.3.11f1. Server: .NET 10. Shared: pure C# 9 / netstandard2.1, no UnityEngine or UnityEditor references.
- Shared is both a local UPM package and a .NET project; never duplicate its sources in Client.
- Map source of truth: `Shared/Map/Data/training-arena.json`. Changes to collision semantics require a schema version and tests.
- Initial shapes: axis-aligned boxes and cardinal-yaw straight ramps. Do not add arbitrary mesh/terrain/OBB collision.
- Client and server use meters, Y-up, +Z-forward; spawn position is feet position. Player movement ignores other players; shots will use separate hitboxes.
- Server is authoritative at 30 TPS. Client inputs must never set final position, hit, damage, health, ammo, or score.
- LiteNetLib integration starts in Phase 2. Prediction/rewind starts only after baseline movement/combat verification.
- Do not add .NET 10-only APIs to Shared. Store .NET build outputs outside the UPM package.

## Required checks
1. `dotnet build FPSPrototype.slnx -c Release`
2. `dotnet run --project Tests/FPSPrototype.Tests.csproj -c Release`
3. `dotnet run --project Server/FPSPrototype.Server.csproj -c Release -- --validate-map`
4. `pwsh -File Tools/Validate-Unity.ps1 -UnityPath '<Unity.exe>'` (actual Editor compile, generation twice, geometry parity, Play Mode smoke, screenshots).
5. Inspect `Artifacts/phase1-validation.json`, `Artifacts/unity-validation.log`, generated overview/ground screenshots, and `git diff --check`.
6. If Unity cannot run, record the exact error and leave Unity checks unverified; never substitute .NET tests for Unity execution.

The .NET test program is a dependency-free executable and returns nonzero on failure. `dotnet test` alone does not run it. Generated assets in `Client/Assets/Generated` are reproducible and ignored; source `.meta` files and `ProjectSettings` are tracked. Run the generator to restore generated assets after cloning. Never manually edit generated assets.

## PR
Reference the issue, final behavior, commands/results, screenshots where useful, limitations, and remaining phases. Keep authentication material, Library/Temp/Logs, local caches, binaries, and machine paths out of commits. Do not claim CI ran when only local checks ran.
