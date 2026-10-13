-- Owned one-shot launcher bridge. Never approves Java mods or bypasses game checks.
local finished=false
local function write(name,value)
    local out=getFileWriter(name,true,false)
    if not out then error('bridge file unavailable') end
    out:write(value);out:close()
end
local function status(value) pcall(write,'OrangeLauncherSession.status',value..'\n') end
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
    local reader=getFileReader('OrangeLauncherSession.request',false)
    if not reader then finished=true;return end
    local lines={}
    for i=1,12 do lines[i]=reader:readLine() end
    local excess=reader:readLine();reader:close()
    finished=true
    local scrubbed=pcall(write,'OrangeLauncherSession.request','')
    if not scrubbed then status('failed-request-cleanup');return end
    local timestamp=tonumber(lines[3]);local now=getTimestamp()
    local valid=lines[1]=='ORANGE1' and safe(lines[2],32) and #lines[2]==32 and lines[2]:match('^[a-f0-9]+$') and timestamp and now>=timestamp and now-timestamp<=120 and not excess
    for i=4,12 do valid=valid and safe(lines[i],(i==10 or i==11) and 4096 or 1024) end
    if not valid then status('expired-or-invalid-request');return end
    local ok=false
    if lines[4]=='save' and segment(lines[5]) and segment(lines[6]) and lines[5]~='Multiplayer' and MainScreen.continueLatestSave then
        ok=pcall(MainScreen.continueLatestSave,lines[5],lines[6])
        status(ok and 'submitted-save' or 'failed-save-manual-entry-required')
    elseif lines[4]=='server' and safe(lines[7],256) and lines[7]~='' and tonumber(lines[8]) and tonumber(lines[8])>=1 and tonumber(lines[8])<=65535 and lines[9]~='' and (lines[12]=='0' or lines[12]=='1') then
        ok=pcall(function()
            ConnectToServer.instance:connect(menu,'',lines[9],lines[10],lines[7],'',lines[8],lines[11],lines[12]=='1',true,1)
        end)
        status(ok and 'submitted-server' or 'failed-server-manual-entry-required')
    else status('invalid-target-manual-entry-required') end
    lines=nil
end
Events.OnTick.Add(tick)
Events.OnMainMenuEnter.Add(tick)
