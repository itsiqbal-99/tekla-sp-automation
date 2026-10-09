# Single Part Drawing Automation

**Last updated:** 2026-10-08

## Overview

Single Part Drawing Automation is an internal Windows application that supports Engineers who create and review Tekla Structures Single Part Drawings.

Tekla remains the source of model data and the drawing engine. The application adds a controlled workflow around Tekla Open API for selection validation, duplicate prevention, drawing creation, post-create verification, navigation, and traceability.

The connection screen keeps its indeterminate animation visible for about 2.5 seconds while the Tekla check runs in parallel, so the transition remains clear without delaying the actual connection request.

The active application is the WPF project. The original Console project is retained as historical POC source but is no longer part of the active solution build.

## Current baseline

- Tekla Structures: **2022 SP14**
- Tekla Open API assembly version: **2022.0.0.0**
- Verified local file version: **2022.0.36168.0**
- Framework: **.NET Framework 4.8.1**
- Language: **C# 7.3**
- Platform: **Windows x64**
- UI: **WPF**
- All Tekla runtime assemblies are resolved from the same Tekla 2022 SP14 installation. Do not mix base, model, or drawing DLLs from different 2022 service-pack file versions.
- Active projects:
  - `SinglePartAutoFix.Core`
  - `SinglePartAutoFix.Wpf`
  - `SinglePartAutoFix.Core.Tests`

## User workflow

```text
Start application
      |
      v
Authenticate
      |
      v
Validate Tekla connection and active model
      |
      v
Select physical parts in Tekla
      |
      v
Normalize and group by PART_POS
      |
      v
Dry check
  - numbering
  - metadata consistency
  - unsupported material
  - existing / duplicate drawings
  - drawing state
      |
      v
Review candidates
  - focus parts in Tekla
  - open existing drawing
      |
      v
Validate drawing standard
      |
      v
Confirm controlled sequential batch
      |
      v
Final duplicate/model/config check per candidate
      |
      v
Create with SinglePartDrawing.Insert()
      |
      v
Re-query drawing and verify configured scale
      |
      v
Created / Existing / Need Review / Failed / Cancelled
```

## Implemented capabilities

### Candidate safety

- Reads selected Tekla model parts.
- Reads model ID, `PART_POS`, profile, material, material type, and numbering state.
- Trims and groups `PART_POS` case-insensitively.
- Stores every physical part ID belonging to a drawing candidate.
- Preserves missing `PART_POS` parts as `NeedReview` instead of silently dropping them.
- Detects inconsistent profile, material, or material type inside a shared `PART_POS` group.
- Checks numbering on every physical part.
- Treats concrete as unsupported by the current workflow and returns `NeedReview`.

### Drawing inventory and creation

- Indexes existing Single Part Drawings by normalized `PART_POS`.
- Detects multiple drawings for the same `PART_POS` and returns `NeedReview`.
- Refreshes drawing inventory during dry check and immediately before creation.
- Creates drawings sequentially with an explicit batch limit and confirmation.
- Continues after an individual candidate fails.
- Supports cancel-after-current during a batch.
- Prevents a stale dry check from creating an already-existing drawing.

### Tekla navigation

- **Focus in Model** selects and highlights every physical part in a candidate.
- **Open Drawing** opens an existing or newly-created drawing.
- The application does not close or save another active drawing automatically.

### Drawing state

The workspace displays state read from Tekla Open API 2022:

- up-to-date status;
- locked;
- frozen;
- issued;
- issued but modified;
- ready for issue;
- creation and modification dates;
- view count and view scales.

### Drawing standard and verification

- Resolves an exact required attribute filename from:
  1. model attributes;
  2. Tekla project paths;
  3. Tekla firm paths;
  4. Tekla system paths.
- Blocks creation when the configured profile cannot be resolved.
- Creates the drawing with the configured Tekla drawing attribute.
- Re-queries Tekla after `Insert()`.
- Verifies that exactly one drawing exists and that readable views use the configured scale.
- Does not report `Verified` from `Insert()` alone.

The current profile is test-only:

| Setting | Value |
|---|---|
| Profile | Single Part Test Standard |
| Drawing attribute | `SP_TEST_STANDARD` |
| Required file | `SP_TEST_STANDARD.wd` |
| Expected view scale | `1:5` |

Dimension, annotation, mark, and layout rules are not treated as pass/fail until Engineering supplies approved rules and reference drawings.

## Status meanings

### Process status

- `ReadyToCreate` — candidate passed the dry check.
- `Existing` — one existing Single Part Drawing was found.
- `Created` — Tekla returned a successful insert; see verification status for final confidence.
- `NeedReview` — unsupported, ambiguous, inconsistent, or duplicate case.
- `Failed` — the operation could not be completed.
- `Cancelled` — the candidate was not started after a user cancellation or failed preflight.

### Verification status

- `PendingCreation`
- `SettingsRequested`
- `Verified`
- `NeedReview`
- `Failed`
- `NotConfigured`
- `NotApplicable`

## Authentication

Authentication is selected with `Auth.Mode` in `SinglePartAutoFix.Wpf/App.config`.

- `Demo` is accepted only in Debug builds.
- `Api` sends `POST` JSON containing `username` and `password` to the configured HTTPS company endpoint.
- Release builds fail closed when left in Demo mode or when API configuration is invalid.
- Backend failures do not fall back to demo credentials.

The exact company endpoint and response contract must be confirmed with the backend owner before a production release. The application currently accepts a successful HTTP response and reads an optional `displayName`, `name`, or `username` field.

## Building

Prerequisites:

- Visual Studio 2022 with .NET Framework desktop development tools;
- .NET Framework 4.8.1 developer pack;
- Tekla Structures 2022 SP14 installed at `C:\TeklaStructures\2022.0` or matching project references;
- restored `packages.config` packages.

Build the active x64 solution:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  SinglePartAutoFix.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64
```

Run the dependency-free Core regression suite:

```powershell
.\SinglePartAutoFix.Core.Tests\bin\x64\Release\SinglePartAutoFix.Core.Tests.exe
```

The regression suite does not require an active Tekla model. Tekla integration behavior must still be validated in Tekla Structures 2022 SP14.

## Project structure

```text
SinglePartAutoFix.Core
  Domain                candidate and part models
  Application           processing, batch workflow, and result models
  Infrastructure/Tekla  Tekla Open API 2022 integration
  Infrastructure/Logging

SinglePartAutoFix.Wpf
  Authentication
  Views
  MainWindow             connection/login/workspace orchestration

SinglePartAutoFix.Core.Tests
  dependency-free regression runner for deterministic Core behavior

SinglePartAutoFix
  legacy Console POC; excluded from the active solution
```

## Logging and support

WPF dry checks, refreshes, and controlled batches write logs below the application output directory in `logs`.

Entries include:

- operation reference;
- model name;
- candidate identity and quantity;
- process and verification status;
- drawing state;
- duration;
- technical exception details when available.

The UI shows an operation reference instead of raw exception details.

## Deliberate safety boundaries

The application does not currently:

- update existing drawings automatically;
- issue or unissue drawings;
- delete or print drawings;
- close or save the active Tekla drawing;
- apply unapproved dimension, annotation, mark, view-placement, or layout rules;
- replace Engineering review and release.

Although Tekla Open API 2022 exposes `UpdateDrawing`, `Modify`, `PlaceViews`, and `CommitChanges`, those operations remain disabled until a supported drawing family and Engineering acceptance criteria are approved.

## Remaining validation before production use

- Confirm the company authentication API contract.
- Run integration/UAT in Tekla Structures 2022 SP14 for focus, open drawing, state reading, duplicate detection, batch cancellation, and post-create verification.
- Confirm `SP_TEST_STANDARD.wd` resolution in the intended model/project/firm environment.
- Replace the test drawing profile with an approved production profile.
- Supply supported drawing-family rules, before/after samples, exceptions, and acceptance criteria.

## Success definition

The workflow is successful when an Engineer can select parts, understand every candidate state, navigate directly to the relevant Tekla objects, create supported drawings without accidental duplication, see whether the requested standard was actually verified, and review the final drawing in Tekla before release.
