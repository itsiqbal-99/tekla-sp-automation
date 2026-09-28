# Single Part Drawing Automation — AI Agent Scope Guard

## Purpose

This document defines the exact working scope of the `SinglePartAutoFix` application so future AI Agents can continue development without expanding the project, inventing Engineering rules, or assuming Tekla behavior that has not been verified. Use this document together with `README.md`.

## 1. What This Application Is

`SinglePartAutoFix` is an internal Windows application that automates repetitive Tekla **Single Part Drawing** preparation. It connects to an already-running Tekla Structures instance, works with the currently open model, reads model Parts, validates relevant readiness, groups physical Parts by `PART_POS`, checks existing Single Part Drawings, and will generate drawings using Tekla's own drawing engine. Engineering remains responsible for final review and release.

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

All new Tekla-specific logic should use API functionality available in **Tekla Open API 2022**. Do not introduce a newer method, constructor, or API behavior unless the requirement is confirmed, the incompatibility is documented, and a version-specific solution is intentionally designed. Do not replace the currently working Tekla 2026 DLL references merely to simulate a 2022 build; the final production build must be compiled and validated in the actual 2022 environment.

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
Extract ID / PART_POS / Profile / Material / Numbering Status
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
Read Existing SinglePartDrawing
      |
      v
Map Drawing.PartIdentifier -> Model.Part
      |
      v
Read Existing Drawing PART_POS
      |
      v
EXISTING / READY TO CREATE
```

This pipeline is the current source of truth.

## 4. Business Identity Rule

A physical Tekla Part ID is **not** the drawing identity used by this application. Multiple physical Parts may share the same `PART_POS`. They are grouped into one `DrawingCandidate`. Do not create one drawing per physical Part. Use `PART_POS` as the current engineering identity for existing-drawing matching. `RepresentativePartId` exists only to provide one concrete model Part when Tekla requires an identifier for drawing operations.

## 5. Numbering Rule

`Operation.IsNumberingUpToDateAll()` is not the only readiness gate. The application currently validates relevant model Parts individually with `Operation.IsNumberingUpToDate(modelPart)`. Do not automatically perform numbering unless the requirement is explicitly changed.

## 6. Selection Rule

For all model Parts, use `Tekla.Structures.Model.ModelObjectSelector`. For objects selected by the user in the Tekla UI, use `Tekla.Structures.Model.UI.ModelObjectSelector`. `GetSelectedObjects()` belongs to the UI selector.

## 7. Part Type Rule

There is a name collision between `Tekla.Structures.Model.Part` and `Tekla.Structures.Drawing.Part`. When handling model objects, explicitly use or alias `Tekla.Structures.Model.Part`.

## 8. Existing Drawing Rule

Existing Single Part Drawings are checked as follows:

```text
DrawingHandler -> GetDrawings() -> SinglePartDrawing -> PartIdentifier
-> SelectModelObject(...) -> Model.Part -> PART_POS
```

A candidate is `EXISTING` when an existing Single Part Drawing resolves to the same `PART_POS`. This has been validated with `CP/100`. Do not replace this with a simple representative Part ID equality check.

## 9. Current Domain / Application Concepts

`PartInfo` represents one physical model Part and currently contains ID, PieceMark, Profile, Material, and numbering status. `PartQuery` defines the scope and currently supports only `All` and `Selected`. `DrawingCandidate` represents one unique `PART_POS` and currently contains RepresentativePartId, PieceMark, Profile, Material, and PartCount. Do not add filters or properties until they are required.

## 10. Current Interfaces

Keep responsibilities narrow. Current / intended interfaces include `IPartReader`, `INumberingChecker`, `IDrawingChecker`, and `IDrawingCreator`. Do not introduce repositories, mediator frameworks, event buses, databases, message queues, CQRS, or similar infrastructure without a real requirement.

## 11. Current Architecture Direction

For the current POC, remain in one Visual Studio project with logical folders: `Application`, `Domain`, and `Infrastructure/Tekla`. Do not prematurely split into multiple projects. Keep Tekla-specific implementation under `Infrastructure/Tekla`.

## 12. MVP In Scope

- Connect to active Tekla model.
- Explicit Part processing scope.
- Read required Part information.
- Validate per-Part numbering readiness.
- Group by `PART_POS`.
- Detect existing Single Part Drawings.
- Use approved drawing attributes / settings.
- Generate Single Part Drawings.
- Safe batch processing after single-drawing POC is proven.
- Report Created, Existing/Skipped, Failed, and Need Review.
- Useful troubleshooting logs.
- Engineering review before release.

## 13. Explicitly Out of Scope

- Assembly Drawing automation.
- General Arrangement Drawing automation.
- Custom 2D drawing engine.
- Custom 3D viewer.
- Replacing Tekla Structures.
- Cloud-native native-Tekla processing.
- Automatic Engineering approval.
- AI-generated drawing geometry or AI decision-making in the initial MVP.
- Automatic numbering without an explicit requirement.
- Arbitrary model geometry changes.
- Database or microservice architecture without a real requirement.

## 14. Engineering Rules Not Yet Defined — Do Not Invent

The AI Agent must not invent: company-approved drawing attribute file names, attribute mapping by Part type, layout requirements, dimensions/annotation rules, title block rules, company-standard exceptions, supported/unsupported Part categories, concrete-Part handling, special-profile handling, retry policy, batch size, output naming/revision rules, or final WPF UI behavior. If one becomes necessary, obtain a confirmed requirement instead of guessing.

## 15. Current Next Development Step

Create **one** Single Part Drawing from **one** `READY TO CREATE` `DrawingCandidate`. Use its RepresentativePartId, create/insert one `SinglePartDrawing`, verify it manually in Tekla, then rerun the checker and confirm that candidate becomes `EXISTING`. Do not implement batch creation before this one-drawing POC is proven.

## 16. AI Agent Working Rules

1. Make one small change at a time.
2. Compile and test each checkpoint.
3. Prefer Tekla Open API 2022-compatible logic.
4. Do not change working Tekla DLL references without a concrete reason.
5. Do not guess Tekla API signatures.
6. Verify version-sensitive calls before introducing them.
7. Do not invent Engineering standards.
8. Preserve tested behavior.
9. Keep Tekla-specific classes isolated from business/domain models.
10. Do not jump directly to WPF, REST, database, batch generation, or AI.
11. Extend proven code rather than unnecessarily rewriting it.
12. Read `README.md` and this file before proposing architecture changes.

## 17. Current Success Definition

At this stage, success means the user can select model Parts, the application identifies unique `PART_POS` candidates, correctly detects existing drawings, and safely identifies what is `READY TO CREATE`. The next milestone is **one verified Single Part Drawing creation**.
