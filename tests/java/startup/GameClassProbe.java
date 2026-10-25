package ozl.startup.tests;
import ozl.startup.StartupRuntime;
/** Loads and verifies actual 42.21 classes without initializing them, so this
 * check cannot create a game window or start Steam. */
public final class GameClassProbe {
    static Class<?> type(String name)throws Exception{return Class.forName(name,false,ClassLoader.getSystemClassLoader());}
    static void methods(String name,String...names)throws Exception{Class<?> value=type(name);for(String method:names)value.getDeclaredMethod(method);}
    static void fields(String name,Class<?> expected,String...names)throws Exception{Class<?> value=type(name);for(String field:names)if(value.getDeclaredField(field).getType()!=expected)throw new AssertionError("Changed observation field: "+name+"."+field);}
    public static void main(String[] args)throws Exception {
        String[] names={"zombie.GameWindow","zombie.ZomboidFileSystem","zombie.Lua.LuaManager","zombie.gameStates.MainScreenState","zombie.gameStates.GameLoadingState","org.lwjglx.opengl.Display","zombie.core.opengl.RenderThread","zombie.core.Translator","zombie.network.GameClient","zombie.gameStates.ConnectToServerState","zombie.gameStates.LoadingQueueState","zombie.Lua.LuaEventManager","zombie.core.textures.TextureDraw$GenericDrawer","zombie.core.SpriteRenderer","zombie.ui.UIManager","zombie.core.Core","zombie.GameTime","zombie.network.ServerOptions"};
        if(StartupRuntime.snapshot()==null)throw new AssertionError("No accepted agent session");
        for(String name:names){
            Class<?> type=Class.forName(name,false,ClassLoader.getSystemClassLoader());
            type.getDeclaredMethods();
            if(!StartupRuntime.snapshot().active)throw new AssertionError("Actual game transformation failed: "+name);
        }
        Class.forName(names[0],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("DoLoadingText",String.class);
        Class.forName(names[1],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("loadMod",String.class);
        Class.forName(names[5],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("swapBuffers");
        Class<?> translator=Class.forName(names[7],false,ClassLoader.getSystemClassLoader());
        int targets=0;
        for(java.lang.reflect.Method method:translator.getDeclaredMethods()){
            Class<?>[] parameters=method.getParameterTypes();
            if(method.getName().equals("getText")&&parameters.length>0&&parameters[0]==String.class&&method.getReturnType()==String.class)targets++;
        }
        if(targets==0||translator.getDeclaredMethod("getText",String.class,Object[].class).getReturnType()!=String.class)throw new AssertionError("Actual Translator getText target absent");
        methods("zombie.GameWindow","mainThreadInit","init","initFonts");methods("zombie.ZomboidFileSystem","init");type("zombie.ZomboidFileSystem").getDeclaredMethod("searchFolders",java.io.File.class);
        methods("zombie.gameStates.MainScreenState","enter","render","renderBackground");
        methods("zombie.gameStates.GameLoadingState","enter","render","update","exit","Done","SendDone");
        methods("zombie.gameStates.ConnectToServerState","enter","update","exit");type("zombie.gameStates.ConnectToServerState").getDeclaredMethod("FromLua",String.class);
        methods("zombie.gameStates.LoadingQueueState","enter","render","update","redirectState","onConnectionImmediate");type("zombie.gameStates.LoadingQueueState").getDeclaredMethod("onPlaceInQueue",int.class,java.util.HashMap.class);
        Class<?> client=type("zombie.network.GameClient");client.getDeclaredMethod("doConnect",String.class,String.class,String.class,String.class,String.class,String.class,String.class,boolean.class,int.class,String.class);
        client.getDeclaredMethod("doConnectCoop",String.class);client.getDeclaredMethod("disconnect",boolean.class);client.getDeclaredMethod("doDisconnect",String.class);client.getDeclaredMethod("GameLoadingRequestData");client.getDeclaredMethod("setRequest",type("zombie.network.GameClient$RequestState"));
        for(int count=0;count<=8;count++){Class<?>[] arguments=new Class<?>[count+1];java.util.Arrays.fill(arguments,Object.class);arguments[0]=String.class;if(type("zombie.Lua.LuaEventManager").getDeclaredMethod("triggerEvent",arguments).getReturnType()!=void.class)throw new AssertionError("Wrong event return");}
        Class<?> drawer=type("zombie.core.textures.TextureDraw$GenericDrawer");drawer.getConstructor();java.lang.reflect.Method render=drawer.getDeclaredMethod("render");
        if(!java.lang.reflect.Modifier.isPublic(drawer.getModifiers())||!java.lang.reflect.Modifier.isAbstract(render.getModifiers())||render.getReturnType()!=void.class||java.lang.reflect.Modifier.isAbstract(drawer.getDeclaredMethod("render",type("zombie.core.textures.TextureDraw")).getModifiers())||java.lang.reflect.Modifier.isAbstract(drawer.getDeclaredMethod("postRender").getModifiers()))throw new AssertionError("Unsafe drawer lifecycle API");
        if(type("zombie.core.SpriteRenderer").getMethod("drawGeneric",drawer).getReturnType()!=type("zombie.core.textures.TextureDraw"))throw new AssertionError("Wrong queue API");type("zombie.core.SpriteRenderer").getField("instance");type("zombie.ui.UIManager").getField("useUiFbo");
        Class<?> element=type("zombie.ui.UIElement"),texture=type("zombie.core.textures.Texture"),table=type("se.krka.kahlua.vm.KahluaTable");
        if(element.getMethod("getTable").getReturnType()!=table||element.getMethod("getControls").getReturnType()!=java.util.ArrayList.class||element.getMethod("isVisible").getReturnType()!=Boolean.class)throw new AssertionError("Native UI identity API changed");
        table.getMethod("rawget",Object.class);if(type("zombie.ui.UIManager").getMethod("tableget",table,Object.class).getReturnType()!=Object.class||type("zombie.ui.UIManager").getMethod("getUI").getReturnType()!=java.util.ArrayList.class||type("zombie.Lua.LuaManager").getField("env").getType()!=table)throw new AssertionError("Native environment API changed");
        element.getMethod("DrawTextureScaledColor",texture,Double.class,Double.class,Double.class,Double.class,Double.class,Double.class,Double.class,Double.class);
        element.getMethod("DrawTextureColor",texture,double.class,double.class,double.class,double.class,double.class,double.class);
        element.getMethod("DrawTextureScaledAspect3",texture,double.class,double.class,double.class,double.class,double.class,double.class,double.class,double.class);
        type("zombie.ui.LoadingQueueUI").getDeclaredMethod("render");if(type("zombie.gameStates.LoadingQueueState").getDeclaredField("ui").getType()!=type("zombie.ui.LoadingQueueUI"))throw new AssertionError("Native queue identity changed");methods("zombie.core.Core","EndFrameUI");
        fields("zombie.GameWindow",boolean.class,"serverDisconnected");fields("zombie.gameStates.GameLoadingState",boolean.class,"worldVersionError","unexpectedError","playerWrongIP","done","convertingWorld");fields("zombie.gameStates.GameLoadingState",int.class,"convertingFileCount","convertingFileMax");fields("zombie.gameStates.GameLoadingState",String.class,"gameLoadingString");
        fields("zombie.network.GameClient",boolean.class,"client");fields("zombie.network.GameClient",String.class,"gameMap");fields("zombie.network.GameClient",java.util.ArrayList.class,"serverMods");fields("zombie.core.Core",String.class,"gameSaveWorld","gameMode","gameMap");fields("zombie.gameStates.LoadingQueueState",boolean.class,"cancel","done");
        fields("zombie.network.GameClient",String.class,"ip");fields("zombie.network.GameClient",int.class,"port");
        Class<?> reader=type("zombie.core.network.ByteBufferReader");type("zombie.gameStates.ConnectToServerState").getDeclaredMethod("receiveGameTime",reader);type("zombie.gameStates.ConnectToServerState").getDeclaredMethod("receiveServerOptions",reader);
        fields("zombie.GameTime",type("zombie.GameTime"),"instance");for(String method:new String[]{"getYear","getMonth","getDay","getHour","getMinutes"})if(type("zombie.GameTime").getMethod(method).getReturnType()!=int.class)throw new AssertionError("Changed game clock getter");if(type("zombie.GameTime").getMethod("getWorldAgeHours").getReturnType()!=double.class||type("zombie.network.ServerOptions").getMethod("getMaxPlayers").getReturnType()!=int.class)throw new AssertionError("Changed server time/options API");
        if(!StartupRuntime.snapshot().active||StartupRuntime.snapshot().epoch!=1)throw new AssertionError("Verification initialized game operations");
        System.out.println("PASS "+names.length+" actual game classes, exact hook/queue/lifecycle/event APIs, no class initialization/window");
    }
}
