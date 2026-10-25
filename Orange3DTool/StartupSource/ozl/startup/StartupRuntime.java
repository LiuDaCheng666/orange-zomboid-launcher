package ozl.startup;

import java.lang.reflect.Field;
import java.util.ArrayList;
import org.lwjgl.opengl.*;

/** Observations and rendering only: original game operations retain control. */
public final class StartupRuntime {
    private static volatile LoadingModel model;
    private static Session session;
    private static GameRenderer renderer;
    private static long lastSwap,lastIssueRead;
    private static volatile long issueFloor;
    private static boolean drawing,reportedFailure;
    private static final ThreadLocal<String> frame=new ThreadLocal<>();
    private static final NativeConnectionPresentation presentation=new NativeConnectionPresentation();
    private static final ThreadLocal<long[]> cpuFrame=new ThreadLocal<>();
    private static final java.util.concurrent.atomic.AtomicLong frameSequence=new java.util.concurrent.atomic.AtomicLong();
    private static volatile Object connectionOwner;
    private static final ThreadLocal<Object> connectionDispatch=new ThreadLocal<>();
    private static volatile WorkshopInteraction workshopInteraction;
    private static WorkshopDownload download;
    private static volatile boolean workshopRestorePending;
    private static void rejectWorkshop(){presentation.restoreWorkshop();LoadingModel m=model;if(m!=null&&m.snapshot().downloadPage){m.download(false,"","",-1,-1,-1,-1);m.nativeInteraction(true);if(download!=null)download.valid=false;}}
    private static final class WorkshopInteraction {final Object owner;final long generation;volatile boolean success;WorkshopInteraction(Object owner,long generation){this.owner=owner;this.generation=generation;}}
    private static final ServerQuery publicQuery=new ServerQuery((operation,result)->{LoadingModel m=model;if(m!=null)m.publicInfo(operation,result.name,result.map,result.players,result.maximum);});
    public static void begin(Session accepted){publicQuery.cancel();presentation.restoreWorkshop();presentation.clear();download=null;workshopInteraction=null;connectionOwner=null;session=accepted;model=new LoadingModel();issueFloor=0;}
    public static LoadingModel.Snapshot snapshot(){LoadingModel value=model;return value==null?null:value.snapshot();}
    public static void phase(String value){LoadingModel m=model;if(m!=null)m.phase(value);}
    public static void early(String value){LoadingModel m=model;if(m!=null)m.early(value.equals("initFonts")?"fonts":value);}
    public static void resource(java.io.File value){LoadingModel m=model;if(m!=null)m.resource(value,System.nanoTime());}
    public static void translation(String key,String label){LoadingModel m=model;if(m!=null)m.translation(key,label);}
    public static void mod(String value){LoadingModel m=model;if(m!=null)m.mod(value);}
    public static void script(String value){LoadingModel m=model;if(m!=null)m.script(value);}
    public static long generation(){LoadingModel.Snapshot s=snapshot();return s==null?0:s.generation;}
    public static void menuReady(){publicQuery.cancel();LoadingModel m=model;if(m!=null){issueFloor=System.currentTimeMillis();m.menuReady();System.out.println("[OZL-STARTUP-JAVA] game-menu");}}
    public static void serverBegin(String friendly){serverBegin(friendly,"","");}
    public static void serverBegin(String friendly,String host,String port){publicQuery.cancel();presentation.restoreWorkshop();presentation.clear();download=null;workshopInteraction=null;connectionOwner=null;LoadingModel m=model;if(m!=null){issueFloor=System.currentTimeMillis();int parsed=0;try{if(port!=null&&port.length()<8)parsed=Integer.parseInt(port.trim());}catch(NumberFormatException invalid){}m.serverBegin(friendly,host==null?"":host.trim(),parsed);startQuery(m,host,parsed);}}
    private static void startQuery(LoadingModel m,String host,int port){if(host==null||m.snapshot().endpoint.isEmpty())return;String target=host.trim();if(target.startsWith("[")&&target.endsWith("]"))target=target.substring(1,target.length()-1);publicQuery.request(m.snapshot().generation,target,port);}
    public static void coopConnected(Object client){LoadingModel m=model;if(m==null||client==null)return;Class<?> type=client.getClass();String host=string(type,"ip");int port=number(type,null,"port");m.endpoint(m.snapshot().generation,host,port);startQuery(m,host,port);}
    public static void handoff(boolean keep){if(!keep)publicQuery.cancel();presentation.restoreWorkshop();presentation.clear();download=null;workshopInteraction=null;connectionOwner=null;LoadingModel m=model;if(m!=null){issueFloor=System.currentTimeMillis();m.handoff(keep);}}
    public static void queueBegin(){LoadingModel m=model;if(m!=null){m.serverResume();m.queueWaiting();}}
    public static void queue(int position){LoadingModel m=model;if(m!=null)m.queue(position);}
    private static int integer(Object value){if(value instanceof Byte||value instanceof Short||value instanceof Integer||value instanceof Long){long n=((Number)value).longValue();if(n>=Integer.MIN_VALUE&&n<=Integer.MAX_VALUE)return (int)n;}return -1;}
    public static void queueInformation(long operation,int position,java.util.HashMap<?,?> info){LoadingModel m=model;if(m==null||m.snapshot().generation!=operation)return;m.queue(position);if(info!=null)m.queueInformation(operation,integer(info.get("countPlayers")),integer(info.get("maxPlayers")),integer(info.get("Year")),integer(info.get("Month")),integer(info.get("Day")),integer(info.get("Hour")),integer(info.get("Minutes")));}
    public static void serverTime(Object state,long operation){if(state==null)return;try{readClock(state.getClass().getClassLoader(),operation);}catch(ReflectiveOperationException ignored){}}
    private static int getter(Class<?> type,Object value,String method)throws ReflectiveOperationException{return integer(type.getMethod(method).invoke(value));}
    private static void readClock(ClassLoader loader,long operation)throws ReflectiveOperationException{
        LoadingModel m=model;if(m==null||m.snapshot().generation!=operation)return;Class<?> time=gameType("zombie.GameTime",loader);Object value=read(time,null,"instance");if(value==null)return;Object age=time.getMethod("getWorldAgeHours").invoke(value);if(!(age instanceof Double hours))return;
        m.serverClock(operation,getter(time,value,"getYear"),getter(time,value,"getMonth"),getter(time,value,"getDay"),getter(time,value,"getHour"),getter(time,value,"getMinutes"),hours);
    }
    private static void refreshClock(ClassLoader loader)throws ReflectiveOperationException{LoadingModel.Snapshot s=snapshot();if(s!=null&&s.clockObserved)readClock(loader,s.generation);}
    public static void serverOptions(Object state,long operation){LoadingModel m=model;if(m==null||state==null||m.snapshot().generation!=operation)return;try{Class<?> options=gameType("zombie.network.ServerOptions",state.getClass().getClassLoader());Object value=read(options,null,"instance");if(value!=null)m.maximum(operation,getter(options,value,"getMaxPlayers"));}catch(ReflectiveOperationException ignored){}}
    private static Object read(Class<?> type,Object instance,String name){try{Field f=type.getDeclaredField(name);if(!f.canAccess(instance))f.setAccessible(true);return f.get(instance);}catch(ReflectiveOperationException|RuntimeException unavailable){return null;}}
    private static boolean flag(Class<?> type,String name){return Boolean.TRUE.equals(read(type,null,name));}
    private static int number(Class<?> type,Object instance,String name){Object value=read(type,instance,name);return value instanceof Integer count?count:-1;}
    private static String string(Class<?> type,String name){Object value=read(type,null,name);return value instanceof String text?text:"";}
    private static int listSize(Object value){return value instanceof ArrayList<?> list?list.size():-1;}
    private static Class<?> gameType(String name,ClassLoader loader)throws ClassNotFoundException{return Class.forName(name,false,loader);}
    public static void worldBegin(Object state){
        LoadingModel m=model;if(m==null||state==null)return;issueFloor=System.currentTimeMillis();
        try{ClassLoader loader=state.getClass().getClassLoader();Class<?> client=gameType("zombie.network.GameClient",loader),core=gameType("zombie.core.Core",loader);Object mode=read(client,null,"client");if(!(mode instanceof Boolean server)){m.handoff(false);return;}
            // The multiplayer save field can encode address/user IDs: never read it for servers.
            if(!server)publicQuery.cancel();m.worldBegin(server,server?"":string(core,"gameSaveWorld"),server?"":string(core,"gameMode"),server?string(client,"gameMap"):string(core,"gameMap"));refreshServer(loader);refreshClock(loader);
        }catch(ReflectiveOperationException unavailable){handoff(false);}
    }
    public static void worldEnd(){publicQuery.cancel();LoadingModel m=model;if(m!=null)m.worldEnd();}
    /** Multiplayer SendDone sends a request; only the original done flag proves completion. */
    public static void worldSent(Class<?> type){Object done=read(type,null,"done");if(!(done instanceof Boolean completed)||completed)worldEnd();}
    public static void worldFrame(Object state){
        LoadingModel m=model;if(m==null||state==null||!m.snapshot().world)return;
        Class<?> type=state.getClass();
        for(String guard:new String[]{"worldVersionError","unexpectedError","playerWrongIP","done"}){Object value=read(type,null,guard);if(!(value instanceof Boolean enabled)||enabled){handoff(false);return;}}
        try{ClassLoader loader=type.getClassLoader();Class<?> game=gameType("zombie.GameWindow",loader);Object disconnected=read(game,null,"serverDisconnected");if(!(disconnected instanceof Boolean ended)||ended){handoff(false);return;}
            m.worldStatus(string(type,"gameLoadingString"));m.conversion(flag(type,"convertingWorld"),number(type,null,"convertingFileCount"),number(type,null,"convertingFileMax"));refreshServer(loader);refreshClock(loader);
        }catch(ReflectiveOperationException unavailable){handoff(false);}
    }
    private static void refreshServer(ClassLoader loader)throws ReflectiveOperationException{
        LoadingModel m=model;if(m==null||m.snapshot().context!=LoadingModel.Context.SERVER)return;
        Class<?> client=gameType("zombie.network.GameClient",loader),core=gameType("zombie.core.Core",loader);Object instance=read(client,null,"instance");
        String map=string(client,"gameMap");if(map.isBlank())map=string(core,"gameMap");m.serverStats(map,instance==null?-1:listSize(read(client,instance,"serverMods")));
    }
    public static void connectionState(Object state){
        LoadingModel m=model;if(m==null||state==null||state!=connectionOwner)return;Class<?> type=state.getClass();Object current=read(type,state,"state");
        if(current instanceof Enum<?> actual){String actualStage=actual.name();if(actualStage.equals("Exit")){handoff(true);return;}
            int projects=switch(actualStage){case "WorkshopQuery","WorkshopConfirm","ServerWorkshopItemScreen","WorkshopUpdate" -> listSize(read(type,state,"workshopItems"));default -> -1;};
            m.serverStage(actualStage,projects,-1);
            WorkshopDownload observed=download;if(observed!=null&&observed.owner==state&&observed.generation==m.snapshot().generation){observed.publish(m,System.nanoTime());if(!m.snapshot().downloadPage)presentation.restoreWorkshop();}
            // TestTCP initializes the map before entering workshop states. CheckMods
            // replaces serverMods only before Finish; earlier values can belong to an old server.
            if(!actualStage.equals("Start")&&!actualStage.equals("TestTCP"))try{
                Class<?> client=gameType("zombie.network.GameClient",type.getClassLoader());
                Object instance=read(client,null,"instance");m.serverStats(string(client,"gameMap"),actualStage.equals("Finish")&&instance!=null?listSize(read(client,instance,"serverMods")):-1);
            }catch(ReflectiveOperationException ignored){}
        }
    }
    public static Object enterConnection(Object state,String method){Object prior=connectionDispatch.get();if(method.equals("enter")){if(connectionOwner!=null&&connectionOwner!=state){rejectWorkshop();download=null;workshopInteraction=null;}connectionOwner=state;}connectionDispatch.set(state);return prior;}
    public static void exitConnection(Object state,Object prior,Throwable error){try{if(error==null)connectionState(state);else if(state==connectionOwner)handoff(false);}finally{if(prior==null)connectionDispatch.remove();else connectionDispatch.set(prior);}}
    public static void sync(){LoadingModel m=model;if(m!=null)m.serverStage("Loading",-1,-1);}
    private record LuaReload(LoadingModel model,long generation,long epoch) {}
    public static Object luaReloadBegin(String reason){
        // This reset is issued synchronously by the current connection's Finish.
        // Options/auth/menu resets must not take over a different native page.
        LoadingModel m=model;Object owner=connectionOwner;
        if(m==null||owner==null||connectionDispatch.get()!=owner||!"ConnectedToServer".equals(reason))return null;
        long generation=m.snapshot().generation,epoch=m.serverLuaReloadBegin();if(epoch==0)return null;
        presentation.restoreWorkshop();presentation.clear();download=null;workshopInteraction=null;
        return new LuaReload(m,generation,epoch);
    }
    public static void luaReloadEnd(Object value,Throwable error){
        if(!(value instanceof LuaReload reload)||model!=reload.model)return;
        reload.model.serverLuaReloadEnd(reload.generation,reload.epoch,error==null);
    }
    public static void queueUpdate(Object state){if(state!=null&&flag(state.getClass(),"cancel"))handoff(false);}
    public static void queueRedirect(Object next){handoff(next!=null&&next.getClass().getName().equals("zombie.gameStates.GameLoadingState"));}
    public static void connectionEvent(String state){switch(state){case "Failed","Disconnected","Disconnecting","ClientVersionMismatch" -> handoff(false);default -> {}}}
    private record StatusObservation(Object owner,NativeConnectionPresentation.Lease page,Object label) {}
    private static boolean passiveState(String state){return switch(state){case "Connecting","GettingServerInfo","UDPConnecting","SteamConnecting","AuthPending","ConnectionPending","TestTCP","Connected","ConnectionImmediately","ApplyRequest" -> true;default -> false;};}
    public static Object statusBefore(Class<?> eventType,Object state,Object message,Object argument){
        if(eventType==null||!eventType.getName().equals("zombie.Lua.LuaEventManager")||!(state instanceof String key))return null;
        boolean allowed=passiveState(key);
        if(key.equals("Message"))allowed=message instanceof String suffix&&passiveState(suffix);
        if(key.equals("FormatMessage"))allowed="PlaceInQueue".equals(message)&&integer(argument)>=0&&integer(argument)<=255;
        LoadingModel.Snapshot s=snapshot();Object owner=connectionOwner;
        ClassLoader loader=eventType.getClassLoader();
        // Native handshakes occur before ConnectionDetails creates the Java state.
        // Exact page/generation identities own that interval; an established owner must still match.
        if(!allowed||s==null||!s.active||s.context!=LoadingModel.Context.SERVER||s.route!=LoadingModel.Route.BEHIND_UI||loader==null||owner!=null&&owner.getClass().getClassLoader()!=loader)return null;
        NativeConnectionPresentation.Lease page=presentation.statusPage(loader,s);Object label=presentation.statusIdentity(page);
        return page==null||label==null?null:new StatusObservation(owner,page,label);
    }
    public static void statusAfter(Object value){
        if(!(value instanceof StatusObservation observation)||observation.owner!=connectionOwner)return;
        LoadingModel m=model;if(m==null)return;LoadingModel.Snapshot s=m.snapshot();String label=presentation.statusAfter(observation.page,s,observation.label);
        if(!label.isEmpty())m.nativeStatus(s.generation,label);
    }
    public static long commandBefore(){return generation();}
    public static void commandAfter(Object owner,String command,long operation,Throwable error){if(error!=null){if(owner==connectionOwner)rejectWorkshop();return;}if("disconnect".equals(command)){handoff(false);return;}WorkshopDownload d=download;if(error==null&&"install".equals(command)&&d!=null&&d.valid&&d.owner==owner&&owner==connectionOwner&&d.generation==operation&&generation()==operation&&d.state("WorkshopUpdate"))d.installed=true;}
    public static void workshopEvent(Object flag,Object item,Object bytes,Object total){
        LoadingModel m=model;if(m==null||!(flag instanceof String kind))return;
        switch(kind){case "Error" -> handoff(false);case "Required" -> {presentation.restoreWorkshop();download=new WorkshopDownload(connectionDispatch.get()==connectionOwner?connectionOwner:null,m.snapshot().generation,item);m.workshop(listSize(item),-1,-1);workshopInteraction=new WorkshopInteraction(connectionOwner,m.snapshot().generation);presentation.clear();m.nativeInteraction(true);}
            case "Details" -> {WorkshopDownload d=download;if(d!=null&&d.owner==connectionDispatch.get()&&d.owner==connectionOwner&&d.generation==generation())d.details(item);}
            case "Progress" -> {WorkshopDownload d=download;if(d!=null&&d.owner==connectionDispatch.get()&&d.owner==connectionOwner&&d.generation==generation())d.progress(item,bytes,total,System.nanoTime());}
            case "Success" -> {WorkshopInteraction interaction=workshopInteraction;if(interaction!=null&&interaction.owner!=null&&interaction.owner==connectionOwner&&interaction.owner==connectionDispatch.get()&&interaction.generation==m.snapshot().generation){Object state=read(connectionOwner.getClass(),connectionOwner,"state");if(state instanceof Enum<?> actual&&actual.name().equals("WorkshopUpdate")){interaction.success=true;download=null;m.download(false,"","",-1,-1,-1,-1);presentation.restoreWorkshop();m.nativeInteraction(true);m.serverStage("CheckMods",-1,-1);}}}default -> {}}
    }
    public static void worldEvent(String event){LoadingModel m=model;if(m==null)return;m.worldStatus(switch(event){case "OnLoadedTileDefinitions" -> "已读取地形定义";case "OnLoadedMapZones" -> "已读取地图区域";case "OnGameTimeLoaded" -> "已读取世界时间";default -> "";});}
    public static String enterFrame(String type){String prior=frame.get();if(workshopRestorePending){workshopRestorePending=false;rejectWorkshop();}if(snapshot()==null||!snapshot().active||!snapshot().downloadPage)presentation.restoreWorkshop();presentation.clear();cpuFrame.set(new long[]{frameSequence.incrementAndGet(),0});frame.set(type.equals("zombie.gameStates.MainScreenState")?"MENU":type.equals("zombie.gameStates.LoadingQueueState")?"QUEUE":"");return prior;}
    public static void exitFrame(String prior){presentation.clear();cpuFrame.remove();if(prior==null)frame.remove();else frame.set(prior);}
    static boolean acceptedFrame(long sequence){long[] cpu=cpuFrame.get();return cpu!=null&&cpu[0]==sequence&&cpu[1]!=0;}
    public static boolean skipElement(Object element){return presentation.skipElement(element);}
    static void rejectWorkshopRows(){rejectWorkshop();presentation.clear();}
    static boolean workshopList(Object element){return presentation.workshopList(element);}
    static boolean workshopPresentationCurrent(long generation,long epoch){LoadingModel m=model;return m!=null&&m.presentationCurrent(generation,epoch);}
    static boolean ownsWorkshop(ClassLoader loader,long generation){WorkshopDownload d=download;return d!=null&&d.owner==connectionOwner&&d.generation==generation&&d.owner.getClass().getClassLoader()==loader&&d.present();}
    public static boolean skipWorkshopTitle(Object element,String text,double x,double y){return presentation.skipWorkshopTitle(element,text,x,y);}
    public static boolean skipQueue(Object element){return presentation.skipQueue(element);}
    public static boolean skipTexture(Object element,Object texture,String kind){return presentation.skipTexture(element,texture,NativeConnectionPresentation.Kind.valueOf(kind));}
    /** Queue place=-1/immediate never call renderBackground; this runs after their black clear. */
    public static void endFrameUI(Object core){if("QUEUE".equals(frame.get()))background(core);}
    public static void background(Object state){
        LoadingModel.Snapshot s=snapshot();String scope=frame.get();if(s==null||s.context!=LoadingModel.Context.SERVER||s.route!=LoadingModel.Route.BEHIND_UI||scope==null||scope.isEmpty()||state==null)return;
        long[] cpu=cpuFrame.get();if(cpu==null||cpu[1]!=0)return;
        WorkshopDownload observed=download;if(s.downloadPage&&observed!=null&&observed.owner==connectionOwner&&observed.generation==s.generation){observed.publish(model,System.nanoTime());s=snapshot();}
        WorkshopInteraction interaction=workshopInteraction;
        if(interaction!=null&&interaction.success&&interaction.owner==connectionOwner&&interaction.generation==s.generation&&scope.equals("MENU")&&presentation.resolve(state.getClass().getClassLoader(),s,0,false)!=null){model.nativeInteraction(false);workshopInteraction=null;s=snapshot();}
        if(!s.active)return;
        try{ClassLoader loader=state.getClass().getClassLoader();Object use=read(gameType("zombie.ui.UIManager",loader),null,"useUiFbo");if(!Boolean.FALSE.equals(use)){rejectWorkshop();return;}
            NativeConnectionPresentation.Lease lease=presentation.resolve(loader,s,cpu[0],scope.equals("QUEUE"));
            if(s.downloadPage&&lease==null){rejectWorkshop();return;}
            if(QueuedBackdrop.enqueue(loader,new QueuedBackdrop.Ticket(s,scope,lease))){cpu[1]=1;presentation.accept(lease);}else rejectWorkshop();
        }
        catch(Throwable error){failed(error);}
    }
    private static boolean currentContext(){try{return GameDisplay.created()&&GL.getCapabilities()!=null;}catch(Throwable notCurrent){return false;}}
    /** Called by the fixed generated drawer; exceptions cannot escape into native UI commands. */
    public static void queuedDraw(QueuedBackdrop.Ticket ticket){
        try{LoadingModel.Snapshot current=snapshot();if(ticket==null||current==null||!current.active||current.generation!=ticket.state.generation||current.epoch!=ticket.state.epoch||current.context!=LoadingModel.Context.SERVER||current.route!=LoadingModel.Route.BEHIND_UI||(!ticket.scope.equals("MENU")&&!ticket.scope.equals("QUEUE"))||drawing||!currentContext())return;
            if(GL11.glGetInteger(GL30.GL_DRAW_FRAMEBUFFER_BINDING)!=0){if(ticket.state.downloadPage)workshopRestorePending=true;return;}
            draw(ticket.state,ticket.statusBottom,ticket.cancelTop);
        }catch(Throwable error){failed(error);}
    }
    private static void draw(LoadingModel.Snapshot state)throws Exception{draw(state,0,0);}
    private static void draw(LoadingModel.Snapshot state,double statusBottom,double cancelTop)throws Exception{
        drawing=true;try{LoadingModel m=model;long now=System.currentTimeMillis();if(now-lastIssueRead>=250){lastIssueRead=now;m.issue(session.readIssue(now,issueFloor));}
            if(renderer==null)renderer=new GameRenderer();java.lang.Runtime memory=java.lang.Runtime.getRuntime();renderer.draw(state,session,memory.totalMemory()-memory.freeMemory(),memory.maxMemory(),statusBottom,cancelTop);
        }finally{drawing=false;}
    }
    public static void beforeSwap(){lastSwap=System.nanoTime();LoadingModel.Snapshot s=snapshot();if(s==null||drawing||!s.active||s.route!=LoadingModel.Route.FULL||!currentContext())return;try{draw(s);}catch(Throwable failure){failed(failure);}}
    /** Full overlays can idle on the existing render thread. Behind-UI frames use only the game's queue. */
    public static void idle(){LoadingModel.Snapshot s=snapshot();if(s==null||drawing||!s.active||s.route!=LoadingModel.Route.FULL||System.nanoTime()-lastSwap<33_333_333L||!currentContext())return;try{GameDisplay.swap();}catch(Throwable failure){failed(failure);}}
    public static void failed(Throwable failure){publicQuery.cancel();LoadingModel m=model;if(m!=null)m.disable();if(!reportedFailure){reportedFailure=true;System.err.println("[OZL-STARTUP-JAVA] presentation disabled: "+failure.getClass().getSimpleName());}}
}
