# Three launch configurations

## User decisions

The homepage's current-configuration selector becomes 启动配置. Its choices are 启动器配置 (default), Steam 配置 and 原版配置. The user explicitly retains ordinary 接管 Steam 配置 and 强制接管 as optional Steam-mode actions; launcher mode must not require either. One confirmation precedes force takeover. This design records the ongoing conversation's decisions, not a claim that the modes already exist.

Launcher mode owns its startup arguments, Buddy configuration, Java approvals, enabled-component selection and effective mod discovery. Steam options, original game JSON/BAT and original Java component declarations do not control it and are not silently rewritten. Ordinary mod lists and component enablement remain independent. Vanilla mode temporarily excludes ordinary mods and Java components while retaining original settings for the next launch. Existing frozen snapshots retain their own manifest and must not be repurposed as live contexts.

## Runtime and state

Introduce an explicit per-game/user launcher context below Data/launcher-configurations/<stable key>. Keep Engine.Game and User as the real selected game and save/cache directory for process identity, native resources and saves. Context-owned config/default lists/runtime payload paths are separate properties. Persist the selected mode as one of launcher, steam, vanilla; old installations default to launcher. Engine constructors used by legacy tests remain Steam-style unless the production controller explicitly binds the selected mode, so existing frozen/maintenance contracts do not silently change.

The context maintains component source selection/enabled state separately from the ordinary default list. First initialization may import the currently enabled known components read-only, without requiring old agents to parse successfully. Normal configurations reference the selected current Workshop/local component roots read-only; only the small launcher runtime/Buddy payload needs owned deployment. They never modify original Workshop metadata. Component-center operations and authorization must use the same context that the next launch consumes.

The user's later clarification unifies ordinary named configurations and snapshots under the homepage 启动配置 selector. A normal named configuration chooses installed game version, component enablement and mod order, following ordinary game/Workshop updates. A snapshot configuration selects copied game/mod versions; its mod order and selection can be edited independently. Adding a mod to a snapshot copies its current files into that snapshot, so later Workshop changes cannot change it. Editing one configuration does not alter other configurations or original Workshop/save lists. Snapshot game version stays pinned; save progress continues in its existing save unless separately backed up. Do not create a second conflicting snapshot-only editor/navigation concept.

## Startup transport

Launcher and vanilla use the selected game's jre64/bin/java.exe, explicit stock main/classpath/platform/native options, owned memory settings, real game working directory, explicit -cachedir and Steam app IDs. They do not execute original BAT or consume arbitrary original VM agents/options. Resolve stock required JAR/native paths against the actual game. Do not accept modified original vmArgs as the baseline. Remove injected Java-option environment variables from the child only.

The real background probe established Buddy 2.3.4 premain outside game root. Pass Unicode configuration through -Dzb.config_dir=<owned directory> and agent option prop_prefix=zb. Passing a Chinese config_dir directly in agent options corrupted the path. A separate Unicode agent-JAR-path probe is required. Buddy's owned config has no global preload or trusted authors, disables auto_fix_mod_order, and denies unapproved Java. Explicit component approval is hash-bound; do not import broad author trust. Unknown external Java must not become active just because its Lua root remains visible.

A dedicated live-discovery agent contract (separate from frozen manifests) selects exactly one explicit source for managed Java identities, excludes their duplicate alternatives, retains unrelated Lua mods, and redirects only the owned mod-choice files. An exact source/hash execution list ensures only enabled selected Java components run; unknown Java does not become enabled through a game's broad policy. Normal configurations resolve current source bytes, while snapshot configurations use only their copied roots. Vanilla exposes only any temporary launcher bridge required for a chosen save/server target, and no Buddy/optional Java component. Startup presentation remains an optional owned agent and can be disabled without disabling discovery isolation.

## Entry points and recovery

Normal, save and server targets all use the selected mode. Launcher/vanilla explicit processes record PID and birth; they do not inherit the old Steam 180-second no-PID grace. Controller restart reconstructs the same context. Force close keeps exact selected-game JRE/main/PID-birth classification and never expands to all Java processes. Exit/cancel/failure release leases and remove only session-owned artifacts. Choosing a mode, browsing pages and reading status are not blocked by unrelated tasks.

Steam mode keeps current behavior and optional takeover actions. Force takeover clears all current-account App108600 Steam launch options after one confirmation; only this explicit action may change those options. It reports partial cleanup truthfully for an unhandled legacy source. It must not require players in launcher mode to fix those unrelated files before playing.

## UI

Keep the existing homepage layout. Put the built-in and user-created configurations in the existing current-configuration position, titled 启动配置. Launcher/Steam/vanilla are built-in choices; named normal and snapshot configurations appear alongside them with a clear type/version. Show a short description only for the selected configuration, and actual selected memory/components beside it. The selected configuration's edit action manages its version, components, mods and order in one place; snapshot additions explicitly copy local bytes with visible copy progress. Launch result text uses user-facing stage/error names; no raw JSON, duplicate status banners or internal validation inventory. Steam's takeover buttons appear only beside Steam configuration, with a single explicit force-confirmation describing cleared options and Java-loading entries. Never display an enabled-looking mode whose backend is absent.

## Verification and boundaries

Targeted RED/GREEN fixtures cover polluted/malformed original JSON/BAT/Steam options, stock argument generation/Unicode paths, child environment, original-byte preservation, independent components/approvals/duplicates, ordinary mod no-op, all targets and PID recovery. Actual DOM clicks call production controller/native file operations against owned data; inspect screenshots and final files for all three choices, success/failure/cancel and force confirmation.

Actual game acceptance must use the built candidate's production entry and exact parameters: menu, Lua/Java source provenance, save/save-exit/relaunch, multiplayer, restart recovery and force stop. Keep original settings hashed unchanged in launcher/vanilla mode. Background JVM probes and synthetic UI are explicitly not actual game acceptance. No publication before required acceptance; preserve the user's foreground and real data. This plan does not modify website/server configuration or publish a release.
