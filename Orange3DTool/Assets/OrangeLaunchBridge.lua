-- Owned one-shot launcher bridge. Never approves Java mods or bypasses game checks.
local finished=false
local sessionKind=nil
local function write(name,value)
    local out=getFileWriter(name,true,false)
    if not out then error('bridge file unavailable') end
    out:write(value);out:close()
end
local function status(value) pcall(write,'OrangeLauncherSession.status.txt',value..'\n') end
local function safe(value,limit)
    return type(value)=='string' and #value<=limit and not value:find('[%z\1-\31\127]')
end
local function segment(value)
    return safe(value,256) and value~='' and value~='.' and value~='..' and not value:find('[\\/:]')
end
local function tick()
    if finished then return end
    local menu=MainScreen and MainScreen.instance
    if not menu or not menu.isVisible or not menu:isVisible() or (menu.delay and menu.delay>0) then return end
    if menu.termsOfServiceDialog and menu.termsOfServiceDialog:isVisible() then return end
    if not ConnectToServer or not ConnectToServer.instance then return end
    local reader=getFileReader('OrangeLauncherSession.request.txt',false)
    if not reader then finished=true;return end
    local lines={}
    for i=1,12 do lines[i]=reader:readLine() end
    local excess=reader:readLine();reader:close()
    finished=true
    local scrubbed=pcall(write,'OrangeLauncherSession.request.txt','')
    if not scrubbed then status('failed-request-cleanup');return end
    local timestamp=tonumber(lines[3]);local now=getTimestamp()
    local valid=lines[1]=='ORANGE1' and safe(lines[2],32) and #lines[2]==32 and lines[2]:match('^[a-f0-9]+$') and timestamp and now>=timestamp and now-timestamp<=600 and not excess
    for i=4,12 do valid=valid and safe(lines[i],(i==10 or i==11) and 4096 or 1024) end
    if not valid then status('expired-or-invalid-request');return end
    local ok=false
    if lines[4]=='save' and segment(lines[5]) and segment(lines[6]) and lines[5]~='Multiplayer' and MainScreen.continueLatestSave then
        sessionKind='save'
        status('submitted-save')
        ok=pcall(MainScreen.continueLatestSave,lines[5],lines[6])
        if not ok then sessionKind=nil;status('failed-save-manual-entry-required') end
    elseif lines[4]=='server' and safe(lines[7],256) and lines[7]~='' and tonumber(lines[8]) and tonumber(lines[8])>=1 and tonumber(lines[8])<=65535 and lines[9]~='' and (lines[12]=='0' or lines[12]=='1') then
        -- Original Buddy preference: do not pin local core IDs ahead of the server list.
        -- This API persists that order preference, but does not approve JARs or disable checksums.
        if ZombieBuddy and ZombieBuddy.setAutoFixModOrder then
            local ordered=pcall(function() ZombieBuddy.setAutoFixModOrder(false) end)
            if not ordered then print('[OZL] Buddy order preference unavailable; original game connection continues.') end
        end
        sessionKind='server'
        status('submitted-server')
        ok=pcall(function()
            ConnectToServer.instance:connect(menu,'',lines[9],lines[10],lines[7],'',lines[8],lines[11],lines[12]=='1',true,1)
        end)
        if not ok then sessionKind=nil;status('failed-server-manual-entry-required') end
    else status('invalid-target-manual-entry-required') end
    lines=nil
end
Events.OnTick.Add(tick)
Events.OnMainMenuEnter.Add(tick)
-- OnTick does not run in the main menu; its initial delay ends after menu entry.
Events.OnRenderTick.Add(tick)
Events.OnConnected.Add(function()
    sessionKind=nil
end)
local function connectionFailed()
    if sessionKind=='server' then status('connection-failed');sessionKind=nil end
end
Events.OnConnectFailed.Add(connectionFailed)
Events.OnDisconnect.Add(connectionFailed)
Events.OnConnectionStateChanged.Add(function(state)
    if state=='Disconnected' or state=='Failed' or state=='ClientVersionMismatch' then connectionFailed() end
end)
