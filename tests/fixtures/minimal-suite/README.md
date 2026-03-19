# Minimal Suite Fixture

Test fixture for the dotnet-source-reference plugin pipeline.

## Contents

| File | Classification | Purpose |
|---|---|---|
| `FakeSuite.dll` | suite | Matches `FakeSuite*` pattern in test-profile |
| `Newtonsoft.Json.dll` | third_party | Matches `Newtonsoft.*` pattern |
| `Unknown.Library.dll` | unknown | Matches neither pattern |
| `FakeSuite.decompiled.cs` | — | Pre-decompiled source; used by stages that don't need real decompilation |
| `test-profile.json` | — | Suite profile for this fixture |

## Notes on Zero-Byte Stubs

`FakeSuite.dll`, `Newtonsoft.Json.dll`, and `Unknown.Library.dll` are intentionally zero-byte files. They allow pre-classify to detect assemblies by filename pattern without shipping real binaries.

- `Newtonsoft.Json.dll`: classified as third-party and pruned before decompilation — ilspycmd is never invoked for it.
- `Unknown.Library.dll`: classified as unknown. With `unknown_default: "decompile"` in the profile, it will be sent to ilspycmd, which will fail (zero-byte is not a valid .NET assembly). This is intentional — it tests decompile failure handling. Scenario tests for decompile stages should expect this assembly to have `decompile_status: "failed"`.
- `FakeSuite.dll`: same situation — ilspycmd will fail. Scenario tests that need working source should use `FakeSuite.decompiled.cs` directly (already pre-placed alongside the stub).

## Usage

Pass `tests/fixtures/minimal-suite/test-profile.json` as the `<profile>` argument when testing any command against this fixture.
