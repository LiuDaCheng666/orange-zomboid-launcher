# Game discovery and launch classification repair

User scope: repair automatic game-folder identification after the loading refinement, without requiring evidence the remote player no longer has. Work stays hidden; no launcher, game, Steam SDK or foreground interaction. The delivered candidate4 remains untouched.

## Evidence and decision

Read-only reproductions are in `artifacts/game-discovery-20261026/discovery-research.md` and `launch-compat-research.md`. Normal modern/legacy Steam libraries, custom install names and Unicode already work. Missing registry roots and stale/unlisted libraries can leave discovery empty. A separate checker treats one supported branch-local Buddy loader as a cleanup requirement, and counts ordinary properties containing `zbNative` as loaders. The photographs do not establish the remote machine's exact cause.

Keep Steam manifest discovery as the first automatic source and add a finite conventional fallback. A full recursive disk scan would slow startup and is excluded. Manual selection also accepts a Steam/library/common parent and resolves its specific game installation. Existing valid saved choices remain preferred. Missing `version.txt` remains an honest unknown display version and does not invalidate a complete game folder; no Java/runtime/log version guessing.

## Discovery contract

- Pure `Find(string[] roots)` remains deterministic, with no registry or environment reads. Normal multi-library/custom-manifest behavior remains supported.
- Automatic startup combines registry roots with conventional Program Files Steam locations and immediate `Steam`/`SteamLibrary` children of ready local fixed drives. No UNC, removable-drive or recursive scan is introduced by fallback.
- Bound metadata reads (library VDF 1 MiB, app manifest 256 KiB), distinct roots (128), and elapsed work (2 seconds checked between local file operations). Never claim an OS file read has a hard timeout. Skip malformed, inaccessible, oversized and reparse inputs and continue to another valid candidate.
- Reject manifest folder components `.`, `..`, rooted paths, separators, invalid Windows characters, trailing dots/spaces, and any canonical result outside the direct `steamapps/common` parent.
- A bounded saved-path reader handles missing, locked, malformed, oversized and stale preference files without crashing startup. A valid direct saved game folder wins.
- Manual selection first accepts the exact game root (or exact `ProjectZomboid64.exe` passed to the resolver), then resolves Steam/library/steamapps/common parents using the same bounded manifest rules. A wrong folder still receives a short actionable error. Do not enumerate arbitrary child directories.
- Both modern and legacy startup consume the same discovery/preference helper. The modern folder action preserves network, transaction and path safeguards and only persists a successfully resolved game root.

## Launch classification contract

Launch validation is separate from the explicit cleanup rewrite. Preserve duplicate-key/type/syntax checks and refusal of unknown custom agent options. Accept zero or one exact canonical `-agentlib:zbNative` across the existing traversed root/Windows argument structure, including a single branch-local occurrence. Multiple occurrences remain conservatively refused because the native launcher's alternative-branch combination contract has not been established. Recognized noncanonical legacy Buddy agents remain a repair case.

Count actual Buddy arguments through the shared classifier, never arbitrary substrings in JVM properties. Keep required Buddy files, owned startup-agent checks, unknown Java-agent checks, selected-mod ambiguity, dependency/order checks, pending transaction guards and Steam identity/byte ownership requirements.

When JSON fails, the dependent guard row says that configuration must be fixed first and is marked skipped/warning; it must not imply Steam was independently checked and failed. Ordinary launch is still blocked by the configuration error. Do not broaden or rewrite Steam/takeover behavior.

## Verification and delivery

Fixture tests must demonstrate the old failures before fixes, then cover normal and fallback discovery, preference and parent selection, malformed boundaries, valid branch-local native loader, harmless properties, true duplicates/custom agents and skipped-guard status. All engines/controllers use temporary fixture paths with fake Steam evidence. Run the native regression suite and relevant frontend contract tests; the Java loading payload is unchanged, so verify its hash rather than repeat unchanged Java suites. Fresh candidate5 embeds the current Java payload and freshly built UI bundle, with resource/archive hash checks. Do not publish, bump versions, contact servers or modify website/deploy files in this repair.
