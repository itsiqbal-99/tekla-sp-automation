# Single Part Drawing Automation

## 1. Project Overview

**Single Part Drawing Automation** is an internal Windows desktop application that automates the repetitive process of creating **single-part drawings** from an active **Tekla Structures** model.

The application does **not** replace Tekla Structures, its model, or its drawing engine. Tekla remains the source of model data and the engine responsible for creating and storing drawings. This application provides a controlled automation workflow around Tekla Open API.

### Current development environment

- **Tekla Structures:** 2026
- **Tekla Open API:** 2026
- **Language:** C#
- **Desktop UI target:** WPF
- **Target platform:** Windows x64
- **Current POC type:** Console application
- **Current project:** `SinglePartAutoFix`

The current connection POC has been successfully tested against:

- Tekla process running
- Active Tekla model open
- x64 process
- Tekla Open API connection
- Model name and model path retrieval

---

## 2. Business Problem

The engineering workflow already has the required 3D model in Tekla. The main pain point is the repetitive work required to create many single-part drawings one by one.

A typical manual flow is:

1. Identify the parts that need drawings.
2. Check whether the model is ready and numbered.
3. Select a part.
4. Apply the correct drawing settings / attribute.
5. Create the single-part drawing.
6. Repeat for many parts.
7. Check which drawings were created, skipped, or failed.

The goal of this project is to reduce this repetitive work while keeping Engineering in control of the final drawing review and release.

---

## 3. Project Goal

The primary goal is:

> **Automatically generate valid single-part drawings in batch from an active Tekla Structures model using approved Engineering settings and rules.**

The application should make the workflow:

- faster,
- repeatable,
- traceable,
- maintainable,
- safe to rerun,
- easy to troubleshoot.

Engineering remains responsible for validating the generated drawings before production release.

---

## 4. Target Workflow

```text
Tekla Structures
      |
      v
Check Connection / Model
      |
      v
Read Parts
      |
      v
Define Processing Scope
      |
      v
Validate Model Readiness
(Numbering / Eligibility / Existing Drawings)
      |
      v
Apply Approved Drawing Settings
      |
      v
Generate Single-Part Drawings
      |
      v
Record Results
(Created / Skipped / Failed / Need Review)
      |
      v
Engineering Review
```

---

## 5. MVP Scope

The first production-oriented version should focus only on the core automation problem.

### In scope

- Detect whether Tekla Structures is running.
- Detect whether a Tekla model is open.
- Connect through Tekla Open API.
- Read parts from the active model.
- Process an explicit user-defined scope.
- Read relevant part information such as:
  - identifier,
  - piece mark,
  - profile,
  - material / grade,
  - other drawing-relevant attributes available from the model.
- Check model numbering / readiness.
- Check whether a single-part drawing already exists.
- Map supported part types to approved drawing settings.
- Generate single-part drawings in batch.
- Continue processing when an individual item fails.
- Record processing results.
- Show:
  - Created
  - Skipped
  - Failed
  - Need Review
- Provide simple logs for troubleshooting.
- Keep Engineering review before release.

### Out of scope for MVP

- Replacing Tekla Structures.
- Replacing the Tekla drawing engine.
- Building a custom 3D model viewer.
- Web-based processing of native Tekla models.
- Cloud processing of native Tekla models.
- Automatically solving every special drawing case.
- AI-based drawing generation.
- Automatic production release without Engineering review.

---

## 6. AI Scope

AI is **not required for the initial automation**.

The first priority is deterministic automation and reliable data capture.

Future AI should only be introduced after enough real workflow data exists and only where Engineering still performs repetitive decisions that can benefit from assistance.

Potential future AI areas may include:

- intelligent part-selection assistance,
- grouping / filtering recommendations,
- identifying recurring manual exceptions,
- recommending drawing rules based on historical approved decisions.

AI must not silently override Engineering decisions.

---

## 7. Architecture Principles

The application should be designed for long-term maintainability from the beginning.

### Clean separation of responsibilities

Tekla-specific code must not be scattered across UI code or business logic.

The architecture should separate:

- UI / presentation
- application workflow
- domain models and rules
- Tekla integration
- logging / infrastructure
- future external REST API integration

### Tekla Open API isolation

Tekla Open API is an external, version-sensitive integration point.

Tekla-specific APIs should therefore be isolated behind application-facing interfaces / services.

The rest of the application should depend on abstractions and application models rather than directly depending on Tekla API types wherever practical.

This makes it easier to:

- test business logic,
- maintain the application,
- upgrade Tekla versions,
- troubleshoot API-specific issues,
- support future integrations.

### No unnecessary abstraction

Do not introduce patterns, frameworks, services, repositories, event buses, message brokers, databases, or other infrastructure unless there is a real requirement for them.

The architecture should be clean, but not over-engineered.

---

## 8. External REST API — Future Requirement

The application is currently a local Windows desktop application because Tekla Open API requires access to the local Tekla environment.

However, the architecture should allow a future external REST API without redesigning the entire application.

The intended long-term direction is:

```text
                 +----------------------+
                 |      WPF Desktop     |
                 |      Application      |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 | Application / Core   |
                 | Workflow & Rules     |
                 +----+------------+----+
                      |            |
                      v            v
              +-----------+   +-----------+
              | Tekla     |   | REST API  |
              | Adapter   |   | Client /  |
              |            |   | Integration|
              +-----------+   +-----------+
                      |
                      v
                 Tekla Structures
```

The application should therefore avoid coupling the core workflow directly to WPF or directly to HTTP.

Future REST support may be used for things such as:

- centralized configuration,
- reporting,
- job history,
- integration with other internal systems,
- remote monitoring,
- future engineering workflow integration.

The exact REST architecture should be introduced only when the external integration requirement is confirmed.

---

## 9. Reliability Requirements

The application should behave safely when processing many parts.

### Important rules

- One failed drawing must not stop the complete batch.
- Existing drawings should not be duplicated accidentally.
- Invalid or unsupported cases should become **Need Review** rather than being silently processed.
- Missing configuration should produce a clear error.
- Every processed item should have traceable results.
- Batch operations should be safely rerunnable.
- Errors should be understandable without requiring a developer to inspect the entire Tekla model manually.

---

## 10. Engineering Review

The application is an automation tool, not an automatic approval system.

Generated drawings should remain subject to Engineering review.

The workflow should make it obvious:

- what was generated,
- what was skipped,
- what failed,
- what requires manual review,
- which drawing settings were used.

---

## 11. Development Strategy

Development should be incremental.

### Phase 1 — Connectivity

```text
Connect to Tekla
      |
      v
Verify active model
      |
      v
Read model information
```

### Phase 2 — Read model parts

```text
Active model
      |
      v
Read parts
      |
      v
Extract required metadata
      |
      v
Display / log part information
```

### Phase 3 — Single drawing

```text
Select one part
      |
      v
Validate
      |
      v
Apply approved settings
      |
      v
Create one single-part drawing
      |
      v
Verify result
```

### Phase 4 — Batch generation

```text
Part scope
      |
      v
Pre-check
      |
      v
Batch generation
      |
      v
Created / Skipped / Failed / Need Review
```

### Phase 5 — WPF application

Add a simple desktop interface around the proven workflow.

### Phase 6 — Validation / pilot

Test against representative Engineering models and approved drawings.

### Phase 7 — Future integrations

Only after the core workflow is stable:

- REST API support
- centralized reporting
- additional integrations
- AI-assisted improvements

---

## 12. Important Technical Constraint

The production environment may use a different Tekla Structures version from the current development environment.

The current development baseline is **Tekla Structures 2026**.

When the production Tekla version is confirmed, the application must be tested against the actual production version and its matching Open API assemblies before deployment.

Do not assume that a newer Tekla Open API assembly is automatically compatible with an older production installation.

---

## 13. Current Known Baseline

The current project successfully proves:

```text
C# Console Application
        |
        v
Tekla Open API 2026
        |
        v
Tekla Structures 2026
        |
        v
Active Sample Office Building model
        |
        v
Connection successful
        |
        v
Model name/path successfully read
```

The verified model used during the connection test is:

`Sample Office Building (metric).db1`

---

## 14. Coding Guidelines for AI Agents

When an AI Agent is asked to modify this project, it should follow these rules.

### Understand the goal first

The main objective is **Single Part Drawing Automation**, not generic Tekla development.

Do not add unrelated functionality.

### Preserve the architecture

Prefer small, clear components with one responsibility.

Do not put:

- Tekla API logic inside WPF views,
- business rules inside UI event handlers,
- HTTP logic inside Tekla integration classes.

### Avoid over-engineering

Do not introduce a new framework or architectural pattern merely because it is available.

Prefer the simplest design that supports:

- maintainability,
- testing,
- Tekla integration,
- future REST API support.

### Tekla API safety

- Use the matching Tekla Open API version for the target environment.
- Do not mix Tekla assemblies from different versions.
- Keep Tekla-specific code isolated.
- Do not assume an API method exists in another Tekla version without verification.
- Do not change working dependencies without a clear reason.

### Batch processing safety

Never make one failed item terminate the entire batch unless the failure is a global system / connection failure.

### Engineering rules

Do not invent engineering drawing rules.

If a drawing rule, attribute, exception, or expected output is unknown, treat it as a requirement to confirm rather than guessing.

### Future REST support

Keep application logic independent from the transport layer.

The core workflow should remain usable whether the command originates from:

- WPF UI,
- a future REST endpoint,
- another internal integration.

---

## 15. Immediate Next Target

The immediate development target is:

> **POC: read parts from the active Tekla 2026 model and display a clean summary of the available part information.**

Expected next step:

```text
Tekla connection
      |
      v
Model object selector
      |
      v
Read Part objects
      |
      v
Extract:
- ID
- Piece Mark
- Profile
- Material / Grade
      |
      v
Display count + sample records
```

After this works reliably, the project will move to the first single-part drawing creation test.

---

## 16. Project Success Definition

The project is successful when an engineer can:

1. Open a supported Tekla model.
2. Start the application.
3. Select the intended parts / scope.
4. Run a controlled batch.
5. Have valid single-part drawings generated using approved settings.
6. See exactly what was created, skipped, failed, or needs review.
7. Review the result in Tekla before production release.

The project should reduce repetitive drawing preparation without taking away Engineering control.
