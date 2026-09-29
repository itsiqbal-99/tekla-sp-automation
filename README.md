# Single Part Drawing Automation

**Last updated:** 2026-09-29

## 1. Project Overview

**Single Part Drawing Automation** is an internal Windows application that automates repetitive Single Part Drawing preparation around an active **Tekla Structures** model.

The application does **not** replace Tekla Structures, its model, or its drawing engine. Tekla remains the source of model data and the engine responsible for creating and storing drawings. This application provides a controlled automation workflow around Tekla Open API.

### Current development environment

- **Production target:** Tekla Structures 2022
- **Production API baseline:** Tekla Open API 2022
- **Current development runtime:** Tekla Structures 2026
- **Current development references:** Tekla Open API 2026 assemblies from the local Tekla 2026 installation
- **Compatibility rule:** new Tekla-specific logic must use API functionality available in Tekla Open API 2022 unless explicitly approved otherwise
- **Language:** C#
- **Framework:** .NET Framework 4.8.1
- **Desktop UI target:** WPF
- **Target platform:** Windows x64
- **Current POC type:** Console application
- **Current project:** `SinglePartAutoFix`

### Current development status

> **Core processing POC: stable**  
> **Controlled batch POC: validated**  
> **Drawing Standardization POC: waiting for Engineering input**  
> **WPF: not started by design**  
> **Tekla 2022 production validation: pending**

---

## 2. Business Problem

Tekla Structures can already generate Single Part Drawings in bulk. The main Engineering pain point is not simply creating the drawings, but the repetitive work required **after generation** to make each drawing match company standards.

A typical workflow is:

1. Identify the parts that need drawings.
2. Check whether the model is ready and numbered.
3. Check whether a Single Part Drawing already exists.
4. Create or retrieve the drawing.
5. Open and review the drawing.
6. Apply the correct approved Tekla drawing settings.
7. Adjust views, dimensions, annotations, scale, layout, and other drawing-specific details where required.
8. Save the drawing.
9. Repeat the same standardization work across many parts.
10. Review exceptions manually.

The project therefore focuses on **controlled generation + repetitive standardization + traceability**, while Engineering remains responsible for final review and release.

---

## 3. Project Goal

The primary goal is:

> **Automate the controlled creation and repetitive standardization of Single Part Drawings from an active Tekla Structures model using approved Engineering settings and deterministic rules.**

The workflow should be:

- faster,
- repeatable,
- traceable,
- maintainable,
- safe to rerun,
- easy to troubleshoot,
- clear about unsupported or ambiguous cases.

The initial MVP remains **rule-based and deterministic**. AI is not required for the current workflow.

---

## 4. Target Workflow

```text
Tekla Structures
      |
      v
Check Connection / Model
      |
      v
Read Selected Parts
      |
      v
Validate Readiness
(Numbering / Eligibility)
      |
      v
Group Physical Parts by PART_POS
      |
      v
Check Existing Drawings
      |
      v
Create / Retrieve Drawing
      |
      v
Load Approved Tekla Drawing Settings
      |
      v
Apply Engineering Standardization Rules
      |
      v
Validate
      |
      v
Save
      |
      v
Created / Existing / Failed / Need Review
      |
      v
Engineering Review
```

Unsupported or ambiguous cases should become **Need Review** instead of being forced through automation.

---

## 5. MVP Scope

### In scope

- Connect to the active Tekla model.
- Read selected / explicit model parts.
- Read:
  - identifier,
  - `PART_POS`,
  - profile,
  - material / grade,
  - material type,
  - numbering readiness.
- Group physical parts by `PART_POS`.
- Check existing Single Part Drawings.
- Cache existing drawing identities for efficient repeated checks.
- Run dry-run classification.
- Create Single Part Drawings through controlled sequential processing.
- Continue after an individual candidate fails.
- Prevent accidental duplicate creation.
- Update the existing-drawing cache after successful creation.
- Report:
  - `ReadyToCreate`,
  - `Existing`,
  - `Created`,
  - `Failed`,
  - `NeedReview`.
- Write simple processing logs and summaries.
- Use approved drawing settings / attributes.
- Apply confirmed deterministic standardization rules.
- Keep Engineering review before production release.

### Current implementation status

Implemented and validated in the Console POC:

- Tekla model connection.
- Selected-part reading.
- `PART_POS` grouping.
- Per-part numbering readiness.
- Material type reading.
- Existing drawing detection by `PART_POS`.
- Cached existing-drawing lookup.
- Dry run.
- Single drawing creation.
- Duplicate prevention.
- Controlled batch processing with confirmation and limit.
- Continue-on-error behavior.
- Processing summaries.
- File logging.
- `CONCRETE` candidates currently return `NeedReview` for the current Single Part Drawing workflow.

Pending Engineering input:

- approved Tekla drawing settings / attributes,
- confirmed drawing-family definitions,
- mandatory standardization rules per family,
- before / after reference drawings,
- acceptance criteria,
- exception / `NeedReview` cases.

### Out of scope for MVP

- Replacing Tekla Structures.
- Replacing Tekla's drawing engine.
- Custom 2D or 3D drawing engine.
- Custom 3D viewer.
- Web / cloud processing of native Tekla models.
- Automatically solving every special drawing case.
- AI-generated drawing geometry.
- Automatic Engineering approval or production release.

---

## 6. AI Scope

AI is **not required for the initial automation**.

The first priority is deterministic automation and reliable data capture.

Potential future AI areas may include:

- intelligent part-selection assistance,
- grouping / filtering recommendations,
- identifying recurring manual exceptions,
- recommending drawing rules from historical approved decisions.

AI must not silently override Engineering decisions.

---

## 7. Architecture Principles

### Clean separation of responsibilities

The architecture separates:

- UI / presentation,
- application workflow,
- domain models and rules,
- Tekla integration,
- logging / infrastructure,
- future external REST integration.

### Tekla Open API isolation

Tekla-specific APIs should remain isolated under `Infrastructure/Tekla` and behind application-facing interfaces where practical.

### No unnecessary abstraction

Do not introduce repositories, CQRS, mediator frameworks, event buses, databases, message brokers, or similar infrastructure without a real requirement.

---

## 8. Future REST API

The application currently runs locally because Tekla Open API requires access to the local Tekla environment.

Future REST support may be used for:

- centralized configuration,
- reporting,
- job history,
- internal integration,
- remote monitoring.

Do not introduce the REST layer until the requirement is confirmed.

---

## 9. Reliability Requirements

- One failed drawing must not stop the whole batch.
- Existing drawings must not be duplicated accidentally.
- Invalid or unsupported cases should become `NeedReview`.
- Missing configuration should produce a clear error.
- Every processed item should have a traceable result.
- Batch operations should be safely rerunnable.
- Errors should be understandable from logs / result messages.

---

## 10. Engineering Review

The application is an automation tool, not an approval system.

The workflow should make it clear:

- what was created,
- what already existed,
- what failed,
- what needs manual review,
- which settings / rules were applied.

Engineering remains responsible for final review and release.

---

## 11. Development Strategy

### Phase 1 — Connectivity — **Done**

Connect to Tekla, verify the active model, and read model information.

### Phase 2 — Part Reading & Candidate Preparation — **Done**

Read selected parts, extract metadata, validate numbering, and group by `PART_POS`.

### Phase 3 — Core Drawing Processing — **Done for POC**

- Validate candidate readiness.
- Detect existing drawings.
- Dry run.
- Create one drawing.
- Verify rerun / duplicate prevention.

### Phase 4 — Controlled Batch & Traceability — **Done for POC**

Validated behavior includes:

- sequential processing,
- explicit confirmation,
- small controlled batch limit,
- continue-on-error,
- per-candidate result handling,
- cached existing-drawing lookup,
- cache update after successful creation,
- file logging and summary.

### Phase 5 — Drawing Standardization POC — **Waiting for Engineering Input**

Start with **one deterministic drawing family only**.

Required Engineering inputs:

1. Approved Tekla drawing settings.
2. One confirmed drawing family.
3. Before / After drawing references.
4. Mandatory adjustment rules.
5. Exception / `NeedReview` cases.
6. Acceptance criteria.

Do not invent these rules.

### Phase 6 — WPF Application — **Not Started by Design**

Build WPF only after the core workflow and at least one standardization family are proven.

### Phase 7 — Tekla 2022 Validation & Engineering UAT — **Pending**

- Build against the actual Tekla 2022 environment.
- Validate Open API 2022 compatibility.
- Test representative Engineering models.
- Compare results with approved drawings.
- Validate rerun and recovery behavior.

### Phase 8 — Future Integrations

Only after the core workflow is stable:

- REST API support,
- centralized reporting,
- additional integrations,
- AI-assisted improvements.

---

## 12. Important Technical Constraint

The company production environment uses **Tekla Structures 2022**.

The current development machine uses **Tekla Structures 2026**.

Rules:

- Tekla Open API **2022 is the compatibility baseline**.
- Development references may remain on the working Tekla 2026 installation.
- Do not use a Tekla API introduced after 2022 unless explicitly approved.
- Keep Tekla-specific code isolated under `Infrastructure/Tekla`.
- Final production build and testing must use the actual Tekla 2022 environment and matching 2022 API assemblies.
- Do not assume newer Open API assemblies are compatible with older Tekla runtime.
- Do not mix Tekla assemblies from different versions / installations in one build.

---

## 13. Current Known Baseline

The current Console POC has successfully proven:

```text
C# Console Application
        |
        v
Tekla Structures 2026 Development Runtime
        |
        v
Connect to Active Model
        |
        v
Read Selected Physical Parts
        |
        v
Read ID / PART_POS / Profile / Material / Material Type
        |
        v
Check Numbering Status Per Part
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

### Verified capabilities

- Tekla model connection works.
- Active model name and path can be read.
- `Selected` and `All` part-reading modes are supported.
- Selection from Tekla UI works.
- Part metadata includes ID, `PART_POS`, profile, material, material type, and numbering status.
- Numbering readiness is checked per physical model Part.
- Multiple physical parts with the same `PART_POS` are grouped into one candidate.
- Existing drawings are matched by `PART_POS`.
- Existing drawing identities are cached in a case-insensitive `HashSet`.
- Dry-run processing works without creating drawings.
- Single drawing creation with `SinglePartDrawing.Insert()` is validated.
- Rerun prevents accidental duplicates.
- Controlled batch creation has been validated.
- Individual failures do not terminate the batch.
- Cache is updated after successful creation.
- File logs include candidate details and summary counts.
- Tested `STEEL`, `TIMBER`, and `MISCELLANEOUS` examples successfully passed the current creation workflow.
- Tested `CONCRETE` examples are returned as `NeedReview` instead of being forced through `SinglePartDrawing.Insert()`.

### Verified controlled-batch examples

Successful examples include:

- `BP/356`
- `BP/231`
- `BP/260`
- `wp/13`
- `wp/12`
- `filler/1000`
- `BP/451`
- `BP/431`
- `BP/422`

Verified development model:

`Sample Office Building (metric).db1`

### Current state

> **Core processing POC is stable. Drawing Standardization POC is waiting for Engineering input.**

This is **not yet production-ready**. Pending items include:

- approved company drawing settings,
- confirmed standardization rules,
- one proven standardization family,
- WPF,
- Tekla 2022 compatibility build / test,
- Engineering UAT / pilot.

---

## 14. Coding Guidelines for AI Agents

- Understand the Single Part Drawing Automation goal before changing code.
- Make small changes and test each checkpoint.
- Preserve working behavior.
- Keep Tekla-specific code isolated.
- Treat Tekla Open API 2022 as the compatibility baseline.
- Do not guess version-sensitive Tekla API signatures.
- Do not invent Engineering standards.
- Do not move business rules into UI code.
- Do not introduce unnecessary architecture.
- Do not start WPF, REST, database, AI, or broad uncontrolled batch expansion before the current milestone requires it.
- Extend proven code rather than rewriting it unnecessarily.

---

## 15. Immediate Next Target

The immediate target is now:

> **POC: standardize one supported Single Part Drawing family using approved Engineering settings and confirmed deterministic adjustment rules.**

### Current blocker

Engineering input is required before standardization logic is implemented.

Required inputs:

1. Approved Tekla drawing settings / attributes.
2. One drawing family for the first POC.
3. Before / After drawing samples.
4. Mandatory adjustment rules.
5. Exception / `NeedReview` cases.
6. Acceptance criteria.

### Safe work while waiting

- regression testing,
- code cleanup,
- technical documentation,
- backlog / progress updates,
- Tekla 2022 compatibility preparation.

Do not invent drawing-standardization rules or start WPF as a substitute for missing Engineering requirements.

---

## 16. Project Success Definition

The project is successful when an engineer can:

1. Open a supported Tekla model.
2. Start the application.
3. Select the intended parts / scope.
4. Run a dry run and see clear processing states.
5. Run a controlled batch.
6. Create supported Single Part Drawings without accidental duplication.
7. Standardize supported drawings using approved Engineering settings and deterministic rules.
8. See exactly what was created, already existed, failed, or needs review.
9. Review the final result in Tekla before production release.

The project should reduce repetitive drawing preparation without taking away Engineering control.