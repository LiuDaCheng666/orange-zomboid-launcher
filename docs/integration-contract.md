# Full launcher native/Web contract

All names below are `ModernController` actions. Read actions never modify saves, default mods, approvals or Steam subscriptions. Root owns endpoint registration. All writes reject while actual game runs unless expressly read-only/import configuration. UI API `api(action,payload)` uses existing envelopes. Native strings are rendered as text, not arbitrary HTML.

## Mod workspace
- `modProfiles` => `{stamp,profiles:[{id,name,modCount,updated}],activeMods:[]}`.
- `modProfileGet {id}` => `{id,name,stamp,mods:[]}`.
- `modProfileSave {id?,name,stamp,mods:[]}` => saved profile DTO. Empty id creates. Profile collection stamp always required.
- `modProfileDelete {id,stamp}` => message. Snapshot conflicts refuse.
- `modProfileActivate {id,stamp,confirmed:true}` => message; writes only ordinary default mods block and preserves maps/custom sections. Host game/session guards apply.
- `modSort {mods:[]}` => `{mods:[],warnings:[],errors:[],changed}`. Stable dependency preview only; no write.
- `modCatalog` existing `mods` richer rows includes title/author/version/description/id/item/coverToken/require/loadAfter/loadBefore/conflicts plus existing shapes. `modCover` existing route remains.
- save editor same `saveGet/saveSave`, rich enabled rows come from catalog. Profile can be selected then copied into unsaved save edit, not silently written.

## Steam
Actions mapped to `SteamLauncherServices.Execute` action names connection/subscriptions/search/details/download/hall. Worker writes no secret inputs and lives briefly.
- `steamConnection` => `{available,accountName,steamId,message}`. No auth ticket; Steam owns credentials.
- `steamSubscriptions` => `{items:[{id,title,description,author,previewUrl,updated,subscribed,downloaded,needsUpdate,sizeBytes}],message}`.
- `steamSearch {query,page}` => `{items:[same],page,totalPages,total,message}`.
- `steamDetails {id}` => `{item:{same,dependencies:[]},message}`.
- `steamDownload {id,confirmed:true}` => `{message,id}`; subscribe+download only after explicit click; no automatic activation. Validate app108600 before mutation.
- `serverHall {query?}` => `{servers:[{id,name,host,port,queryPort,players,maxPlayers,ping,description,passwordRequired,steam}],message}`. Id is display key, not personal saved server id. Actual hall result, no dummy fallbacks.
- External preview images can't load directly under current WebView CSP; use local Workshop/catalog cover where known, otherwise neutral native icon. Original source links allowed only via native approved action.

## Favorites/pictures
- `serverFavorites` => `{favorites:[{id,name,host,port,username,hasUserPassword,hasServerPassword,useSteamRelay}],message}`. Never return plaintext password. ID scoped to source DB+row. Import resolves exact current row; secret stays native.
- `favoriteImport {id,confirmed:true}` => message; imported own server profile retains DPAPI protections.
- `saveThumb {id}` / `serverThumb {id}` => `{dataUrl,mime,message}`; null dataUrl when absent/ambiguous. Strict bounded original thumb image bytes and exact save/server-role match. Personal server id only.

## Task center
- `taskGet` => `{active:null|task,history:[task],logPath}`.
- task `{id,action,title,started,finished,status,progress,message,events:[{at,progress,message}]}`. Status running/completed/partial/failed/cancelled/interrupted. `progress=-1` genuinely unknown, measured0..100 only when available. Completed100, failed/partial never forced100.
- Controller emits existing progress event; taskGet snapshots durable state. Persist sanitized field-only events and bounded rolling log; never serialize launch/password payloads.

## Playtime
- `playtimeGet` => `{todaySeconds,totalSeconds,running,sessionSeconds,reminderMinutes,reminderDue,tracking,message}`. Seconds actual sampled game process lifetime, not launcher-open time. Day local timezone. No launch => no automatic unrelated process tracking.
- `playtimeSettings {reminderMinutes}` => message; 0 off or integer15..480. `dismissReminder:true` acknowledges today's current threshold without changing interval. Modal appears once per due threshold (UI does not spam via poll).
- Root starts independent same-owner worker after normal or targeted launch. Starts wait for process max120seconds, verifies game executable path+pid/start before accounting. Heartbeat receipts bounded; don't fabricate offline elapsed after uncertain worker failure.

## UI
11 routes home/components/mods/workshop/saves/servers/shaders/settings/memory/maintenance/help. Compact sidebar visible on1100x760; two accepted themes. All async calls show progress/error/retry; dirty edits survive passive polling. Task center accessible globally. Server personal/favorites/hall tabs with high contrast separated metric labels and values. All real data; fixtures only in tests/capture explicitly identified.
