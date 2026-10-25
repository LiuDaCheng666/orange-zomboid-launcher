-- Owned one-shot launcher bridge. Never approves Java mods or bypasses game checks.
local finished=false
local sessionKind=nil
local captureNonce=nil
local captureEndpoint=nil
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
-- Vanilla applies the last recognized option row. Read the effective value and
-- append an override, never truncate the player's unrelated mod settings.
local function savedBuddyOrder()
    local reader=getFileReader('ModOptions.ini',true)
    if not reader then error('mod options unavailable') end
    local value=nil;local bytes=0;local count=0
    local ok,reason=pcall(function()
        while true do
            local line=reader:readLine()
            if line==nil then break end
            bytes=bytes+#line+2
            count=count+1
            if bytes>1048576 or count>16384 then error('mod options too large') end
            local setting=line:match('^tickbox|ZombieBuddy|autoFixModOrder|([^|]+)')
            if setting=='true' or setting=='false' then value=setting end
        end
    end)
    reader:close()
    if not ok then error(reason) end
    return value,count>0
end
local function persistBuddyOrder()
    local value,hasRows=savedBuddyOrder()
    if value=='false' then return end
    local out=getFileWriter('ModOptions.ini',true,true)
    if not out then error('mod options writer unavailable') end
    local wrote=pcall(function() out:write((hasRows and '\r\n' or '')..'tickbox|ZombieBuddy|autoFixModOrder|false\r\n') end)
    local closed=pcall(function() out:close() end)
    if not wrote or not closed or savedBuddyOrder()~='false' then error('mod options persistence not confirmed') end
end
local function serverOrder()
    if not ZombieBuddy or not ZombieBuddy.setAutoFixModOrder then return end
    local synced=pcall(function()
        local api=PZAPI and PZAPI.ModOptions
        local options=api and api:getOptions('ZombieBuddy')
        local option=options and options:getOption('autoFixModOrder')
        if not option or not option.setValue then error('Buddy option unavailable') end
        option:setValue(false)
        persistBuddyOrder()
    end)
    if not synced then print('[OZL] Buddy saved order option unavailable; runtime preference will still be attempted.') end
    local ordered=pcall(function() ZombieBuddy.setAutoFixModOrder(false) end)
    if not ordered then print('[OZL] Buddy order preference unavailable; original game connection continues.') end
end
local inputNonce=nil
local function inputRequired()
    local reader=getFileReader('OrangeLauncherSession.request.txt',false)
    if not reader then return end
    local protocol=reader:readLine();local nonce=reader:readLine();local timestamp=tonumber(reader:readLine());reader:close()
    local now=getTimestamp()
    if protocol=='ORANGE1' and safe(nonce,32) and #nonce==32 and nonce:match('^[a-f0-9]+$') and timestamp and now>=timestamp and now-timestamp<=600 and inputNonce~=nonce then
        inputNonce=nonce;print('[OZL-STARTUP] input-required '..nonce)
    end
end
local function tick()
    if finished then return end
    local menu=MainScreen and MainScreen.instance
    if not menu or not menu.isVisible or not menu:isVisible() or (menu.delay and menu.delay>0) then return end
    if menu.termsOfServiceDialog and menu.termsOfServiceDialog:isVisible() then inputRequired();return end
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
    if lines[4]=='normal' then
        print('[OZL-STARTUP] menu-ready '..lines[2])
        status('menu-ready')
    elseif lines[4]=='save' and segment(lines[5]) and segment(lines[6]) and lines[5]~='Multiplayer' and MainScreen.continueLatestSave then
        sessionKind='save'
        status('submitted-save')
        ok=pcall(MainScreen.continueLatestSave,lines[5],lines[6])
        if not ok then sessionKind=nil;status('failed-save-manual-entry-required') end
        print('[OZL-STARTUP] '..(ok and 'target-submitted ' or 'target-failed ')..lines[2])
    elseif lines[4]=='server' and safe(lines[7],256) and lines[7]~='' and tonumber(lines[8]) and tonumber(lines[8])>=1 and tonumber(lines[8])<=65535 and lines[9]~='' and (lines[12]=='0' or lines[12]=='1') then
        -- Original Buddy preference: do not pin local core IDs ahead of the server list.
        -- This API persists that order preference, but does not approve JARs or disable checksums.
        serverOrder()
        sessionKind='server'
        captureNonce=lines[2]
        captureEndpoint=lines[7]..':'..lines[8]
        status('submitted-server')
        ok=pcall(function()
            -- Native connect hides previousScreen; its MainScreen parent must stay visible.
            ConnectToServer.instance:connect(menu.bottomPanel,'',lines[9],lines[10],lines[7],'',lines[8],lines[11],lines[12]=='1',true,1)
        end)
        if not ok then sessionKind=nil;status('failed-server-manual-entry-required') end
        print('[OZL-STARTUP] '..(ok and 'target-submitted ' or 'target-failed ')..lines[2])
    else status('invalid-target-manual-entry-required') end
    lines=nil
end
Events.OnTick.Add(tick)
if Events.OnTickEvenPaused and Events.OnTickEvenPaused.Add then Events.OnTickEvenPaused.Add(tick) end
-- Observe the original Required event only; vanilla confirmation remains untouched.
if Events.OnServerWorkshopItems and Events.OnServerWorkshopItems.Add then
    Events.OnServerWorkshopItems.Add(function(kind,items)
        if kind~='Required' or sessionKind~='server' or not captureNonce then return end
        pcall(function()
            local count=items:size()
            if type(count)~='number' or count<1 or count>2000 or count~=math.floor(count) then return end
            local rows={'ORANGE_WORKSHOP_1',captureNonce,captureEndpoint,tostring(count)}
            local seen={}
            for i=1,count do
                local id=items:get(i-1)
                if type(id)~='string' or #id>20 or not id:match('^[1-9][0-9]*$') or seen[id] then return end
                seen[id]=true;rows[#rows+1]=id
            end
            rows[#rows+1]='END'
            local record=table.concat(rows,'\n')..'\n'
            if #record<=65536 then write('OrangeLauncherWorkshop.txt',record) end
        end)
    end)
end
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
