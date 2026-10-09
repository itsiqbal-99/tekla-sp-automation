# Single Part Drawing Automation — Scope Guard

**Last updated:** 2026-10-08

## Purpose

This document keeps development aligned with the proven Tekla Structures 2022 workflow and prevents unapproved drawing mutations or unnecessary architecture.

## Product boundary

The application is a local WPF companion for an already-running Tekla Structures instance. It does not replace Tekla, its model, Document Manager, or drawing engine.

Engineering remains responsible for final drawing review and release.

## Fixed technical baseline

- Tekla Structures 2022 SP14.
- Tekla Open API assembly version 2022.0.0.0.
- Verified DLL file version 2022.0.36168.0.
- Base, model, and drawing assemblies must come from the same Tekla 2022 SP14 installation and file version.
- .NET Framework 4.8.1, C# 7.3, Windows x64.
- WPF is the active application.
- Core contains application/domain/Tekla integration logic.
- Console source is legacy POC and excluded from the active solution.

Do not use an API introduced after Tekla 2022 without an explicit version-specific design and approval.

## Business identity

- `PART_POS` is the current drawing business identity.
- It is normalized with trim and case-insensitive comparison.
- Multiple physical parts with the same `PART_POS` form one `DrawingCandidate`.
- All physical model identifiers are retained for navigation.
- `RepresentativePartId` is used only where Tekla requires one concrete part.
- Multiple drawings for one normalized `PART_POS` are `NeedReview`, never silently accepted.

## Current supported workflow

1. Authenticate.
2. Validate Tekla connection and active model.
3. Read selected model parts.
4. Preserve missing/invalid parts as visible review results.
5. Group consistent parts by normalized `PART_POS`.
6. Validate numbering and material support.
7. Refresh existing Single Part Drawing inventory.
8. Show drawing state and dry-check results.
9. Allow focus/open navigation into Tekla.
10. Validate the configured standard file.
11. Confirm and run a sequential batch of at most ten drawings.
12. Revalidate model, standard, and duplicates before each insert.
13. Re-query and verify the created drawing.
14. Log and display the result.

## Tekla Open API 2022 usage

Approved current usage includes:

- model selection and highlighting;
- selected-part reading and report properties;
- per-part numbering validation;
- drawing enumeration and `SinglePartDrawing` creation;
- active-drawing detection and safe drawing opening;
- drawing state properties;
- sheet/view enumeration and view scale reading;
- advanced-option path resolution.

The following available APIs remain deliberately disabled:

- `DrawingHandler.UpdateDrawing`;
- drawing `Modify`;
- `PlaceViews`;
- `CommitChanges` for automated standardization;
- issue/unissue, delete, and print operations.

## Drawing standard rule

`SP_TEST_STANDARD` and `SP_TEST_STANDARD.wd` are test configuration only.

Current automated verification is limited to:

- exactly one drawing is found after creation;
- the drawing is readable;
- at least one view exists;
- all readable views use the configured expected scale.

Do not invent production rules for dimensions, marks, annotations, layout, view placement, or exceptions. These require Engineering-owned reference drawings and acceptance criteria.

## Reliability rules

- One candidate failure must not terminate the remaining batch.
- Cancellation takes effect between candidates.
- A final duplicate check is mandatory immediately before insert.
- Connection and model path are checked before each batch item.
- Missing or ambiguous cases become `NeedReview`.
- Raw exceptions go to logs; the UI shows a useful action and operation reference.
- Existing drawings are not modified automatically.
- The application never closes or saves the active Tekla drawing automatically.

## Architecture rule

Prefer incremental extraction from WPF code-behind only when logic needs independent testing or reuse.

Use concrete classes for single implementations. Add an interface or wrapper only when it provides an actual alternate implementation, testing seam, or shared contract.

Do not introduce full MVVM rewrites, DI frameworks, mediator/event-bus patterns, repositories, databases, or generic drawing-rule engines without a demonstrated requirement.

## Authentication rule

- Debug builds may use explicitly configured Demo mode.
- Release builds must use the company HTTPS API and fail closed otherwise.
- Authentication failure never falls back to demo access.
- Roles, offline access, and token persistence are outside the current confirmed contract.

## Out of scope

- CI/CD and distribution.
- Cloud processing of native Tekla models.
- Custom drawing or 3D engines.
- Automatic Engineering approval.
- Bulk update of existing drawings.
- AI-generated geometry or unapproved drawing rules.

## Required validation

Core regression tests must pass without Tekla. Final acceptance additionally requires Tekla 2022 SP14 integration/UAT for connection loss, model switching, focus/open navigation, drawing-state reading, duplicate prevention, sequential creation, cancellation, rerun safety, and post-create scale verification.
