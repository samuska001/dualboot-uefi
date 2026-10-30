# Progress — 2026-10-29-installer.md

Task 1: implemented parser/policy, reused firmware transaction, durable journal/deployment/update/restore. RED initial parser test, then GREEN; RED deployment stub, then GREEN. Added RED→GREEN regressions for missing encryption fields, journal/ESP binding, unknown empty directories and read-only FAT SteamOS targets.

Task 2: native inventory, WinForms assistant and demo/inspect/render modes implemented. Builder review decisions incorporated in spec. Read-only WMI associations did not expose hidden volumes without elevation, so read their existing AccessPaths and use native read-only volume APIs; local inspection confirms both ESP roots and FAT/FAT32 types without mounting or writing.

Task 3: EXE/ZIP/checksums builder, installer-first README/manual and CI updates implemented. Full C/EFI verification and all 12 QEMU scenarios passed. Independent final review completed; findings fixed below. Public upload/CI verification remain.

Choices: use Windows built-in Framework compiler (no SDK/download for runtime); keep EFI core version 0.1.0 inside installer version 0.2.0; use FAT32 only for writes and allow FAT-family read-only boot targets. Preserve the developer's custom Secure Boot installation and do not physically test install/uninstall on this machine.

Final review fix pass: (1) exclusive staging directory creation + unique setup-owner.id marker + promotion tracking, regression reproduces foreign destination being deleted RED then GREEN; Prepared recovery without marker refuses before firmware writes. (2) consistent 1 MiB journal write/read cap before EFI/NVRAM writes, oversized-data reproduction rejected. (3) unique current-ESP Windows selection helper leaves ambiguity for explicit selection, covered by core assertions. (4) adopted pending config options from exact known new bytes and added read-only pending preview; regression for stale timeout RED then GREEN. The metadata finding is treated as important because stale GUI options can undo a user's committed configuration.

Final installer suite: 28 core checks + 30 deployment checks passed. GUI demo renders without privilege or physical changes. Native inspect was read-only, unelevated, and correctly kept privileged Secure Boot/encryption states unknown.
