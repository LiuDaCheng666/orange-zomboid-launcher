# Server translation size hotfix — 2026-10-26

## Cause

The 1.2.24 server connection path builds temporary text-only translation overlays before writing the launch session or sending the game request. `ServerLaunchTranslations.CopyTexts` rejected any individual file over 4 MiB. The actual enabled Workshop pack `3556540080` has `Sandbox.json` at 4,737,772 bytes and `IG_UI.json` at 4,208,750 bytes. These ordinary inputs therefore throw the exact screenshot error before connection submission.

## Change

The initial unpublished 1.2.25 candidate raised the per-file bound to 16 MiB. It was superseded after the user's request to remove size-based launch gating; no 1.2.25 feed or GitHub release was activated.

1.2.26 removes the per-file limit entirely. The optional derived overlay uses a 64 MiB aggregate memory budget and the existing ownership-compatible file-count budget. If those optional-copy budgets are exceeded, the entire derived set is discarded before any file is staged. Existing selected translation IDs remain enabled and normal source-mod loading handles them; server staging continues. Only the private capacity signal is caught. Reparse, I/O, source-change and ownership failures retain their existing behavior. No incomplete derived translation mod is emitted. Website/server configuration and actual player files were not changed by the tests.

## Targeted verification

- Before implementation, `ServerTranslationBoundsTests` failed with the exact `IOException` from the screenshot while staging a normal current-sized translation file.
- Nine synthetic checks cover full byte-preserving staging/recovery, unchanged sources/defaults, complete acceptance above 16 MiB, successful server staging above the optional-copy budget with the original enabled translation, exact recovery, and discarding a partial earlier overlay if a later source exhausts the budget.
- The actual-source read-only run prepared both local translation packs: 137 files, 21,792,164 bytes, largest 4,737,772 bytes. The actual Sandbox hash matched, and synthetic target paths were never created.
- Existing `ServerLaunchPolicyTests`: 24 checks pass, including source order, common/version merge boundaries, enabled/disabled policy, owned cleanup and player edits.
- Existing `ServerProfilesTests`: 60 checks pass, including actual loopback UDP query/challenge, timeout/backoff and credential boundaries.
- Headless server-related experience UI: 11 groups pass, including server-card launch, target changes and query failure routing.
- Final independent re-review passed after rerunning the nine synthetic and three actual-source checks; no release-blocking issue remains. It confirmed selected source-ID retention, whole-set fallback, bounded allocation arithmetic and narrow exception handling.

Evidence is in ignored `artifacts/server-translation-20261026/`; publication receipts will be in `artifacts/publication-1.2.26/`. The real-source check reads public local translation files; it does not start Steam/game, write player files or test a live server join.
