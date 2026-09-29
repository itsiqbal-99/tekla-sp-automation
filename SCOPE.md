# Single Part Drawing Automation — AI Agent Scope Guard

**Last updated:** 2026-09-29

## Purpose

This document defines the exact working scope of the `SinglePartAutoFix` application so future AI Agents can continue development without expanding the project, inventing Engineering rules, or assuming Tekla behavior that has not been verified.

Use this document together with `README.md`.

---

## 1. What This Application Is

`SinglePartAutoFix` is an internal Windows application that automates repetitive Tekla **Single Part Drawing** preparation.

It connects to an already-running Tekla Structures instance, works with the currently open model, reads model Parts, validates readiness, groups physical Parts by `PART_POS`, checks existing Single Part Drawings, performs dry-run classification, and creates drawings through a controlled batch workflow using Tekla's own drawing engine.

The core creation workflow is already proven.

The next functional milestone is **Drawing Standardization** using approved Engineering settings and confirmed deterministic rules.

Engineering remains responsible for final drawing review and release.

---

## 2. Fixed Version Strategy

### Production target

- Tekla Structures: **2022**
- Tekla Open API compatibility baseline: **2022**
- Windows x64
- .NET Framework application

### Current development environment

- Tekla Structures: **2026**
- Current working references: DLLs from the installed Tekla 2026 runtime
- Current project type: Console POC
- Future presentation target: WPF

### Mandatory compatibility rule

All new Tekla-specific logic should use API functionality available in **Tekla Open API 2022**.

Do not introduce a newer API method, constructor, or behavior unless:

- the requirement is confirmed,
- incompatibility is documented,
- a version-specific solution is intentionally designed.

Do not replace the currently working Tekla 2026 DLL references merely to simulate a 2022 build.

The final production build must be compiled and validated in the actual Tekla 2022 environment.

---

## 3. Current Verified Pipeline

```text
Connect to Tekla
      |
      v
Read Active Model
      |
      v
Read Selected / All Model Parts
      |
      v
Extract ID / PART_POS / Profile / Material / Material Type / Numbering Status
      |
      v
Validate Numbering Per Part
      |
      v
Group Physical Parts by PART_POS
      |
      v
DrawingCandidate
      |
      v
DrawingProcessor
      |
      +--> Numbering invalid --> NeedReview
      |
      +--> CONCRETE --> NeedReview
      |
      v
Cached Existing Drawing Check by PART_POS
      |
      +--> Existing
      |
      v
Dry Run --> ReadyToCreate
      |
      v
Controlled Batch
      |
      v
SinglePartDrawing.Insert()
      |
      v
Created / Failed
      |
      v
Update Existing-Drawing Cache
      |
      v
File Log + Summary
```

This pipeline is the current source of truth.

### Current status

- Core processing POC: **stable**
- Controlled batch POC: **validated**
- File logging / summaries: **validated**
- Drawing Standardization: **waiting for Engineering input**
- WPF: **not started by design**
- Tekla 2022 production validation: **pending**

---

## 4. Business Identity Rule

A physical Tekla Part ID is **not** the drawing identity used by this application.

Multiple physical Parts may share the same `PART_POS`. They are grouped into one `DrawingCandidate`.

Do not create one drawing per physical Part.

Use `PART_POS` as the current engineering identity for existing-drawing matching.

`RepresentativePartId` exists only to provide one concrete model Part when Tekla requires an identifier for drawing operations.

---

## 5. Numbering Rule

`Operation.IsNumberingUpToDateAll()` is not the only readiness gate.

The application validates relevant model Parts individually with:

`Operation.IsNumberingUpToDate(modelPart)`

Do not automatically perform numbering unless the requirement is explicitly changed.

A candidate is `NeedReview` if its grouped physical parts are not all numbering-ready.

---

## 6. Selection Rule

For all model Parts, use:

`Tekla.Structures.Model.ModelObjectSelector`

For objects selected by the user in Tekla UI, use:

`Tekla.Structures.Model.UI.ModelObjectSelector`

`GetSelectedObjects()` belongs to the UI selector.

---

## 7. Part Type Rule

There is a class-name collision between:

- `Tekla.Structures.Model.Part`
- `Tekla.Structures.Drawing.Part`

When handling model objects, explicitly use or alias:

`Tekla.Structures.Model.Part`

---

## 8. Existing Drawing Rule

Existing Single Part Drawings are resolved as follows:

```text
DrawingHandler
   -> GetDrawings()
   -> SinglePartDrawing
   -> PartIdentifier
   -> SelectModelObject(...)
   -> Model.Part
   -> PART_POS
```

A candidate is `Existing` when an existing Single Part Drawing resolves to the same `PART_POS`.

Do not replace this with a simple representative Part ID equality check.

### Current performance rule

The current implementation loads existing drawing `PART_POS` values once into a case-insensitive:

`HashSet<string>`

Subsequent `Exists()` checks use the cache instead of rescanning all Tekla drawings.

After a drawing is created successfully, the cache is updated so the same running process recognizes that `PART_POS` as existing.

---

## 9. Current Domain / Application Concepts

### PartInfo

Represents one physical model Part and currently includes:

- ID,
- PieceMark / `PART_POS`,
- Profile,
- Material,
- MaterialType,
- numbering status.

### PartQuery

Current processing scopes:

- `All`
- `Selected`

### DrawingCandidate

Represents one unique `PART_POS` and currently includes:

- RepresentativePartId,
- PieceMark,
- Profile,
- Material,
- MaterialType,
- PartCount,
- `IsNumberingUpToDate`.

### DrawingProcessStatus

Current statuses:

- `ReadyToCreate`
- `Existing`
- `Created`
- `Failed`
- `NeedReview`

### DrawingProcessResult

Carries:

- `DrawingCandidate`
- `DrawingProcessStatus`
- Message

Do not add properties until a proven workflow requirement needs them.

---

## 10. Current Interfaces

Keep responsibilities narrow.

Current / intended interfaces include:

- `IPartReader`
- `INumberingChecker`
- `IDrawingChecker`
- `IDrawingCreator`
- `IDrawingProcessor`

### DrawingProcessor responsibility

```text
Candidate
   |
   +--> Numbering invalid --> NeedReview
   |
   +--> Current unsupported Single Part case --> NeedReview
   |
   +--> Existing --> Existing
   |
   +--> Dry Run --> ReadyToCreate
   |
   +--> Create success --> Created
   |
   +--> Create false / exception --> Failed
```

Do not introduce repositories, mediator frameworks, event buses, databases, message queues, CQRS, or similar infrastructure without a real requirement.

---

## 11. Current Architecture Direction

For the current POC, remain in one Visual Studio project with logical folders:

```text
Application
Domain
Infrastructure
  ├── Tekla
  └── Logging
```

Do not prematurely split into multiple projects.

Keep Tekla-specific implementation under `Infrastructure/Tekla`.

WPF must remain presentation only when introduced later.

---

## 12. MVP In Scope

- Connect to active Tekla model.
- Explicit Part processing scope.
- Read required Part information.
- Validate per-Part numbering readiness.
- Group by `PART_POS`.
- Detect existing Single Part Drawings.
- Cache existing drawing identities.
- Dry-run candidate processing.
- Controlled Single Part Drawing creation.
- Safe controlled batch processing.
- Continue after individual candidate failure.
- Prevent accidental duplicate creation.
- Report `ReadyToCreate`, `Existing`, `Created`, `Failed`, and `NeedReview`.
- Useful troubleshooting logs and summaries.
- Use approved drawing attributes / settings.
- Apply confirmed deterministic standardization rules.
- Engineering review before release.

---

## 13. Explicitly Out of Scope

- Assembly Drawing automation.
- General Arrangement Drawing automation.
- Custom 2D drawing engine.
- Custom 3D viewer.
- Replacing Tekla Structures.
- Cloud-native native-Tekla processing.
- Automatic Engineering approval.
- AI-generated drawing geometry or AI decision-making in the initial MVP.
- Automatic numbering without explicit requirement.
- Arbitrary model geometry changes.
- Database or microservice architecture without a real requirement.

---

## 14. Engineering Rules Not Yet Defined — Do Not Invent

The AI Agent must not invent:

- company-approved drawing attribute file names,
- final drawing-family definitions,
- attribute mapping by drawing family,
- layout requirements,
- dimension / annotation rules,
- title block rules,
- company-standard exceptions,
- special-profile handling,
- final production batch size,
- output naming / revision rules,
- final WPF UI behavior.

### Current concrete handling

Tested `CONCRETE` candidates failed the current `SinglePartDrawing` creation path.

The current workflow therefore returns them as:

`NeedReview`

Do not generalize this current technical handling into broader company Engineering standards without confirmation.

---

## 15. Current Next Development Step

The one-drawing POC and controlled batch POC are already proven.

The next functional milestone is:

> **Drawing Standardization POC for one supported drawing family.**

Do not implement standardization rules until Engineering provides:

1. Approved Tekla drawing settings / attributes.
2. One confirmed drawing family for the first POC.
3. Before / After reference drawings.
4. Mandatory adjustment rules.
5. Exception / `NeedReview` cases.
6. Acceptance criteria.

### Safe work while waiting

- regression testing,
- code cleanup,
- technical documentation,
- backlog / progress updates,
- Tekla 2022 compatibility preparation.

Do not start WPF merely because Engineering input is pending.

---

## 16. AI Agent Working Rules

1. Make one small change at a time.
2. Compile and test each checkpoint.
3. Prefer Tekla Open API 2022-compatible logic.
4. Do not change working Tekla DLL references without a concrete reason.
5. Do not guess Tekla API signatures.
6. Verify version-sensitive calls before introducing them.
7. Do not invent Engineering standards.
8. Preserve tested behavior.
9. Keep Tekla-specific classes isolated from business / domain models.
10. Do not jump directly to WPF, REST, database, broad uncontrolled batch expansion, or AI.
11. Extend proven code rather than unnecessarily rewriting it.
12. Read `README.md` and this file before proposing architecture changes.

---

## 17. Current Success Definition

At this stage, success means the user can:

- connect to the active Tekla model,
- select model Parts,
- build unique `PART_POS` drawing candidates,
- validate numbering readiness,
- identify `Existing`, `ReadyToCreate`, and `NeedReview` candidates,
- run a safe dry run,
- create Single Part Drawings through a controlled batch,
- continue processing after an individual failure,
- avoid accidental duplicates,
- record results in a traceable log.

These capabilities have been proven in the current Console POC.

The next milestone is:

> **One verified Drawing Standardization POC using approved Engineering settings and confirmed rules.**