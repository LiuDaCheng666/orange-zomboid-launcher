"""Run the shipped bridge against installed B42 ModOptions/Buddy Lua, without a game window.

PYTHONPATH=Dependencies/Python python tests/launch-bridge-modoptions-native.py \
  --game-root <ProjectZomboid> --buddy-root <workshop item>/mods/ZombieBuddy
All option files and events below are isolated in Lua memory, never the player's files.
"""
import argparse
from pathlib import Path

from lupa.lua51 import LuaRuntime


def run(game_root, buddy_root):
    mod_options = (game_root / "media/lua/client/PZAPI/ModOptions.lua").read_text(encoding="utf-8-sig")
    buddy_options = (buddy_root / "42/media/lua/client/ZombieBuddy_Options.lua").read_text(encoding="utf-8-sig")
    bridge = Path(__file__).parents[1].joinpath("Orange3DTool/Assets/OrangeLaunchBridge.lua").read_text(encoding="utf-8-sig")
    original = ("tickbox|ZombieBuddy|autoFixModOrder|true\r\n"
                "slider|ZombieBuddy|watermarkOpacity|0.65\r\n"
                "tickbox|ZombieBuddy|fixApprovalDialogCursor|false\r\n"
                "tickbox|UnloadedModA|enabled|true\r\n"
                "textentry|UnloadedModB|label|其他模组的原始值\r\n")

    def boot(content, kind="server", include_bridge=True, fail_write=""):
        lua = LuaRuntime(unpack_returned_tuples=True)
        lua.execute("""
            files={};writes={};diagnostics={};autoOrder=true;calls=0
            function print(value) diagnostics[#diagnostics+1]=value end
            function getText(value) return value end
            luautils={split=function(value,sep)
                local out={};local start=1
                while true do local at=value:find(sep,start,true)
                    if not at then out[#out+1]=value:sub(start);return out end
                    out[#out+1]=value:sub(start,at-1);start=at+#sep
                end
            end}
            Events=setmetatable({}, {__index=function(t,k)
                local event={handlers={}};event.Add=function(fn) event.handlers[#event.handlers+1]=fn end
                rawset(t,k,event);return event
            end})
            function emit(name,...) for _,fn in ipairs(Events[name].handlers) do fn(...) end end
            function getFileReader(name,create)
                if not files[name] then if create then files[name]='' else return nil end end
                local text=files[name];local cursor=1
                return {readLine=function()
                    if cursor>#text then return nil end
                    local at=text:find('\\n',cursor,true)
                    local line=at and text:sub(cursor,at-1) or text:sub(cursor)
                    cursor=at and at+1 or #text+1;return (line:gsub('\\r$',''))
                end,close=function() end}
            end
            function getFileWriter(name,create,append)
                local failure=name=='ModOptions.ini' and failOptionWrite or ''
                if failure=='open' then return nil end
                if not append then files[name]='' end
                files[name]=files[name] or ''
                local pending=''
                return {write=function(self,value)
                    if failure=='close' then pending=pending..value
                    else files[name]=files[name]..(failure=='partial' and value:sub(1,12) or value) end
                end,close=function()
                    if failure=='close' then files[name]=files[name]..pending:sub(1,12) end
                    writes[name]=(writes[name] or 0)+1
                end}
            end
            function getTimestamp() return 2000000000 end
            ZombieBuddy={setAutoFixModOrder=function(value) autoOrder=value;calls=calls+1 end}
            MainScreen={instance={delay=500,isVisible=function() return true end},continueLatestSave=function() end}
            ConnectToServer={instance={connect=function(self,...) connected=true;orderAtConnect=autoOrder end}}
        """)
        lua.globals().failOptionWrite = fail_write
        lua.globals().files["ModOptions.ini"] = content
        fields = ["ORANGE1", "a" * 32, "2000000000", kind, "Sandbox", "fixture", "127.0.0.1", "1", "synthetic", "", "", "0"]
        lua.globals().files["OrangeLauncherSession.request.txt"] = "\n".join(fields) + "\n"
        lua.execute(mod_options)
        lua.execute(buddy_options)
        lua.execute("PZAPI.ModOptions:load()")
        if include_bridge:
            lua.execute(bridge)
        lua.globals().emit("OnMainMenuEnter")
        lua.globals().MainScreen.instance.delay = 0
        if include_bridge:
            lua.globals().emit("OnRenderTick")
        return lua

    lua = boot(original)
    assert lua.globals().connected is True and lua.globals().orderAtConnect is False
    saved = lua.globals().files["ModOptions.ini"]
    assert saved.startswith(original), "native file open/write must not truncate or rewrite any existing option bytes"
    assert saved.splitlines()[-1] == "tickbox|ZombieBuddy|autoFixModOrder|false", "a final override must disable the effective saved option"
    reloaded = boot(saved, include_bridge=False)
    assert reloaded.globals().autoOrder is False, "the actual original Buddy menu handler must not re-enable order after a full Lua reload"
    print("PASS: installed original ModOptions/Buddy Lua reload keeps server ordering disabled and preserves unrelated loaded/unloaded options")

    already = boot(saved)
    assert already.globals().writes["ModOptions.ini"] is None and already.globals().files["ModOptions.ini"] == saved
    print("PASS: an already-disabled option is not rewritten")

    single = boot(original, kind="save")
    assert single.globals().files["ModOptions.ini"] == original and single.globals().autoOrder is True
    print("PASS: single-player launch does not change the Buddy option or saved settings")

    failed = boot(original, fail_write="open")
    assert failed.globals().connected is True and failed.globals().orderAtConnect is False
    assert failed.globals().files["ModOptions.ini"] == original
    print("PASS: option-write failure still attempts runtime policy and original connection, with no new launch gate")

    for failure in ["partial", "close"]:
        failed = boot(original, fail_write=failure)
        assert failed.globals().connected is True and failed.globals().orderAtConnect is False
        assert failed.globals().files["ModOptions.ini"].startswith(original), "partial/flush failure must preserve all original option bytes"
        assert any("saved order option unavailable" in entry for entry in failed.globals().diagnostics.values()), "silent native PrintWriter failure must be detected by readback"
    print("PASS: silent partial-write and close/flush failures retain the complete old options and report failed persistence")

    empty = boot("")
    assert empty.globals().files["ModOptions.ini"].splitlines() == ["tickbox|ZombieBuddy|autoFixModOrder|false"]
    duplicate = boot(original + "tickbox|ZombieBuddy|autoFixModOrder|true\r\n")
    after_duplicate = boot(duplicate.globals().files["ModOptions.ini"], include_bridge=False)
    assert after_duplicate.globals().autoOrder is False
    print("PASS: first-use and duplicate option rows cannot restore the old automatic order value")

    unterminated = boot(original.rstrip("\r\n"))
    fresh = boot(unterminated.globals().files["ModOptions.ini"], include_bridge=False)
    assert fresh.globals().autoOrder is False
    assert unterminated.globals().files["ModOptions.ini"].startswith(original.rstrip("\r\n"))
    print("PASS: appending after an unterminated final option preserves its bytes and applies a separate Buddy override")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--game-root", required=True, type=Path)
    parser.add_argument("--buddy-root", required=True, type=Path)
    args = parser.parse_args()
    run(args.game_root, args.buddy_root)
