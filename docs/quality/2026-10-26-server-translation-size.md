# Server translation size hotfix — 2026-10-26

## Cause

The 1.2.24 server connection path builds temporary text-only translation overlays before writing the launch session or sending the game request. `ServerLaunchTranslations.CopyTexts` rejected any individual file over 4 MiB. The actual enabled Workshop pack `3556540080` has `Sandbox.json` at 4,737,772 bytes and `IG_UI.json` at 4,208,750 bytes. These ordinary inputs therefore throw the exact screenshot error before connection submission.

## Change

1.2.25 raises only the per-file bound to 16 MiB. The 64 MiB aggregate bound, file-count/depth limits, reparse checks, exact copy, owned journal validation and recovery rules stay intact. No exception is hidden or ignored. No website, server configuration, game process or real player configuration is changed by this fix.

## Targeted verification

- Before implementation, `ServerTranslationBoundsTests` failed with the exact `IOException` from the screenshot while staging a normal current-sized translation file.
- Six synthetic checks now cover full byte-preserving staging, owned recovery, unchanged source/default bytes, exact 16 MiB acceptance and 16 MiB + 1 rejection before launch-session or player-selection writes.
- The actual-source read-only run prepared both local translation packs: 137 files, 21,792,164 bytes, largest 4,737,772 bytes. The actual Sandbox hash matched, and synthetic target paths were never created.
- Existing `ServerLaunchPolicyTests`: 24 checks pass, including source order, common/version merge boundaries, enabled/disabled policy, owned cleanup and player edits.
- Existing `ServerProfilesTests`: 60 checks pass, including actual loopback UDP query/challenge, timeout/backoff and credential boundaries.
- Headless server-related experience UI: 11 groups pass, including server-card launch, target changes and query failure routing.
- Independent review reran the six synthetic and three actual-source checks and found no release-blocking issue.

Evidence is in ignored `artifacts/server-translation-20261026/`; publication receipts will be in `artifacts/publication-1.2.25/`. The real-source check reads public local translation files; it does not start Steam/game, write player files or test a live server join.
