# Single Part Drawing Automation — Technical Handoff

**Document status:** current implementation reference
**Last updated:** 2026-10-09
**Primary application:** `SinglePartAutoFix.Wpf`
**Target environment:** Tekla Structures 2022 SP14

## 1. How to use this document

This document is the minimum technical context required before continuing development with another engineer or AI agent.

When starting a new session:

1. Provide this file as the initial product and architecture context.
2. State the requested change separately.
3. If the repository is available, inspect only the files listed in the relevant section before editing.
4. Treat this document as the design intent, but treat compiling source and runtime behavior as the final truth.
5. Run `git status` before editing because the working tree may contain uncommitted user changes.

Do not recreate the application from this document. Continue the existing WPF and Core projects incrementally.

## 2. Product purpose

The application is a local WPF companion for Tekla Structures. It helps an Engineer safely create and review Single Part Drawings from selected physical model parts.

Tekla remains responsible for:

- model data;
- numbering;
- drawing generation;
- drawing storage;
- final drawing editing and approval.

The application adds:

- candidate grouping and validation;
- existing/duplicate drawing detection;
- controlled drawing creation;
- post-create verification;
- navigation back into Tekla;
- clear drawing state and suggested actions;
- operation logging.

The main product goal is not maximum automation. The goal is to make the common drawing workflow faster while preventing silent data loss, accidental duplicates, and unsafe changes to Engineer-edited drawings.

## 3. Fixed technical baseline

| Item | Baseline |
|---|---|
| Tekla Structures | 2022 SP14 |
| Tekla assembly version | `2022.0.0.0` |
| Verified DLL file version | `2022.0.36168.0` |
| Tekla installation | `C:\TeklaStructures\2022.0\bin` |
| Framework | .NET Framework 4.8.1 |
| C# | 7.3 |
| Platform | Windows x64 |
| UI | WPF |
| Build tool | Visual Studio MSBuild, not `dotnet build` |

All Tekla runtime assemblies must come from the same installation and service-pack file version.

Do not mix, for example:

- `Tekla.Structures.dll` file version `2022.0.10715.0`; and
- `Tekla.Structures.Model.dll` file version `2022.0.36168.0`.

That mismatch previously caused Tekla remoting errors such as:

```text
SerializationException:
Tekla.Structures.ModelInternal.dotUIModelObjectHighlighter_t
is not marked as serializable.
```
The active Core project now resolves `Tekla.Structures`, `Tekla.Structures.Model`, and `Tekla.Structures.Drawing` from the same local SP14 installation.

## 4. Solution structure

```text
SinglePartAutoFix.sln
├─ SinglePartAutoFix.Core
│  ├─ Domain/Models
│  ├─ Application/Configuration
│  ├─ Application/Interfaces
│  ├─ Application/Models
│  ├─ Application/Services
│  └─ Infrastructure
│     ├─ Logging
│     └─ Tekla
├─ SinglePartAutoFix.Wpf
│  ├─ Authentication
│  ├─ Views
│  ├─ MainWindow.xaml
│  └─ App.config
└─ SinglePartAutoFix.Core.Tests
```

The old `SinglePartAutoFix` Console project is a legacy proof of concept. Its source is retained, but it is excluded from the active solution build and must not drive new architecture.

## 5. Architecture rule

The code intentionally uses a simple WPF code-behind structure.

Use concrete classes when there is only one implementation. Add an interface only when it provides a real alternate implementation, a required shared contract, or a useful deterministic test seam.

The two important Core test seams are:

- `IDrawingChecker`;
- `IDrawingCreator`.

Avoid introducing:

- full MVVM rewrites;
- a DI container;
- repositories or databases;
- mediator/event-bus layers;
- generic rule engines;
- one interface per class;
- wrapper providers that only return an existing list;
- result DTOs for a simple `bool + out message` flow.

The current implementation was deliberately simplified after an earlier version introduced too many small abstractions.

## 6. End-to-end runtime flow

```text
Application starts
    |
    v
Show animated Tekla connection screen for at least 2.5 seconds
    |-- Tekla connection check runs in parallel
    v
Connection/model available?
    |-- No  -> show recovery instructions and Retry
    `-- Yes -> show Login
                  |
                  v
              Authenticate
                  |
                  v
              Drawing Workspace
                  |
                  v
       Select physical parts in Tekla
                  |
                  v
     Read properties and build candidates
                  |
                  v
               Dry Check
       numbering / metadata / material
       existing and duplicate drawings
       drawing state and scale
                  |
                  v
       Review, filter, focus, open
                  |
                  v
       Validate drawing standard
                  |
                  v
       Confirm sequential batch <= 10
                  |
                  v
       Revalidate before every item
                  |
                  v
       Final duplicate lookup
                  |
                  v
       SinglePartDrawing.Insert()
                  |
                  v
       Refresh, re-query, verify
                  |
                  v
       Log and display final result
```

## 7. Startup and Tekla connection

Entry orchestration is in:

- `SinglePartAutoFix.Wpf/MainWindow.xaml.cs`;
- `SinglePartAutoFix.Wpf/Views/TeklaConnectionView.xaml`;
- `SinglePartAutoFix.Wpf/Views/TeklaConnectionView.xaml.cs`.

Behavior:

1. `MainWindow` creates a `TeklaModelSession`.
2. `CheckTeklaAsync()` displays `TeklaConnectionView`.
3. `CheckSession()` runs on a background task.
4. `Task.WhenAll(checkTask, Task.Delay(2500))` keeps the animated connection screen visible for approximately 2.5 seconds.
5. The delay affects only screen transition; the Tekla check starts immediately.
6. A successful check requires:
   - model API connection;
   - a non-empty active model name.
7. Retry creates a new `TeklaModelSession`, because reusing a failed `Model` object may preserve stale connection state.
8. If the user was already signed in, reconnection returns to the workspace. Otherwise it opens Login.

The visual animation is the indeterminate WPF progress bar. Do not add a blocking sleep or a second timer-based animation unless there is a demonstrated UX need.

## 8. Authentication flow

Relevant files:

- `SinglePartAutoFix.Wpf/Authentication/ILoginService.cs`;
- `DemoLoginService.cs`;
- `ApiLoginService.cs`;
- `LoginServiceFactory.cs`;
- `LoginRequest.cs`;
- `LoginResult.cs`;
- `SinglePartAutoFix.Wpf/App.config`.

Configuration keys:

```xml
Auth.Mode
DummyAuth.Username
DummyAuth.Password
DummyAuth.DisplayName
Auth.Api.BaseUrl
Auth.Api.LoginPath
Auth.Api.TimeoutSeconds
```

Rules:

- `Demo` mode is accepted only in Debug builds.
- Release builds fail closed if left in Demo mode.
- `Api` mode requires an absolute HTTPS base URL.
- Login sends JSON containing `username` and `password`.
- A successful HTTP status is currently treated as successful authentication.
- Optional display name fields are read in this order: `displayName`, `name`, `username`.
- Backend/network failure never falls back to demo credentials.
- Authentication is rechecked before every batch item through the workspace callback.

Known production gap: the real company endpoint, token/session contract, logout behavior, and response schema still require confirmation from the backend owner. Do not invent role, refresh-token, offline-cache, or persistence behavior without that contract.

## 9. Part selection and candidate construction

Relevant files:

- `Infrastructure/Tekla/TeklaPartReader.cs`;
- `Application/Services/DrawingCandidateBuilder.cs`;
- `Domain/Models/PartInfo.cs`;
- `Domain/Models/DrawingCandidate.cs`.

`TeklaPartReader` reads selected Tekla model parts and collects:

- model object ID;
- `PART_POS`;
- profile;
- material;
- `MATERIAL_TYPE`;
- numbering state using `Operation.IsNumberingUpToDate(part)`.

Candidate rules:

1. `PART_POS` is the current business identity.
2. Non-empty `PART_POS` is trimmed and grouped case-insensitively.
3. One normalized `PART_POS` becomes one `DrawingCandidate`.
4. Every physical model object ID is stored in `ModelPartIds`.
5. `RepresentativePartId` is used only when Tekla needs one physical part to create a drawing.
6. Numbering is valid only if all physical parts in the group are up to date.
7. Profile, material, and material type must be consistent inside the group.
8. Missing `PART_POS` parts are preserved as individual `NeedReview` candidates. They must never disappear silently.

Important `DrawingCandidate` fields:

| Field | Meaning |
|---|---|
| `RepresentativePartId` | Part used for drawing insertion |
| `ModelPartIds` | All physical parts in the candidate |
| `PieceMark` | Normalized `PART_POS` |
| `PartCount` | Number of physical parts |
| `IsNumberingUpToDate` | Aggregate numbering state |
| `ValidationMessage` | Missing/inconsistent metadata reason |

## 10. Drawing inventory and lookup

Relevant files:

- `Application/Interfaces/IDrawingChecker.cs`;
- `Infrastructure/Tekla/TeklaDrawingChecker.cs`;
- `Application/Models/DrawingLookupResult.cs`;
- `DrawingLookupStatus.cs`;
- `DrawingSnapshot.cs`.

`TeklaDrawingChecker.Refresh()`:

1. Requires an active Tekla Drawing API connection.
2. Enumerates `DrawingHandler.GetDrawings()`.
3. Keeps only `SinglePartDrawing` objects.
4. Resolves every drawing's `PartIdentifier` back to a model part.
5. Reads that part's `PART_POS`.
6. Builds a case-insensitive dictionary keyed by trimmed `PART_POS`.
7. Stores one `DrawingSnapshot` for every drawing.

Lookup results:

| Status | Meaning |
|---|---|
| `NotFound` | No drawing uses this normalized `PART_POS` |
| `Found` | Exactly one drawing found |
| `Duplicate` | More than one drawing uses the same normalized `PART_POS` |
| `Failed` | Inventory/query could not be completed |

`DrawingSnapshot` stores:

- part identifier;
- piece mark;
- drawing name and mark;
- creation and modification dates;
- raw `UpToDateStatus` enum text;
- locked, frozen, issued, issued-but-modified, and ready-for-issue flags;
- readable view scales.

Friendly state labels are shown in the grid, but the raw Tekla status is retained in detail/log output.

The index is refreshed:

- during a new dry check;
- when the user clicks Refresh;
- immediately before drawing insertion;
- after successful insertion.

## 11. Dry-check decision order

The main decision logic is in `Application/Services/DrawingProcessor.cs`.

For each candidate, the order is intentionally fixed:

1. Candidate metadata validation issue -> `NeedReview`.
2. Numbering not up to date -> `NeedReview`.
3. Material type equals `CONCRETE` -> `NeedReview` because concrete is currently unsupported.
4. Query existing drawings.
5. Duplicate -> `NeedReview`.
6. Lookup failure -> `Failed`.
7. Exactly one existing drawing -> `Existing`.
8. No drawing and dry-run mode -> `ReadyToCreate`.

Do not reorder duplicate lookup after creation. The pre-insert lookup is a safety boundary.

## 12. Drawing navigation

Relevant file: `Infrastructure/Tekla/TeklaNavigationService.cs`.

### Focus in Model

1. Resolve every `ModelPartIds` entry through the active model.
2. Ignore IDs that no longer resolve.
3. If none resolve, instruct the user to select parts again.
4. Pass an `ArrayList` to `Tekla.Structures.Model.UI.ModelObjectSelector.Select()`.

Do not add `Operation.Highlight()` after selection. UI selection already highlights the objects, and the explicit highlighter previously triggered a Tekla 2022 remoting serialization error.

### Open Drawing

1. Query drawings fresh when the button is clicked. Do not retain old Tekla drawing objects in UI state.
2. Match normalized `PART_POS` case-insensitively.
3. Refuse when no drawing or multiple drawings are found.
4. If the target is already active, return success without another operation.
5. If another drawing is active, instruct the user to save or close it in Tekla.
6. Never automatically save or close the active drawing.
7. Otherwise call `DrawingHandler.SetActiveDrawing(target)`.

## 13. Drawing standard configuration

Relevant files:

- `Application/Configuration/DrawingStandardConfiguration.cs`;
- `Application/Models/DrawingStandardProfile.cs`;
- `Infrastructure/Tekla/TeklaDrawingStandardConfigurationResolver.cs`;
- `TeklaDrawingStandardConfigurationValidator.cs`.

Current test-only profile:

| Property | Value |
|---|---|
| ID | `single-part-test` |
| Name | `Single Part Test Standard` |
| Version | `1.0` |
| Drawing attribute | `SP_TEST_STANDARD` |
| Exact required file | `SP_TEST_STANDARD.wd` |
| Expected scale | `5.0` (`1:5`) |

Resolution order:

1. `<model path>\attributes`;
2. `XS_PROJECT` paths;
3. `XS_FIRM` paths;
4. `XS_SYSTEM` paths.

Paths are read with `TeklaStructuresSettings.GetAdvancedOptionPaths(...)`.

Rules:

- Search only the exact required filename.
- Do not use wildcard matching such as `attributeName.*`.
- Exactly one profile must be enabled.
- Creation is blocked when the profile is missing, ambiguous, disabled, disconnected, or unresolved.
- `SP_TEST_STANDARD` must remain visibly labelled as a test profile.
- A production profile may only come from Engineering-approved attributes and acceptance samples.

## 14. Controlled drawing creation

Relevant files:

- `Application/Services/DrawingBatchRunner.cs`;
- `Application/Services/DrawingProcessor.cs`;
- `Infrastructure/Tekla/TeklaDrawingCreator.cs`;
- `Views/DrawingWorkspaceView.xaml.cs`.

UI rules:

- Only `ReadyToCreate` rows can be submitted.
- Supported batch choices are 1, 3, 5, and 10.
- Ten is the maximum batch size.
- The user sees a confirmation containing piece mark, profile, and quantity.
- Processing is sequential.

Before each candidate, `ValidateBatchContext()` confirms:

- authentication remains valid;
- Tekla remains connected;
- active model path is unchanged from workspace entry;
- drawing standard remains valid.

If a global preflight fails, the current item is marked failed and remaining unstarted items are marked cancelled.

Per candidate creation:

```text
normal validation
    -> normal drawing lookup
    -> forced fresh drawing lookup
    -> duplicate/existing safety gate
    -> SinglePartDrawing(partIdentifier, attributeName)
    -> Insert()
    -> refresh drawing index
    -> re-query
    -> verification
```

An individual candidate failure does not stop the next candidate.

Cancellation is cooperative between items. The active Tekla operation is allowed to finish; remaining candidates become `Cancelled`.

## 15. Post-create verification

`Insert()` success is not sufficient for `Verified`.

After creation, verification checks:

1. The drawing can be found again.
2. Exactly one matching drawing exists.
3. The drawing snapshot is readable.
4. The drawing's `PartIdentifier` matches one of the candidate's physical part IDs, or its representative part ID.
5. At least one readable view exists.
6. An expected scale is configured.
7. Every readable view scale matches the expected value with tolerance `< 0.01`.

For the current test profile, the expected scale is `1:5`.

Important status nuance:

- Process status may be `Created` because Tekla inserted the drawing.
- Verification/standardization status may simultaneously be `NeedReview` if the re-query or scale check failed.

The UI must not equate `Created` with `Verified`.

Dimension count, mark count, annotation rules, layout, and view placement are not pass/fail criteria because Engineering has not supplied approved rules.

## 16. Status contracts

### Process status

| Status | Meaning |
|---|---|
| `ReadyToCreate` | Dry check passed and no drawing exists |
| `Existing` | Exactly one existing drawing found |
| `Created` | Tekla reported successful insertion; inspect verification separately |
| `NeedReview` | Unsupported, inconsistent, missing, or duplicate condition |
| `Failed` | Operation could not be completed |
| `Cancelled` | Candidate was not started after cancellation/global preflight failure |

### Standardization/verification status

| Status | Meaning |
|---|---|
| `NotConfigured` | No usable verification configuration |
| `PendingCreation` | Verification waits for drawing creation |
| `SettingsRequested` | Attributes were requested, but no complete verification rule exists |
| `Verified` | Re-query and all configured checks passed |
| `NeedReview` | Drawing exists but a configured check failed |
| `Failed` | Standardization/creation operation failed |
| `NotApplicable` | Existing/unsupported case was not modified |

## 17. Workspace UI behavior

Relevant files:

- `Views/DrawingWorkspaceView.xaml`;
- `Views/DrawingWorkspaceView.xaml.cs`.

The workspace shows:

- authenticated user;
- Tekla/model connection;
- drawing standard state;
- preflight summary;
- selection summary;
- Ready, Existing, Review, and Failed counts;
- candidate grid;
- selected-candidate detail;
- batch controls.

Preflight summary contains:

- Authentication;
- Tekla Connection;
- Active Model;
- Drawing API;
- Drawing Standard.

Available filters:

- process status;
- up-to-date drawing;
- drawing needing attention;
- verified drawing;
- free-text search.

Available actions:

- Select Parts;
- Run Dry Check;
- Refresh;
- Focus in Model;
- Open Drawing;
- Select Ready;
- Create Drawings;
- Cancel Batch;
- Logout.

The UI deliberately remains code-behind based. Do not redesign it merely to satisfy a framework pattern.

## 18. Logging and user-facing errors

Relevant file: `Infrastructure/Logging/SimpleFileLogger.cs`.

Log location:

```text
<application output directory>\logs\single_part_yyyyMMdd.log
```

Logged data includes:

- process name;
- operation ID;
- model name;
- piece mark/profile/material type/quantity;
- process status;
- verification status;
- drawing state and raw Tekla state enum;
- duration;
- technical exception.

Unexpected errors receive an eight-character operation reference.

The UI should show:

- a plain-language failure;
- a suggested recovery action;
- the operation reference.

The UI should not show raw stack traces. Stack traces belong in logs.

## 19. Tekla Open API 2022 calls currently used

| Purpose | API |
|---|---|
| Check model connection | `Model.GetConnectionStatus()` |
| Read model info | `Model.GetInfo()` |
| Read selected objects | `Model.UI.ModelObjectSelector.GetSelectedObjects()` |
| Select/highlight parts | `Model.UI.ModelObjectSelector.Select(ArrayList)` |
| Check numbering | `Operation.IsNumberingUpToDate(part)` |
| Resolve model object | `Model.SelectModelObject(Identifier)` |
| Check drawing connection | `DrawingHandler.GetConnectionStatus()` |
| Enumerate drawings | `DrawingHandler.GetDrawings()` |
| Read active drawing | `DrawingHandler.GetActiveDrawing()` |
| Open drawing | `DrawingHandler.SetActiveDrawing(...)` |
| Create drawing | `SinglePartDrawing(...).Insert()` |
| Read drawing state | `UpToDateStatus`, lock/freeze/issue flags |
| Read sheet/views | `Drawing.GetSheet()`, `GetAllViews()` |
| Read scale | `View.Attributes.Scale` |
| Resolve standard paths | `TeklaStructuresSettings.GetAdvancedOptionPaths(...)` |

APIs confirmed available but deliberately not used automatically:

- `DrawingHandler.UpdateDrawing()`;
- `View.Modify()`;
- `Drawing.PlaceViews()`;
- `Drawing.CommitChanges()`.

## 20. Safety boundaries

The application must not automatically:

- modify existing drawings;
- update drawings in bulk;
- close or save an active drawing;
- issue or unissue drawings;
- delete drawings;
- print drawings;
- change view scale/layout after creation;
- apply dimension, annotation, or mark rules without Engineering approval.

Never remove these gates without explicit product and Engineering approval:

- missing `PART_POS` remains visible;
- metadata inconsistency becomes `NeedReview`;
- numbering is checked for every physical part;
- duplicate lookup immediately before insert;
- active model path check before each batch item;
- re-query after creation;
- `Verified` only after actual checks.

## 21. Build and test

Build with Visual Studio MSBuild:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  .\SinglePartAutoFix.sln `
  /t:Build `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /m `
  /v:minimal
```

Run Core regression tests:

```powershell
.\SinglePartAutoFix.Core.Tests\bin\x64\Release\SinglePartAutoFix.Core.Tests.exe
```

Current regression suite contains 12 scenarios:

1. case-insensitive candidate grouping;
2. missing `PART_POS` preservation;
3. inconsistent metadata detection;
4. numbering failure;
5. existing drawing state mapping;
6. duplicate drawing mapping;
7. final duplicate check prevents insertion;
8. successful scale verification;
9. unexpected scale requires review;
10. post-create lookup failure requires review;
11. batch continues after one creation failure;
12. cancellation marks remaining candidates.

Current verified result at this document update:

```text
Release x64 build: successful
Regression tests: 12 passed, 0 failed
```

Do not use `dotnet build` as the authoritative WPF build command. This is an older .NET Framework WPF project and requires the Visual Studio MSBuild XAML pipeline.

On machines with Windows Application Control, a newly generated unsigned executable or DLL may occasionally be blocked with `0x800711C7`. Confirm whether the failure explicitly names Application Control before treating it as a source-code error.

## 22. Tekla 2022 SP14 integration/UAT checklist

The Core tests do not replace live Tekla testing. Validate these manually:

- connection animation remains visible for approximately 2.5 seconds;
- initial connection and Retry;
- Tekla opened after the application;
- model switch while workspace is open;
- selection of multiple physical parts with one `PART_POS`;
- missing and inconsistent `PART_POS` cases;
- Focus in Model highlights all candidate parts;
- Open Drawing when none, one, duplicate, same active, and another active drawing exists;
- locked, frozen, issued, issued-but-modified, outdated drawing state;
- manual drawing creation followed by Refresh;
- `SP_TEST_STANDARD.wd` from model/project/firm/system paths;
- controlled creation with scale `1:5`;
- post-create identifier verification;
- connection loss between batch items;
- cancel-after-current;
- rerun without duplicate creation.

## 23. Known gaps before production

1. Confirm the actual company authentication API contract.
2. Replace placeholder authentication URL and Demo mode configuration.
3. Confirm `SP_TEST_STANDARD.wd` in the intended UAT environment.
4. Run full Tekla 2022 SP14 integration/UAT.
5. Replace the test profile with an Engineering-approved production profile.
6. Obtain supported drawing families, reference drawings, exception rules, and measurable acceptance criteria before adding more verification.

## 24. Recommended direction for future changes

Prioritize improvements that reduce Engineer effort or uncertainty:

- clearer actionable messages based on real UAT findings;
- approved production standard profile support;
- additional read-only verification backed by Engineering rules;
- small UI refinements proven useful during UAT;
- better authentication handling after the backend contract is known.

Do not add automatic drawing repair merely because the API supports it.

If a future Safe Update feature is approved:

- operate on one drawing at a time;
- only update outdated drawings;
- block locked, frozen, and issued drawings;
- require confirmation;
- run verification again afterward.

## 25. Instructions for the next AI agent

Before changing code:

1. Read this document completely.
2. Read `README.md` and `SCOPE.md`.
3. Run `git status --short` and preserve unrelated user changes.
4. Inspect only the source files connected to the requested behavior.
5. Confirm every new Tekla API against the local Tekla 2022 SP14 DLLs or official version-specific documentation.

While implementing:

- Keep WPF as the primary application.
- Keep Tekla 2022 SP14 as the baseline.
- Prefer direct, readable C# 7.3 code.
- Reuse current models and services before creating new types.
- Do not create an interface for a single implementation without a concrete reason.
- Do not perform model-wide scans unless the feature requires them.
- Do not modify an existing drawing automatically.
- Preserve all duplicate, model, configuration, and post-create gates.
- Keep raw technical exceptions in logs, not in user messages.

Before handing off:

1. Build Release x64 with Visual Studio MSBuild.
2. Run all Core regression tests.
3. Parse or build changed XAML.
4. Run `git diff --check`.
5. State which live Tekla behaviors still require UAT.
6. Update this document if architecture, flow, status meaning, safety rules, or production prerequisites changed.

## 26. Suggested continuation prompt

The following short prompt is sufficient when this document is supplied to another agent:

```text
Use TECHNICAL_HANDOFF.md as the current product and architecture context.
Preserve the Tekla 2022 SP14 baseline, current safety gates, and simple
code-behind architecture. Inspect only the files relevant to this request,
then implement and verify the change with Release x64 build and Core tests.

Requested change:
<describe the next change here>
```
