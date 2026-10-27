# Player feedback follow-up

Only one server feedback receipt was available during the read-only review: an older 1.2.27 report about Buddy takeover and save deletion. The user confirmed there were no additional receipt IDs to locate. Raw report/log contents stay in gitignored private artifacts and are not committed.

The new snapshot screenshot was independently traced to `SaveEnvironments.CaptureSources`: author order violations threw before inventory, and the error plan's uninitialized numeric fields were displayed as zero. The fix and actual isolated copy/UI evidence are recorded in `2026-10-27-snapshot-original-order.md`.

Read-only local same-mod metadata corroborated the order edge: item3779324070, `2DimensionWardrobeBalance`, declares `loadModAfter` including `42_VSGirlBodyNSFW`. The body ID is in item3640496116. This explains the launcher's warning rule; the player's original save/list files were unavailable, so their exact file order was not independently inspected. Author ordering does not justify preventing an original-content snapshot.

## Current-version checks of historical bugs

- `pwsh -NoProfile -File tests/BuddyTakeover.test.ps1 -EvidenceDirectory artifacts/player-feedback-20261027/current-buddy-tests`: exit0, `BUDDY TAKEOVER PASSED`. Includes native inspector observing a real owned fixture process while ActiveProcess fields clear, then process exit and completed cleanup/recovery. `RegOverridePredefKey` redirects only the test process's registry view; the user's Steam registry/session is not changed. Different nonzero account/PID, birth reuse, changed installation and replacement process remain distinguishable. No live Steam shutdown was performed.
- `pwsh -NoProfile -File tests/SaveDeletion.test.ps1 -EvidenceDirectory E:/AppCaches/Temp/ozl-feedback-delete-20261027`: exit0, 73 assertions. Includes actual permanent deletion of isolated directories and public-controller deletion while unrelated Buddy/profile recovery records exist. It verifies unrelated save/default/server bytes remain. This is native filesystem/controller evidence, not a new UI click acceptance claim.
- An initial deletion run used the deep worktree artifacts path and failed its nested launch-journal fixture with .NET `PathTooLongException` after earlier deletion assertions passed. The unchanged suite was rerun with an explicitly short owned test root; this was not an AV-detection workaround. No production source change was made for that test-path issue.

Both compilations retain four known CS0649 warnings. The checks do not establish real player's Steam lifecycle or actual game startup. No game, foreground, real save, original Workshop file, website or deployment was modified. Historical log failures are not presented as new current defects without reproduction.
