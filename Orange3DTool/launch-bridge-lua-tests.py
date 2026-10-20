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
        ConnectToServer={instance={connect=function(self,...) connectAutoOrder=buddyAutoOrder;connectArgs={...} end}}
        files={}; reads={}; writes={}
        function getTimestamp() return 2000000000 end
        function getFileReader(name)
            table.insert(reads,name)
            if not files[name] or files[name]=='' then return nil end
            local values={}; for line in files[name]:gmatch('([^\\n]*)\\n') do table.insert(values,line) end
            local i=0
            return {readLine=function() i=i+1;return values[i] end,close=function() end}
        end
        function getFileWriter(name)
            local allowed={ini=true,cfg=true,txt=true,log=true,json=true}
            if not allowed[name:match('%.([^.]*)$')] then return nil end
            local text=''
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
    args = lua.globals().connectArgs
    assert args is not None, "main-menu render event must submit the request without an in-world OnTick"
    assert lua.globals().connectAutoOrder is False, "server connect must not retain Buddy's local pin-to-front policy"
    assert lua.globals().buddyCalls == 1, "original Buddy order API is applied once before connection"
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
