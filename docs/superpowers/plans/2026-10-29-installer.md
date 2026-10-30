# DualBoot graphical installer implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement these tasks in this session. Steps use checkbox syntax.

**Goal:** Deliver a standalone Portuguese Windows installer for automatic SSD/USB setup and managed recovery.

**Architecture:** Framework 4.8 WinForms EXE with an embedded verified EFI payload; a reusable pure core parses options, creates configuration and journals transactions; native adapters handle WMI/firmware. Existing C boot-manager code remains unchanged.

**Tech Stack:** C# compatible with the Windows Framework compiler, WinForms, System.Management, JavaScriptSerializer, built-in ZIP, PowerShell build scripts.

**Spec:** docs/superpowers/specs/2026-10-29-installer-design.md

## Global constraints

- No physical firmware/disk installation during development.
- Block SSD on Secure Boot enabled/unknown, encrypted/unknown Windows, pending BootNext, invalid targets, external ESP or unmanaged existing directory.
- No keys, local certificates, hardware identifiers or system images in public artifacts.
- Only create owned directories; preserve original EFI entries/files; no automatic reboot, formatting or security-setting changes.
- Runtime requires Windows 10/11 x64/.NET Framework 4.8, no PowerShell commands from the user.

## Review focus

- Malformed or multiple firmware paths: reject without writes; require explicit selection for ambiguity.
- External changes after preview: refresh native inventory and verify original options before any install write.
- Failure after files but before firmware completion: durable journal plus cleanup only for exact owned files.
- Journal/config modified after install: refuse restoration that would remove unknown content.
- Power interruption during config update: record old/new bytes before write and recover exact known states.

## Tasks

### 1. Pure core and transaction tests

Files: installer/Core.cs, installer/BootTransaction.cs, installer/Tests.cs.
Interfaces: FirmwareEntry.Parse(name, Variable), ComputerState/PartitionInfo, InstallRequest, Configuration.Build, Policy.Validate, Journal/JournalStore; reuse IFirmware/Transaction.

- [ ] Implement malformed-entry and policy regression tests, run failing compilation/tests.
- [ ] Implement strict EFI option parsing, target selection/configuration and policy validation.
- [ ] Implement filesystem deployment/journal/install/restore/update against injected IFirmware and temporary roots; tests cover failure, conflicts and serialization.
- [ ] Run installer console tests successfully.

### 2. Native integration and graphical assistant

Files: installer/Native.cs, installer/MainForm.cs, installer/Program.cs, installer/app.manifest.
Consumes pure core; produces real inventory, verified native install actions and demo UI.

- [ ] Implement read-only WMI inventory and restricted EFI adapter; recheck all selections immediately before writing.
- [ ] Implement GUI: system selectors, default/timeout, install/USB/configure/restore/manual actions with progress and concrete summaries.
- [ ] Add --demo mode with fake inventory and no native mutation; --self-test mode bypasses elevation and runs tests.
- [ ] Compile and inspect GUI in demo mode; inspect-only native check if privilege permits.

### 3. Packaging, documentation and public verification

Files: scripts/build-installer.ps1, .github/workflows/verify.yml, README.md, docs/MANUAL.md, CHANGELOG.md.

- [ ] Compile core tests, embed allowlisted EFI payload/manifest/manual, produce DualBoot-Setup-0.2.0.exe and release SHA256.
- [ ] Run native tests/build/12 QEMU scenarios; verify embedded payload hashes and no private paths/keys; include native installer tests/build in CI.
- [ ] Update manual around installer-first workflow with supported cases and manual fallback for Secure Boot/custom layouts.
- [ ] Obtain bounded independent review, fix actionable issues and rerun affected checks.
- [ ] Commit/push, wait for CI, create public experimental v0.2.0 release, verify uploaded digests and record result locally.
