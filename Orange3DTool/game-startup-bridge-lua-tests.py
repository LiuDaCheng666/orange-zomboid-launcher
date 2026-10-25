"""Run the real one-shot Lua bridge against startup readiness boundaries."""
import importlib.util
from pathlib import Path
spec = importlib.util.spec_from_file_location("bridge_existing", Path(__file__).with_name("launch-bridge-lua-tests.py"))
bridge = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bridge)
original = bridge.SCRIPT
MARKER = "[OZL-STARTUP] menu-ready " + "a" * 32


def fixture(prefix="", **kwargs):
    bridge.SCRIPT = "printed={};print=function(value) table.insert(printed,value) end;" + prefix + original
    return bridge.fixture(**kwargs)


def output(lua):
    return list(lua.globals().printed.values())


def run():
    lua = fixture("ConnectToServer=nil;", kind="normal")
    assert output(lua) == [MARKER], "normal Steam startup must report menu-ready without multiplayer UI dependency"
    assert lua.globals().connectArgs is None and lua.globals().saveArgs is None
    assert lua.globals().files["OrangeLauncherSession.request.txt"] == ""
    lua.execute("emit('OnRenderTick');emit('OnMainMenuEnter');emit('OnTick')")
    lua.execute(original)
    lua.execute("emit('OnRenderTick')")
    assert output(lua) == [MARKER], "readiness must be one-shot across ticks and Lua reload"
    assert all("synthetic" not in line for line in output(lua))
    print("PASS: normal menu-ready marker is nonce-bound, one-shot, credential-free and never starts a target")
    for prefix,release in [
        ("MainScreen.instance.isVisible=function() return false end;", "MainScreen.instance.isVisible=function() return true end"),
        ("MainScreen.instance.termsOfServiceDialog={isVisible=function() return true end};", "MainScreen.instance.termsOfServiceDialog=nil"),
    ]:
        lua = fixture(prefix, kind="normal")
        assert output(lua) == [], "hidden/terms-covered menu cannot be ready"
        lua.execute(release + ";emit('OnRenderTick')")
        assert output(lua) == [MARKER]
    print("PASS: hidden and terms-covered menus wait for real visibility")
    for age in [-1,601]:
        lua = fixture(kind="normal", age=age)
        assert output(lua) == [], "future or expired request cannot report readiness"
    for nonce in ["A"*32,"b"*31,"b"*33]:
        lua = fixture("files['OrangeLauncherSession.request.txt']=files['OrangeLauncherSession.request.txt']:gsub('" + "a"*32 + "','"+nonce+"');",kind="normal")
        assert output(lua) == [], "malformed nonce cannot report readiness"
    lua = fixture("files['OrangeLauncherSession.request.txt']=files['OrangeLauncherSession.request.txt']..'extra\\n';",kind="normal")
    assert output(lua) == []
    lua = fixture(kind="unknown")
    assert output(lua) == [], "unsupported targets cannot report readiness"
    print("PASS: TTL, nonce, payload length and target validation gate readiness")
    for kind in ["save","server"]:
        lua = fixture(kind=kind)
        assert output(lua) == [MARKER]
        assert (lua.globals().saveArgs is not None) == (kind=="save")
        assert (lua.globals().connectArgs is not None) == (kind=="server")
    print("PASS: save and server entry retain original target submission after readiness")

if __name__ == '__main__':
    run()
