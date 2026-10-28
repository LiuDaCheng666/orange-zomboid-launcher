# Independent version progress and 1.2.37 release

> **For agentic workers:** Use subagent-driven-development for the scoped fix and independent review. Continue through the already authorized release without another approval loop.

**Goal:** Fix the reported apparently frozen independent-version dialog, inspect current feedback, verify the affected operations and publish the existing configuration work plus the fix.

**Architecture:** Retain VersionLibrary's actual independent file copies. Expose the existing copy operation's preparation, transfer and verification lifecycle inside its owning dialog; retain actionable cancellation and release state on every outcome. Use the established software-only publishing pipeline.

**Tech Stack:** C#/.NET Framework, WebView2, JavaScript/CSS, PowerShell, Python, Velopack.

## Global constraints

- User approved scoped repair and publication. No new product strategy, global task gates, invented progress or website redeployment.
- Preserve other tasks' existing website/deployment changes. Use the existing worktree on codex/velopack-updates.
- Only the native picker supplies a destination. Cancel/failure cannot register a partial version or leave controls unavailable.
- Progress has real file/byte counts; unknown totals use an activity indicator. Long scan/copy must be cancellable.
- Snapshot game/mod files remain independent copies; distinguish save-derived and named-configuration-derived ordinary lists. Do not claim reconstruction of a missing old game version or unperformed gameplay tests.
- SSH uses the shared pinned-key helper only. Software publication must not run deploy_site or change current/Web/SSH configuration.
- Explicit unsigned release exception applies: retain RSA validation and report Windows NotSigned honestly.

## Task 1: Reproduce and repair the independent-version workflow

Files: Orange3DTool/Web/app.js, app.css; VersionLibrary.cs and LibraryController.cs only if progress requires native extension; focused tests.

- [ ] Capture a failing DOM/native case: preserve submit leaves both buttons unavailable and no dialog progress while the native scan/copy is pending.
- [ ] Trace picker, native queue, inventory/hash, copy, verification and completion; distinguish observed screenshot from inferred cause.
- [ ] Add the minimal status/progress/cancel lifecycle to the dialog. Explain its single purpose and next action in concise Chinese. Do not broadly refactor other workflows.
- [ ] Verify initial/picker-cancel/scan/copy/verification/cancel/failure/success, real isolated copied bytes and registration, and controls after closing/reopening. Inspect dark/light and narrow screenshots.
- [ ] Independent spec/code review; fix findings and rerun affected tests.

## Task 2: Feedback and snapshot evidence

- [x] Read server feedback through readonly SQLite/file access with the shared SSH helper; retain redacted evidence under artifacts/feedback-audit-20261029.
- [x] Review snapshot capture/runtime/edit paths and existing acceptance evidence; confirm copied game JRE and mod roots are selected independently.
- [ ] Run focused current regressions for discovered feedback paths and version-copy/snapshot isolation; record actual scope and no-game boundary.
- [ ] Preserve unclassified historical observations; do not label them fixed based on unrelated passing tests.

## Task 3: Package and release

Files: VERSION, CHANGELOG.md and scoped quality record; existing build/deploy scripts.

- [ ] Reconfirm next free version, bump to 1.2.37 and write factual user-facing notes.
- [ ] Commit only owned paths, freeze input hashes and build the complete production release with -AllowUnsignedRelease.
- [ ] Verify actual final archives/bootstrap/RSA/21 file inventory and NotSigned evidence. Run affected actual package operations on isolated data and inspect their pages.
- [ ] Publish exactly the ten verified files through publish_software.py, preserving the old update lane and all protected website/configuration bytes.
- [ ] Push annotated source tag and publish the identical ten assets on GitHub; verify anonymous downloaded bytes for both channels.
- [ ] Run publish_changelog.py after both releases; verify new/history entries and protected files unchanged. Report version, repairs, verification boundary and download link.
