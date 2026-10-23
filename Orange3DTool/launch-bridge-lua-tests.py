"""Execute the production Lua asset with a small game-boundary fixture (requires lupa)."""
from pathlib import Path
from lupa.lua51 import LuaRuntime


SCRIPT = Path(__file__).with_name("Assets").joinpath("OrangeLaunchBridge.lua").read_text(encoding="utf-8-sig")


def fixture(age=0, kind="server", buddy="present"):
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("""
        Events=setmetatable({}, {__index=function(t,k)
            local event={handlers={}}
            event.Add=function(fn) table.insert(event.handlers,fn) end
            rawset(t,k,event); return event
        end})
        function emit(name,...) for _,fn in ipairs(Events[name].handlers) do fn(...) end end
        MainScreen={instance={delay=500,isVisible=function() return true end}}
        MainScreen.continueLatestSave=function(mode,save) saveArgs={mode,save} end
        buddyAutoOrder=true;buddyCalls=0
        ZombieBuddy={setAutoFixModOrder=function(value) buddyCalls=buddyCalls+1;buddyAutoOrder=value end}
        orderOption={value=true,getValue=function(self) return self.value end,setValue=function(self,value) self.value=value end}
        PZAPI={ModOptions={
            getOptions=function(self,id) if id=='ZombieBuddy' then return {getOption=function(self,id) if id=='autoFixModOrder' then return orderOption end end} end end,
            save=function(self) files['ModOptions.ini']='tickbox|ZombieBuddy|autoFixModOrder|'..tostring(orderOption.value)..'\\n' end
        }}
        Events.OnMainMenuEnter.Add(function() buddyAutoOrder=orderOption.value end)
        ConnectToServer={instance={connect=function(self,...) connectAutoOrder=buddyAutoOrder;connectArgs={...} end}}
        files={}; reads={}; writes={}
        function getTimestamp() return 2000000000 end
        function getFileReader(name,create)
            table.insert(reads,name)
            if not files[name] or files[name]=='' then if create then files[name]='' else return nil end end
            local values={}; for line in files[name]:gmatch('([^\\n]*)\\n') do table.insert(values,(line:gsub('\\r$',''))) end
            local i=0
            return {readLine=function() i=i+1;return values[i] end,close=function() end}
        end
        function getFileWriter(name,create,append)
            local allowed={ini=true,cfg=true,txt=true,log=true,json=true}
            if not allowed[name:match('%.([^.]*)$')] then return nil end
            local text=append and (files[name] or '') or ''
            return {write=function(self,value) text=text..value end,
                close=function() files[name]=text; table.insert(writes,text) end}
        end
    """)
    fields = ["ORANGE1", "a" * 32, str(2000000000 - age), kind, "Sandbox", "probe-save",
              "127.0.0.1", "1", "probe-user", "synthetic-role", "synthetic-server", "0"]
    lua.globals().files["OrangeLauncherSession.request.txt"] = "\n".join(fields) + "\n"
    if buddy == "absent":
        lua.execute("ZombieBuddy=nil")
    elif buddy == "incompatible":
        lua.execute("ZombieBuddy={setAutoFixModOrder=function() error('synthetic policy failure') end}")
    lua.execute(SCRIPT)
    lua.globals().emit("OnMainMenuEnter")
    assert lua.globals().connectArgs is None, "must wait until the menu's initial delay ends"
    lua.globals().MainScreen.instance.delay = 0
    lua.globals().emit("OnRenderTick")
    return lua


def status(lua):
    return lua.globals().files["OrangeLauncherSession.status.txt"]


def run():
    lua = fixture()
    lua.execute("items={size=function() return 2 end,get=function(self,i) return ({'123','456'})[i+1] end};emit('OnServerWorkshopItems','Required',items)")
    assert lua.globals().files['OrangeLauncherWorkshop.txt'] == 'ORANGE_WORKSHOP_1\n' + 'a'*32 + '\n127.0.0.1:1\n2\n123\n456\nEND\n', 'complete Java string list captured with nonce and endpoint'
    assert 'synthetic' not in lua.globals().files['OrangeLauncherWorkshop.txt']
    for size, value in [(0, '123'), (2001, '123'), (1, 'bad'), (1, '0')]:
        lua = fixture()
        lua.globals().bad_size, lua.globals().bad_value = size, value
        lua.execute("emit('OnServerWorkshopItems','Required',{size=function() return bad_size end,get=function(self,i) return bad_value end})")
        assert lua.globals().files['OrangeLauncherWorkshop.txt'] is None
        assert lua.globals().connectArgs is not None
    lua = fixture()
    lua.execute("getFileWriter=function() error('fixture writer failure') end;emit('OnServerWorkshopItems','Required',{size=function() return 1 end,get=function() return '123' end})")
    assert lua.globals().connectArgs is not None
    lua = fixture()
    assert lua.globals().files['OrangeLauncherWorkshop.txt'] is None, 'no event cannot prove an empty server'
    print('PASS: Workshop Required Java-list observer captures bounded complete records; malformed/no-event/writer failures preserve connection')
    lua = fixture()
    args = lua.globals().connectArgs
    assert args is not None, "main-menu render event must submit the request without an in-world OnTick"
    assert lua.globals().connectAutoOrder is False, "server connect must not retain Buddy's local pin-to-front policy"
    assert lua.globals().buddyCalls == 1, "original Buddy order API is applied once before connection"
    assert lua.globals().files["ModOptions.ini"].splitlines() == ["tickbox|ZombieBuddy|autoFixModOrder|false"], "Buddy's saved option must not re-enable automatic order after Lua reload"
    lua.globals().emit("OnMainMenuEnter")
    assert lua.globals().buddyAutoOrder is False, "Buddy menu option reapplication must keep server-authoritative order"
    assert [args[i] for i in range(3, 12)] == ["probe-user", "synthetic-role", "127.0.0.1", "", "1", "synthetic-server", False, True, 1]
    assert status(lua) == "submitted-server\n"
    assert lua.globals().files["OrangeLauncherSession.request.txt"] == ""
    lua.globals().emit("OnConnected")
    lua.globals().emit("OnDisconnect")
    assert status(lua) == "submitted-server\n", "a normal disconnect must not be mislabeled as initial connection failure"
    lua.execute(SCRIPT)
    lua.globals().emit("OnRenderTick")
    assert status(lua) == "submitted-server\n", "Lua reload must not claim an unverified world entry"
    print("PASS: real Lua asset submits on the main-menu event, preserves API arguments, consumes once, and survives reload without false success")

    lua = fixture()
    lua.globals().emit("OnConnectFailed")
    assert status(lua) == "connection-failed\n"
    lua = fixture()
    lua.globals().emit("OnDisconnect")
    assert status(lua) == "connection-failed\n", "pre-world OnDisconnect is also a connection failure in vanilla"
    print("PASS: both vanilla pre-world failure events update launcher status")

    for terminal in ["Disconnected", "Failed", "ClientVersionMismatch"]:
        lua = fixture()
        lua.globals().emit("OnConnectionStateChanged", terminal)
        assert status(lua) == "connection-failed\n", "terminal connection state must not remain submitted"
    print("PASS: vanilla terminal connection-state failures update launcher status")

    lua = fixture(age=180)
    assert status(lua) == "submitted-server\n"
    lua = fixture(age=601)
    assert lua.globals().connectArgs is None
    assert status(lua) == "expired-or-invalid-request\n"
    assert lua.globals().files["OrangeLauncherSession.request.txt"] == ""
    print("PASS: slow startup remains supported and expired requests are consumed without connecting")

    lua = fixture(kind="save")
    assert list(lua.globals().saveArgs.values()) == ["Sandbox", "probe-save"]
    assert status(lua) == "submitted-save\n"
    assert lua.globals().buddyCalls == 0, "single-player launch must not change the multiplayer order policy"
    print("PASS: single-player target remains intact without claiming unverified world entry")

    for buddy in ["absent", "incompatible"]:
        lua = fixture(buddy=buddy)
        assert lua.globals().connectArgs is not None, "missing optional order API must not block original game connection"
        assert status(lua) == "submitted-server\n"
    print("PASS: original Buddy order preference is applied before connect without blocking non-Buddy clients")


if __name__ == "__main__":
    run()
