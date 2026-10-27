import java.lang.instrument.Instrumentation;
import java.lang.reflect.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.io.*;
import java.util.*;
import com.sun.tools.attach.VirtualMachine;

/** Test-only observer and one legal click-to-start for a strictly owned cloned game. */
public final class SaveSnapshotRealWorldProbe {
    static ClassLoader loader;
    static Object field(Class<?> type,Object object,String name)throws Exception { Field f=type.getDeclaredField(name);f.setAccessible(true);return f.get(object); }
    static Class<?> type(String name)throws Exception{return Class.forName(name,false,loader);}
    static Object call(Class<?> type,Object object,String name)throws Exception{return type.getMethod(name).invoke(object);}
    static String quote(Object value){if(value==null)return "null";if(value instanceof Number||value instanceof Boolean)return value.toString();return "\""+value.toString().replace("\\","\\\\").replace("\"","\\\"").replace("\n","\\n").replace("\r","\\r")+"\"";}
    static synchronized void write(Properties p,Map<String,Object> row)throws Exception {row.put("utc",java.time.Instant.now().toString());StringJoiner json=new StringJoiner(",","{","}\n");for(Map.Entry<String,Object> e:row.entrySet())json.add(quote(e.getKey())+":"+quote(e.getValue()));Files.writeString(Path.of(p.getProperty("output")),json.toString(),StandardCharsets.UTF_8,StandardOpenOption.CREATE,StandardOpenOption.APPEND);}
    static Object current()throws Exception{Object states=field(type("zombie.GameWindow"),null,"states");return field(states.getClass(),states,"current");}
    static Map<String,Object> state()throws Exception{
        Map<String,Object> row=new LinkedHashMap<>();Object state=current();String name=state==null?"null":state.getClass().getName();row.put("state",name);row.put("isIngameState",call(type("zombie.GameWindow"),null,"isIngameState"));
        row.put("save",field(type("zombie.core.Core"),null,"gameSaveWorld"));row.put("mode",field(type("zombie.core.Core"),null,"gameMode"));row.put("user",call(type("zombie.core.Core"),null,"getMyDocumentFolder"));
        if(name.equals("zombie.gameStates.GameLoadingState")){for(String key:new String[]{"done","playerCreated","showedClickToSkip","unexpectedError","worldVersionError","playerWrongIP","waitForAssetLoadingToFinish1","waitForAssetLoadingToFinish2","forceDone"})row.put(key,field(state.getClass(),state,key));Thread loading=(Thread)field(state.getClass(),null,"loader");row.put("loaderAlive",loading!=null&&loading.isAlive());}
        Object player=call(type("zombie.characters.IsoPlayer"),null,"getInstance");row.put("playerPresent",player!=null);if(player!=null){row.put("playerX",call(player.getClass(),player,"getX"));row.put("playerY",call(player.getClass(),player,"getY"));row.put("playerZ",call(player.getClass(),player,"getZ"));row.put("playerSquarePresent",call(player.getClass(),player,"getSquare")!=null);}
        return row;
    }
    static void queue(Runnable run)throws Exception{type("zombie.MainThread").getMethod("queueInvokeOnMainThread",Runnable.class).invoke(null,run);}
    static void failure(Properties p,Throwable error){try{Map<String,Object> row=new LinkedHashMap<>();row.put("action","probe-error");row.put("error",error.toString());write(p,row);}catch(Exception ignored){}}
    public static void agentmain(String config,Instrumentation instrumentation)throws Exception{
        Properties p=new Properties();try(Reader input=Files.newBufferedReader(Path.of(config),StandardCharsets.UTF_8)){p.load(input);}
        Path game=Path.of(p.getProperty("game")).toRealPath(),user=Path.of(p.getProperty("user")).toRealPath();if(!Files.isSameFile(game,Path.of(System.getProperty("user.dir"))))throw new SecurityException("Probe game working directory differs");
        Class<?> window=null;for(Class<?> value:instrumentation.getAllLoadedClasses())if(value.getName().equals("zombie.GameWindow")){window=value;break;}if(window==null)throw new SecurityException("Actual game class is not loaded");loader=window.getClassLoader();
        if(!Files.isSameFile(game.resolve("projectzomboid.jar"),Path.of(window.getProtectionDomain().getCodeSource().getLocation().toURI())))throw new SecurityException("Game class is outside exact snapshot");
        if(!Files.isSameFile(user,Path.of((String)call(type("zombie.core.Core"),null,"getMyDocumentFolder"))))throw new SecurityException("Game cachedir is outside exact cloned user");
        if((Boolean)field(type("zombie.network.GameClient"),null,"client"))throw new SecurityException("Probe only accepts this single-player session");
        String save=p.getProperty("save"),mode=p.getProperty("mode");if(!save.equals(field(type("zombie.core.Core"),null,"gameSaveWorld"))||!mode.equals(field(type("zombie.core.Core"),null,"gameMode")))throw new SecurityException("Game selected save differs");
        final String action=p.getProperty("action","probe");
        queue(()->{try{Map<String,Object> row=state();row.put("action",action+"-before");write(p,row);
            if(action.equals("capture")){
                if(!Boolean.TRUE.equals(row.get("isIngameState"))||!Boolean.TRUE.equals(row.get("playerPresent")))throw new IllegalStateException("Real capture requires native IngameState and player");
                String name=p.getProperty("capture");if(name==null||!name.matches("SaveSnapshotReal-[A-Za-z0-9-]+\\.png"))throw new IllegalArgumentException("Owned screenshot filename required");
                Object fs=field(type("zombie.ZomboidFileSystem"),null,"instance");Path folder=Path.of((String)call(fs.getClass(),fs,"getScreenshotDir")).toAbsolutePath().normalize();if(!folder.startsWith(user))throw new SecurityException("Screenshot is outside exact clone");
                Object core=call(type("zombie.core.Core"),null,"getInstance");core.getClass().getMethod("TakeFullScreenshot",String.class).invoke(core,name);Map<String,Object> capture=new LinkedHashMap<>();capture.put("action","native-screenshot-api");capture.put("path",folder.resolve(name));capture.put("exists",Files.isRegularFile(folder.resolve(name)));write(p,capture);
            }else if(action.equals("saveexit")){
                if(!Boolean.TRUE.equals(row.get("isIngameState"))||!Boolean.TRUE.equals(row.get("playerPresent")))throw new IllegalStateException("Real save/exit requires native IngameState and a real player");
                call(type("zombie.Lua.LuaManager$GlobalObject"),null,"saveGame");Map<String,Object> saved=state();saved.put("action","native-save-complete");write(p,saved);
                Map<String,Object> exiting=new LinkedHashMap<>();exiting.put("action","native-quitToDesktop-api");write(p,exiting);Object core=call(type("zombie.core.Core"),null,"getInstance");call(core.getClass(),core,"quitToDesktop");
            }else if(action.equals("click")){
                Object current=current();if(!current.getClass().getName().equals("zombie.gameStates.GameLoadingState"))throw new IllegalStateException("Click requires actual loading state");
                for(String key:new String[]{"done","playerCreated","showedClickToSkip"})if(!Boolean.TRUE.equals(row.get(key)))throw new IllegalStateException("Native click gate is not ready: "+key);
                for(String key:new String[]{"unexpectedError","worldVersionError","playerWrongIP","waitForAssetLoadingToFinish1","waitForAssetLoadingToFinish2","loaderAlive"})if(!Boolean.FALSE.equals(row.get(key)))throw new IllegalStateException("Native loading is not finished: "+key);
                // This is the exact transient flag set by Mouse.isButtonDown(0) in
                // GameLoadingState.update. Never force a game state or ready result.
                Field click=current.getClass().getDeclaredField("forceDone");click.setAccessible(true);click.setBoolean(current,true);Map<String,Object> sent=new LinkedHashMap<>();sent.put("action","legal-click-flag");sent.put("testHelperBoundary","Only the real completed loading state's pending click flag; all transitions remain native");write(p,sent);
            }
        }catch(Throwable error){failure(p,error);}});
        Thread observe=new Thread(()->{for(int i=0;i<50;i++){try{Thread.sleep(1000);final int second=i+1;queue(()->{try{Map<String,Object> row=state();row.put("action","observe");row.put("seconds",second);write(p,row);}catch(Throwable error){failure(p,error);}});}catch(Throwable error){failure(p,error);return;}}},"Ozl-owned-save-real-observer");observe.setDaemon(true);observe.start();
    }
    public static void main(String[] args)throws Exception{if(args.length!=3)throw new IllegalArgumentException("pid agent.jar config.properties");VirtualMachine vm=VirtualMachine.attach(args[0]);try{vm.loadAgent(args[1],args[2]);}finally{vm.detach();}}
}
