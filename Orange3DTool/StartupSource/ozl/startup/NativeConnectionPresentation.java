package ozl.startup;

import java.lang.reflect.*;
import java.util.*;

/** Read-only exact identities, valid only in the CPU frame that accepted our drawer. */
public final class NativeConnectionPresentation {
    public enum Kind { SCALED, COLOR, ASPECT }
    private static final String[] DECORATION={"title","serverName1","serverName","userName1","userName","richText"};
    // Native menu chrome stays visible and interactive. Other visible pages veto ownership.
    private static final String[] CHROME={"reportBug","termsOfService","versionBtn","resetLua"};
    private static final String[] PAGES={"bottomPanel","charCreationMain","charCreationProfession","sandOptions","onlineCoopScreen","soloScreen","creditsScreen","loadScreen","worldSelect","mapSpawnSelect","modSelect","bootstrapConnectPopup","serverConnectPopup","multiplayer","lastStandPlayerSelect","workshopSubmit","serverWorkshopItem","serverSettingsScreen","mainOptions","scoreboard","inviteFriends","modListDetail","seedLabel","threeD","demoMessagePanel"};
    private final ThreadLocal<Lease> current=new ThreadLocal<>();
    public static final class Lease {
        public final double statusBottom,cancelTop;
        final ClassLoader loader;final Object environment,mainTable,connectTable,main,connect,logo,background,arrowBG,arrowFG,queue;
        final Set<Object> decoration,allowedMain,allowedConnection,hiddenPages;
        final long generation,epoch,frame;final boolean queueScope;
        Lease(ClassLoader loader,Object environment,Object mainTable,Object connectTable,Object main,Object connect,Object logo,Object background,Object bg,Object fg,Object queue,Set<Object> decoration,Set<Object> allowedMain,Set<Object> allowedConnection,Set<Object> hiddenPages,LoadingModel.Snapshot state,long frame,boolean queueScope,double statusBottom,double cancelTop){
            this.loader=loader;this.environment=environment;this.mainTable=mainTable;this.connectTable=connectTable;this.main=main;this.connect=connect;this.logo=logo;this.background=background;arrowBG=bg;arrowFG=fg;this.queue=queue;this.decoration=decoration;this.allowedMain=allowedMain;this.allowedConnection=allowedConnection;this.hiddenPages=hiddenPages;generation=state.generation;epoch=state.epoch;this.frame=frame;this.queueScope=queueScope;this.statusBottom=statusBottom;this.cancelTop=cancelTop;
        }
    }
    private static Class<?> type(String name,ClassLoader loader)throws Exception{return Class.forName(name,false,loader);}
    private static Object field(Class<?> type,String name)throws Exception{Field field=type.getDeclaredField(name);if(!field.canAccess(null))field.setAccessible(true);return field.get(null);}
    private static Object raw(Object table,String key)throws Exception{return table.getClass().getMethod("rawget",Object.class).invoke(table,key);}
    private static Object element(Object table)throws Exception{if(table==null)return null;Object element=raw(table,"javaObject");if(element==null||element.getClass().getMethod("getTable").invoke(element)!=table)throw new IllegalStateException("native identity");return element;}
    private static boolean visible(Object element)throws Exception{Object result=element.getClass().getMethod("isVisible").invoke(element);if(!(result instanceof Boolean))throw new IllegalStateException("visibility");return Boolean.TRUE.equals(result);}
    private static Set<Object> identities(){return Collections.newSetFromMap(new IdentityHashMap<>());}
    private static double number(Object table,String key)throws Exception{Object value=raw(table,key);if(!(value instanceof Number n)||!Double.isFinite(n.doubleValue()))throw new IllegalStateException("native geometry");return n.doubleValue();}
    private static List<?> children(Object element)throws Exception{Object list=element.getClass().getMethod("getControls").invoke(element);if(!(list instanceof ArrayList<?> children))throw new IllegalStateException("native controls");return children;}
    private static void exactChildren(Object parent,Set<Object> allowed,boolean visibleOnly)throws Exception{for(Object child:children(parent))if((!visibleOnly||visible(child))&&!allowed.contains(child))throw new IllegalStateException("unknown native control");}
    private static void namedType(ClassLoader loader,Object table,String expected)throws Exception{
        Class<?> kahlua=type("se.krka.kahlua.vm.KahluaTable",loader);
        Object actual=type("zombie.ui.UIManager",loader).getMethod("tableget",kahlua,Object.class).invoke(null,table,"Type");
        if(!expected.equals(actual))throw new IllegalStateException("native page type");
    }
    /** Resolve on the game CPU thread, before enqueue. Missing/modified contracts simply decline. */
    public Lease resolve(ClassLoader loader,LoadingModel.Snapshot state,long frame,boolean queueScope){
        try{
            if(!Boolean.FALSE.equals(field(type("zombie.ui.UIManager",loader),"useUiFbo")))return null;
            Object env=field(type("zombie.Lua.LuaManager",loader),"env");if(env==null)return null;
            Object mainTable=raw(raw(env,"MainScreen"),"instance"),main=element(mainTable);if(main==null||!visible(main)||Boolean.TRUE.equals(raw(mainTable,"inGame")))return null;
            namedType(loader,mainTable,"MainScreen");
            Object connectTable=raw(mainTable,"connectToServer"),connect=element(connectTable);if(connect==null)return null;namedType(loader,connectTable,"ConnectToServer");
            if(visible(connect)==queueScope||raw(connectTable,"failMessage")!=null)return null;
            Object logo=raw(mainTable,"logoTexture");if(logo==null)return null;
            Set<Object> decoration=identities(),allowed=identities(),mainAllowed=identities(),hidden=identities();
            for(String name:DECORATION){Object childTable=raw(connectTable,name),child=element(childTable);if(child==null)return null;namedType(loader,childTable,name.equals("richText")?"ISRichTextPanel":"ISLabel");if(!children(child).isEmpty())return null;decoration.add(child);allowed.add(child);}
            for(String name:new String[]{"connectLabel","backBtn","googleAuthPopup"}){Object childTable=raw(connectTable,name),child=element(childTable);if(child==null)return null;namedType(loader,childTable,name.equals("connectLabel")?"ISLabel":name.equals("backBtn")?"ISButton":"ISPanel");allowed.add(child);if(name.equals("googleAuthPopup")&&visible(child))return null;}
            exactChildren(connect,allowed,false);
            mainAllowed.add(connect);
            for(String name:CHROME){Object child=element(raw(mainTable,name));if(child!=null)mainAllowed.add(child);}
            for(String name:PAGES){Object child=element(raw(mainTable,name));if(child!=null){if(visible(child))return null;mainAllowed.add(child);hidden.add(child);}}
            exactChildren(main,mainAllowed,false);
            Object roots=type("zombie.ui.UIManager",loader).getMethod("getUI").invoke(null);
            if(!(roots instanceof ArrayList<?> list)||!list.contains(main))return null;
            for(Object root:list)if(root!=main&&visible(root))return null;
            Object queue=null;if(queueScope){queue=field(type("zombie.gameStates.LoadingQueueState",loader),"ui");if(queue==null||queue.getClass()!=type("zombie.ui.LoadingQueueUI",loader))return null;}
            Object status=raw(connectTable,"connectLabel"),button=raw(connectTable,"backBtn");double y=number(connectTable,"y"),screenHeight=number(mainTable,"height");
            double statusBottom=(y+number(status,"y")+number(status,"height"))/screenHeight,cancelTop=(y+number(button,"y"))/screenHeight;
            if(screenHeight<=0||statusBottom<=0||statusBottom>=cancelTop||cancelTop>=1)return null;
            return new Lease(loader,env,mainTable,connectTable,main,connect,logo,raw(connectTable,"loadingBackground"),raw(connectTable,"arrowBG"),raw(connectTable,"arrowFG"),queue,decoration,mainAllowed,allowed,hidden,state,frame,queueScope,statusBottom,cancelTop);
        }catch(Throwable unavailable){return null;}
    }
    public void accept(Lease lease){if(lease!=null)current.set(lease);}
    public void clear(){current.remove();}
    private Lease valid(){
        Lease lease=current.get();if(lease==null)return null;
        try{
            LoadingModel.Snapshot state=StartupRuntime.snapshot();
            if(state==null||!state.active||state.generation!=lease.generation||state.epoch!=lease.epoch||!StartupRuntime.acceptedFrame(lease.frame)||!Boolean.FALSE.equals(field(type("zombie.ui.UIManager",lease.loader),"useUiFbo"))||field(type("zombie.Lua.LuaManager",lease.loader),"env")!=lease.environment)return null;
            if(raw(raw(lease.environment,"MainScreen"),"instance")!=lease.mainTable||element(raw(lease.mainTable,"connectToServer"))!=lease.connect||!visible(lease.main)||visible(lease.connect)==lease.queueScope)return null;
            if(raw(lease.mainTable,"logoTexture")!=lease.logo||raw(lease.connectTable,"failMessage")!=null)return null;
            if(visible(element(raw(lease.connectTable,"googleAuthPopup"))))return null;
            exactChildren(lease.main,lease.allowedMain,false);exactChildren(lease.connect,lease.allowedConnection,false);for(Object page:lease.hiddenPages)if(visible(page))return null;
            for(String name:DECORATION){Object child=element(raw(lease.connectTable,name));if(!lease.decoration.contains(child)||!children(child).isEmpty())return null;}
            Object roots=type("zombie.ui.UIManager",lease.loader).getMethod("getUI").invoke(null);if(!(roots instanceof ArrayList<?> list)||!list.contains(lease.main))return null;for(Object root:list)if(root!=lease.main&&visible(root))return null;
            return lease;
        }catch(Throwable unavailable){return null;}
    }
    public boolean skipElement(Object element){Lease lease=valid();return lease!=null&&lease.decoration.contains(element);}
    public boolean skipQueue(Object element){Lease lease=valid();return lease!=null&&lease.queueScope&&element==lease.queue;}
    public boolean skipTexture(Object element,Object texture,Kind kind){
        Lease lease=valid();if(lease==null)return false;
        if(element==lease.main)return kind==Kind.SCALED&&texture==lease.logo;
        if(element!=lease.connect||lease.queueScope)return false;
        return switch(kind){case SCALED -> texture==null;case COLOR -> texture!=null&&(texture==lease.arrowBG||texture==lease.arrowFG);case ASPECT -> texture!=null&&texture==lease.background;};
    }
}
