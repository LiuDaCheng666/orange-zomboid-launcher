# Quiet launch and runtime-independent save deletion

**Goal:** Address the two screenshots without adding runtime restrictions: allow explicit recycling of the selected local save while a client/server/Java process exists; do not ask for warning-only launch confirmation or expand a healthy preflight over the home cover.

**Architecture:** Remove the process predicate from SaveDeletion and its UI eligibility, keeping exact-target confirmation, recycle-only behavior and real I/O errors. Retain launch preflight, but continue immediately on `canLaunch`, put passing reports in collapsed details, and auto-expand only blocked reports. Advisory Java information never grants authorization. Metadata messages remain available in details, not a reason for extra confirmation.

**Constraints:** Existing checkout and standing release authorization; do not alter a live save, launcher draft, server, client, Steam, or Java approvals. Tests use owned fixtures. Do not redesign unrelated pages or change duplicate-launch policy.

- [x] Reproduce both behaviors with focused backend and real-DOM browser tests before production edits.
- [x] Remove deletion runtime predicate and indirect active-session gate from `SaveDeletion.cs`, `ModernController.cs` and `saveDeleteBlocked` in `Web/app.js`; update obsolete runtime-refusal assertions, preserving cancellation and exact target/recycle semantics.
- [x] Remove warning-only `confirm` in `launchExperience`; render successful preflight as collapsed detail below the launch layout, keeping blocking details and automatic sort visible.
- [x] Verify warning-only repeated launches submit directly with no modal; inspect details/manual expansion, real blockers, both themes and narrow layouts. Verify runtime-independent confirmed delete and truthful recycle failure.
- [ ] Run native/UI regression, review changes, bump 1.2.8 and build signed installer/ZIP. Publish source and all22 assets only after package verification; verify official downloads and old1.2.7 update code.
