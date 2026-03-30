# Implementation Plan: Configuration Bootstrap Wizard

**Branch**: `luckyfish/002-config-bootstrap` | **Date**: 2026-03-30 | **Spec**: [/specs/luckyfish/002-config-bootstrap/spec.md]
**Input**: Feature specification from `/specs/luckyfish/002-config-bootstrap/spec.md`

## Summary

This feature adds a first-run onboarding experience to ClawSharp. When the system detects that `appsettings.json` is missing, it launches an interactive CLI wizard to gather essential settings (workspace path, data path, AI provider, and API key). The gathered information is saved to a new `appsettings.json`, and the original command is then executed automatically.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: `ClawSharp.Lib`, `Spectre.Console`, `Microsoft.Extensions.Configuration`.  
**Storage**: `appsettings.json` (Main configuration).  
**Testing**: Integration tests in `ClawSharp.Lib.Tests`.  
**Target Platform**: Cross-platform.
**Project Type**: CLI application and Library.  
**Constraints**: Must run before the DI container is fully built if possible, or before the first command execution.

## Constitution Check

- **Library-First**: ✅ `IConfigBootstrapper` implemented in `ClawSharp.Lib`.
- **Async-First**: ✅ File writing and prompt handling are async where appropriate.
- **Modern .NET**: ✅ Uses .NET 10 features.
- **Reliable Persistence**: ✅ Generates structured `appsettings.json`.

## Project Structure

### Documentation

```text
specs/luckyfish/002-config-bootstrap/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── config-bootstrap.md
└── checklists/
    └── requirements.md
```

### Source Code

```text
ClawSharp.Lib/
├── Configuration/
│   ├── IConfigBootstrapper.cs (New)
│   ├── ConfigBootstrapper.cs (New)
│   └── ProviderTemplate.cs (New)

ClawSharp.CLI/
├── Infrastructure/
│   └── BootstrapWizard.cs (New - Interactive logic)
└── Program.cs (Updated to check for file and run wizard)
```

## Phase 0: Outline & Research

Findings consolidated in `research.md`. Key decision: Run wizard in `Program.cs` before host building.

## Phase 1: Design & Contracts

Design artifacts generated:
- `data-model.md`
- `contracts/config-bootstrap.md`
- `quickstart.md`

### Agent Context Update
Updated `gemini` agent context via `.specify/scripts/bash/update-agent-context.sh`.

## Phase 2: Implementation & Verification

- Implementation of `IConfigBootstrapper` in `ClawSharp.Lib`.
- Implementation of `BootstrapWizard` in `ClawSharp.CLI`.
- Logic integration in `Program.cs`.
- Manual verification of various onboarding scenarios.
