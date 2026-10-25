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
    private WorkshopGeometry workshopGeometry;
    public static final class Lease {
        public final double statusBottom,cancelTop;
        final ClassLoader loader;final Object environment,mainTable,connectTable,main,connect,logo,background,arrowBG,arrowFG,queue;
        final Set<Object> decoration,allowedMain,allowedConnection,hiddenPages;
        final long generation,epoch,frame;final boolean queueScope,workshopScope;
        Lease(ClassLoader loader,Object environment,Object mainTable,Object connectTable,Object main,Object connect,Object logo,Object background,Object bg,Object fg,Object queue,Set<Object> decoration,Set<Object> allowedMain,Set<Object> allowedConnection,Set<Object> hiddenPages,LoadingModel.Snapshot state,long frame,boolean queueScope,double statusBottom,double cancelTop){
            this.loader=loader;this.environment=environment;this.mainTable=mainTable;this.connectTable=connectTable;this.main=main;this.connect=connect;this.logo=logo;this.background=background;arrowBG=bg;arrowFG=fg;this.queue=queue;this.decoration=decoration;this.allowedMain=allowedMain;this.allowedConnection=allowedConnection;this.hiddenPages=hiddenPages;generation=state.generation;epoch=state.epoch;this.frame=frame;this.queueScope=queueScope;workshopScope=state.downloadPage;this.statusBottom=statusBottom;this.cancelTop=cancelTop;
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
        return resolve(loader,state,frame,queueScope,true);
    }
    public Lease statusPage(ClassLoader loader,LoadingModel.Snapshot state){return resolve(loader,state,0,false,false);}
    private Lease resolve(ClassLoader loader,LoadingModel.Snapshot state,long frame,boolean queueScope,boolean drawing){
        try{
            if(drawing&&!Boolean.FALSE.equals(field(type("zombie.ui.UIManager",loader),"useUiFbo")))return null;
            if(state.downloadPage)return resolveWorkshop(loader,state,frame,true);
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
    /** A separate read observation; never arms or extends a rendering lease. */
    public Object statusIdentity(Lease lease){try{return lease==null?null:raw(lease.connectTable,"connectLabel");}catch(Throwable unavailable){return null;}}
    public String statusAfter(Lease before,LoadingModel.Snapshot state,Object label){
        if(before==null||state==null||!state.active||state.generation!=before.generation||state.epoch!=before.epoch)return "";
        Lease after=statusPage(before.loader,state);
        if(after==null||before.environment!=after.environment||before.mainTable!=after.mainTable||before.connectTable!=after.connectTable||label!=statusIdentity(after))return "";
        try{Object value=raw(label,"name");return value instanceof String s&&s.length()<=256?LoadingModel.safe(s,256):"";}catch(Throwable unavailable){return "";}
    }
    public void clear(){current.remove();}
    private Lease valid(){
        Lease lease=current.get();if(lease==null)return null;
        if(lease.workshopScope)return validWorkshop(lease);
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
    /** Constant-sized identity checks only; no item scans, new leases or identity-set allocation. */
    private Lease validWorkshop(Lease lease){try{
        if(!StartupRuntime.workshopPresentationCurrent(lease.generation,lease.epoch)||!StartupRuntime.acceptedFrame(lease.frame)||!StartupRuntime.ownsWorkshop(lease.loader,lease.generation))return declineWorkshop();
        if(!Boolean.FALSE.equals(field(type("zombie.ui.UIManager",lease.loader),"useUiFbo"))||field(type("zombie.Lua.LuaManager",lease.loader),"env")!=lease.environment)return declineWorkshop();
        if(raw(raw(lease.environment,"MainScreen"),"instance")!=lease.mainTable||raw(lease.mainTable,"serverWorkshopItem")!=lease.connectTable||raw(raw(lease.environment,"ServerWorkshopItemScreen"),"instance")!=lease.connectTable||raw(lease.mainTable,"logoTexture")!=lease.logo||!visible(lease.main)||!visible(lease.connect))return declineWorkshop();
        if(workshopGeometry==null||workshopGeometry.environment!=lease.environment||workshopGeometry.page!=lease.connectTable||!workshopGeometry.matches()||!Boolean.FALSE.equals(raw(workshopGeometry.install,"enable"))){restoreWorkshop();return declineWorkshop();}
        exactChildren(lease.main,lease.allowedMain,false);exactChildren(lease.connect,lease.allowedConnection,false);for(Object page:lease.hiddenPages)if(visible(page))return declineWorkshop();
        if(!children(element(workshopGeometry.label)).isEmpty()||!children(element(workshopGeometry.install)).isEmpty()||!children(element(workshopGeometry.disconnect)).isEmpty()||!children(element(workshopGeometry.scroll)).isEmpty())return declineWorkshop();
        List<?> listChildren=children(element(workshopGeometry.list));if(listChildren.size()!=1||listChildren.get(0)!=element(workshopGeometry.scroll))return declineWorkshop();
        Object roots=type("zombie.ui.UIManager",lease.loader).getMethod("getUI").invoke(null);if(!(roots instanceof ArrayList<?> list)||!list.contains(lease.main))return declineWorkshop();for(Object root:list)if(root!=lease.main&&visible(root))return declineWorkshop();
        return lease;
    }catch(Throwable unavailable){return declineWorkshop();}}
    private Lease declineWorkshop(){restoreWorkshop();clear();return null;}
    public boolean skipWorkshopTitle(Object element,String text,double x,double y){Lease lease=valid();try{return lease!=null&&lease.workshopScope&&element==lease.connect&&text!=null&&y==10&&x==number(lease.connectTable,"width")/2;}catch(Exception unavailable){return false;}}
    private Lease resolveWorkshop(ClassLoader loader,LoadingModel.Snapshot state,long frame,boolean reflow){try{
        if(!StartupRuntime.ownsWorkshop(loader,state.generation))return null;Class<?> font=type("zombie.ui.UIFont",loader),text=type("zombie.ui.TextManager",loader);text.getMethod("MeasureStringX",font,String.class);if(text.getField("instance").get(null)==null)return null;
        if(!Boolean.FALSE.equals(field(type("zombie.ui.UIManager",loader),"useUiFbo")))return null;
        Object env=field(type("zombie.Lua.LuaManager",loader),"env"),mt=raw(raw(env,"MainScreen"),"instance"),main=element(mt);namedType(loader,mt,"MainScreen");if(!visible(main)||Boolean.TRUE.equals(raw(mt,"inGame")))return null;
        Object wt=raw(mt,"serverWorkshopItem"),page=element(wt);namedType(loader,wt,"ServerWorkshopItemScreen");if(!visible(page)||raw(raw(env,"ServerWorkshopItemScreen"),"instance")!=wt)return null;
        Set<Object> allowed=identities(),ma=identities(),hidden=identities();ma.add(page);
        for(String name:new String[]{"label","listbox","installButton","disconnectButton"}){Object t=raw(wt,name),e=element(t);namedType(loader,t,name.equals("label")?"ISLabel":name.equals("listbox")?"WorkshopItemList":"ISButton");if(!visible(e))return null;allowed.add(e);if(!name.equals("listbox")&&!children(e).isEmpty())return null;}
        exactChildren(page,allowed,false);
        Object listTable=raw(wt,"listbox"),listElement=element(listTable),scrollTable=raw(listTable,"vscroll"),scroll=element(scrollTable);if(scroll==null||raw(listTable,"hscroll")!=null)return null;namedType(loader,scrollTable,"ISScrollBar");if(!children(scroll).isEmpty())return null;Set<Object> scrollAllowed=identities();scrollAllowed.add(scroll);exactChildren(listElement,scrollAllowed,false);
        for(String name:CHROME){Object e=element(raw(mt,name));if(e!=null)ma.add(e);}
        Object connection=element(raw(mt,"connectToServer"));if(connection==null||visible(connection)||raw(raw(mt,"connectToServer"),"failMessage")!=null||visible(element(raw(raw(mt,"connectToServer"),"googleAuthPopup"))))return null;ma.add(connection);hidden.add(connection);
        for(String name:PAGES){if(name.equals("serverWorkshopItem"))continue;Object e=element(raw(mt,name));if(e!=null){if(visible(e))return null;ma.add(e);hidden.add(e);}}
        exactChildren(main,ma,false);Object roots=type("zombie.ui.UIManager",loader).getMethod("getUI").invoke(null);if(!(roots instanceof ArrayList<?> list)||!list.contains(main))return null;for(Object root:list)if(root!=main&&visible(root))return null;
        if(!Boolean.FALSE.equals(raw(raw(wt,"installButton"),"enable")))return null;
        if(workshopGeometry!=null&&(workshopGeometry.page!=wt||workshopGeometry.environment!=env||!workshopGeometry.sameWidgets())){restoreWorkshop();return null;}
        if(workshopGeometry==null)workshopGeometry=new WorkshopGeometry(loader,env,wt);
        if(reflow){if(!workshopGeometry.apply(number(mt,"height")))return null;}else if(!workshopGeometry.matches())return null;
        Object logo=raw(mt,"logoTexture");if(logo==null)return null;
        return new Lease(loader,env,mt,wt,main,page,logo,null,null,null,null,identities(),ma,allowed,hidden,state,frame,false,0,0);
    }catch(Throwable unavailable){return null;}}
    /** CPU-only: retained through input, including between rendered frames. */
    public void restoreWorkshop(){WorkshopGeometry geometry=workshopGeometry;workshopGeometry=null;if(geometry!=null)geometry.restore();}
    private static final class WorkshopGeometry {
        final ClassLoader loader;final Object environment,page,list,label,install,disconnect,scroll;final double originalY,originalLabelY;double appliedY,appliedLabelY;
        WorkshopGeometry(ClassLoader loader,Object env,Object page)throws Exception{this.loader=loader;environment=env;this.page=page;list=raw(page,"listbox");label=raw(page,"label");install=raw(page,"installButton");disconnect=raw(page,"disconnectButton");scroll=raw(list,"vscroll");originalY=number(list,"y");originalLabelY=number(label,"y");appliedY=originalY;appliedLabelY=originalLabelY;}
        void set(Object table,String method,double value)throws Exception{
            Class<?> kt=type("se.krka.kahlua.vm.KahluaTable",loader),ui=type("zombie.ui.UIManager",loader);Object function=ui.getMethod("tableget",kt,Object.class).invoke(null,table,method);if(function==null)throw new IllegalStateException("native setter");
            Object caller=field(type("zombie.Lua.LuaManager",loader),"caller"),thread=ui.getMethod("getDefaultThread").invoke(null);
            caller.getClass().getMethod("pcallvoid",type("se.krka.kahlua.vm.KahluaThread",loader),Object.class,Object[].class).invoke(caller,thread,function,new Object[]{table,Double.valueOf(value)});
            String key=method.equals("setY")?"y":"height",getter=method.equals("setY")?"getY":"getHeight";Object javaValue=element(table).getClass().getMethod(getter).invoke(element(table));if(!(javaValue instanceof Double n)||Math.abs(number(table,key)-value)>.01||Math.abs(n-value)>.01)throw new IllegalStateException("native setter mismatch");
        }
        boolean sameWidgets()throws Exception{return raw(page,"listbox")==list&&raw(page,"label")==label&&raw(page,"installButton")==install&&raw(page,"disconnectButton")==disconnect&&raw(list,"vscroll")==scroll;}
        boolean matches()throws Exception{return sameWidgets()&&number(list,"y")==appliedY&&number(label,"y")==appliedLabelY;}
        boolean apply(double screen)throws Exception{if(number(list,"y")!=appliedY||number(label,"y")!=appliedLabelY)return false;double bottom=number(list,"y")+number(list,"height"),button=number(raw(page,"installButton"),"y");if(Math.abs(bottom+20-button)>.01)return false;double y=Math.max(originalY,screen*.56-number(page,"y"));if(bottom-y<90||screen<480)return false;double beforeY=appliedY,beforeLabel=appliedLabelY,beforeHeight=number(list,"height");appliedY=y;appliedLabelY=y-number(label,"height")-8;try{set(list,"setY",y);set(list,"setHeight",bottom-y);set(label,"setY",appliedLabelY);return true;}catch(Exception error){try{set(list,"setY",beforeY);set(list,"setHeight",beforeHeight);set(label,"setY",beforeLabel);}finally{appliedY=beforeY;appliedLabelY=beforeLabel;}throw error;}}
        void restore(){try{if(number(list,"y")==appliedY&&Math.abs(number(list,"y")+number(list,"height")+20-number(raw(page,"installButton"),"y"))<.01){double bottom=number(list,"y")+number(list,"height");set(list,"setY",originalY);set(list,"setHeight",bottom-originalY);}if(number(label,"y")==appliedLabelY)set(label,"setY",originalLabelY);}catch(Throwable unavailable){/* Native page remains authoritative if a mod replaced the setter. */}}
    }
    public boolean workshopList(Object element){Lease lease=valid();try{return lease!=null&&lease.workshopScope&&element==element(raw(lease.connectTable,"listbox"));}catch(Throwable unavailable){return false;}}
    public boolean skipElement(Object element){Lease lease=valid();return lease!=null&&lease.decoration.contains(element);}
    public boolean skipQueue(Object element){Lease lease=valid();return lease!=null&&lease.queueScope&&element==lease.queue;}
    public boolean skipTexture(Object element,Object texture,Kind kind){
        Lease lease=valid();if(lease==null)return false;
        if(element==lease.main)return kind==Kind.SCALED&&texture==lease.logo;
        if(element!=lease.connect||lease.queueScope)return false;
        if(lease.workshopScope)return kind==Kind.SCALED&&texture==null;
        return switch(kind){case SCALED -> texture==null;case COLOR -> texture!=null&&(texture==lease.arrowBG||texture==lease.arrowFG);case ASPECT -> texture!=null&&texture==lease.background;};
    }
}
