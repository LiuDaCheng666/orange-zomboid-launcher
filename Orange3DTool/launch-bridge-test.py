import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent.parent/'Dependencies/Python'))
from lupa.lua51 import LuaRuntime
script=Path(__file__).parent/'Assets/OrangeLaunchBridge.lua'
assert script.is_file(), 'one-shot launch bridge is not implemented'
def run(lines,ready=True,throw=False):
    lua=LuaRuntime(unpack_returned_tuples=True)
    lua.execute('''calls={};writes={};ticks={}; menus={}; now=1000; visible=true; fail=false
function getTimestamp() return now end
function getFileWriter(name) return {write=function(self,s) writes[name]=s end,close=function() end} end
function getFileReader(name) local n=0; return {readLine=function() n=n+1;return lines[n] end,close=function() end} end
Events=setmetatable({OnMainMenuEnter={Add=function(f) menus[#menus+1]=f end},OnTick={Add=function(f) ticks[#ticks+1]=f end}}, {__index=function(t,k) local event={Add=function(f) end};rawset(t,k,event);return event end})
MainScreen={instance={delay=0,isVisible=function() return visible end},continueLatestSave=function(mode,save) if fail then error('secret not to log') end calls.save={mode,save} end}
ConnectToServer={instance={connect=function(self,...) if fail then error('secret not to log') end calls.server={...} end}}
''')
    lua.globals().lines=lua.table_from(lines)
    lua.globals().visible=ready;lua.globals().fail=throw
    lua.execute(script.read_text(encoding='utf-8'))
    for _ in range(2):lua.execute('for _,f in ipairs(ticks) do f() end')
    return lua
base=['ORANGE1','a'*32,'990','save','Sandbox','Save One','','0','','','','0']
lua=run(base)
assert list(lua.globals().calls.save.values())==['Sandbox','Save One']
assert lua.globals().writes['OrangeLauncherSession.request.txt']==''
assert lua.globals().writes['OrangeLauncherSession.status.txt']=='submitted-save\n'
server=['ORANGE1','a'*32,'990','server','','','127.0.0.1','16261','player','character-secret','server-secret','0']
lua=run(server)
args=list(lua.globals().calls.server.values())
assert args[2:9]==['player','character-secret','127.0.0.1','','16261','server-secret',False],args
assert lua.globals().writes['OrangeLauncherSession.request.txt']==''
assert lua.globals().writes['OrangeLauncherSession.status.txt']=='submitted-server\n'
expired=base.copy();expired[2]='1';lua=run(expired);assert lua.globals().calls.save is None
assert lua.globals().writes['OrangeLauncherSession.request.txt']==''
lua=run(base,False);assert lua.globals().calls.save is None
lua=run(base,True,True);assert 'secret' not in lua.globals().writes['OrangeLauncherSession.status.txt']
malformed=base.copy();malformed[0]='BAD';lua=run(malformed);assert lua.globals().calls.save is None
malformed=base.copy();malformed[5]='../escape';lua=run(malformed);assert lua.globals().calls.save is None
print('PASS: actual Lua bridge save/connect arguments, one-shot scrub, expiry, menu readiness, error secrecy and traversal guard')
