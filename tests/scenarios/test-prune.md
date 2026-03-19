# Test Scenario: prune

## Setup
Source folder contains:
- `FakeSuite.dll` — suite assembly, Stored in repo: Yes
- `FakeSuite.pdb` — debug symbols
- `FakeSuite.decompiled.cs` — retained source
- `FakeSuite.dll.config` — config file

Index marks `FakeSuite.dll.config` as `Stored in repo: Yes` (relevant config).
Index marks `FakeSuite.dll` as `Stored in repo: Yes`.
Index marks `FakeSuite.pdb` as `Stored in repo: No`.

## Test Case A: Normal prune
Expected:
- `FakeSuite.pdb` is deleted
- `FakeSuite.dll` is deleted (binary even though index says Yes — binaries are always pruned after decompilation)
- `FakeSuite.decompiled.cs` is retained
- `FakeSuite.dll.config` is retained

## Test Case B: Conflict — prune would delete a file marked Stored in repo: Yes
Inject a scenario where a `.decompiled.cs` file is marked `Stored in repo: Yes` in the index but is not present on disk.
Expected: hard stop with conflict message.

## Pass Criteria
- Binaries (.dll, .exe) always deleted after decompilation regardless of index
- .pdb always deleted
- .decompiled.cs retained if Stored in repo: Yes
- Config files retained if Stored in repo: Yes
- Conflict triggers hard stop
