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

## Usage

Pass `tests/fixtures/minimal-suite/test-profile.json` as the `<profile>` argument when testing any command against this fixture.
