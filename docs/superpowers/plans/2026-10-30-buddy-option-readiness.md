# Buddy option readiness repair

**Goal:** Direct server connection must not deliberately throw a Lua error when Buddy's Java API is present before its Lua settings register. Keep the existing server-authoritative order across ResetLua.

**Design:** Reuse `serverOrder()`. Optional settings UI can be absent; update it only when available, then use the existing non-truncating persisted override and original Buddy runtime API. Do not wait for registration, gate connection, skip Java approval, or change UI/installation/configuration strategies. Preserve genuine persistence/runtime failure reporting and the original native connection arguments. Merely returning early on missing UI is rejected because reload could restore the old order; waiting for registration is rejected because it would delay connection.

**Files:** `Orange3DTool/Assets/OrangeLaunchBridge.lua`; extend the existing installed-ModOptions/Buddy Lua integration test; add a narrow actual Kahlua regression for missing registration.

- [x] Reproduce missing UI API, missing Buddy option group, missing option and ready option. Assert no artificial Lua error, runtime order disabled, old INI bytes retained and final override applied.
- [x] Preserve real installed B42 ModOptions and Buddy Lua execution; reload saved data and verify order remains disabled, including delayed registration. Keep failure, single-player and one-shot tests.
- [x] Implement the optional lookup branch and continue existing persistence/runtime handling. No new task state, background wait or launch restriction.
- [x] Run affected tests, inspect diff, record limitations. Do not modify the installed player files or publish another release as part of this repair-method turn.
