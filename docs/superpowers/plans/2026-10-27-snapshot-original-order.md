# Preserve Original Snapshot Lists

User's 2026-10-27 correction: a snapshot copies the save's current mod list, order and files. Do not require sorting, display irrelevant metadata/order warnings, or silently fix the list. Only actual missing/unreadable/failed-copy files need concrete action. Explicit user-chosen newer-ID replacements/skips remain supported. Work is part of current local changes, not a publication.

## Global Constraints

- Preserve real user saves/game/Workshop/Steam and foreground; isolated test data only. Preserve unrelated dirty website/deploy files and the separate launch-configuration implementation.
- Original save/default list order and bytes remain unchanged; captured explicit mod choices remain consistent. Do not remove missing-file handling/automatic downloads or fake success.
- No order/conflict/author-metadata warning should block copying available original files. No nonexistent statistic rendered as measured zero. Progress/count/cancel and usable finished/error state remain visible.
- Actual native/UI/file operation evidence required; distinguish fixtures from actual game. Do not claim unchanged runtime has newly passed real-game acceptance.

### Task 1: Snapshot copies current order instead of requiring mod repair

Files: SaveEnvironments.cs, focused snapshot tests/native UI harness, Web/app.js and related tests only if needed, docs/quality report.

- [x] RED with available A/B mods stored B,A while A requires/loads before B: preview currently falls into zeros. Include author-declared conflict and cycle cases; these are not reasons to refuse copying original bytes. Verify existing ordered/missing/explicit replacement/skip cases remain defined.
- [x] Remove sort/error/violation validation from snapshot capture. Preserve explicitly captured save/default/preload order; metadata can locate actual files/dependencies, not rewrite lists. Inspect Expand calls and remove implicit list changes where they contradict the original-order requirement; available supplemental dependency files may be copied without silently enabling them. Leave exact missing files and explicit chosen replacements/skips intact.
- [x] Return reliable count/game/mod bytes after completed inventory; on an actual early file/config failure, mark statistics unavailable and UI shows a concrete error/retry rather than 0. Do not blanket-hide actionable file failures. Filter irrelevant author metadata notices from snapshot output while preserving concrete unavailable sources.
- [x] Real DOM click opens preview -> production controller with isolated real available mods in reverse order -> nonzero inventory -> choose/create -> real copied files/manifests and preserved list bytes/order. Cover failure/retry and cancel release. Inspect screenshot. Run only affected snapshot/native/UI tests, commit owned exact files, report RED/GREEN/evidence/boundaries to this plan's task-1-report.md.
