package ozl.startup.tests;
import java.nio.file.*;
import java.util.*;
import ozl.startup.*;
import zombie.Lua.LuaEventManager;
import zombie.gameStates.ConnectToServerState;

/** Installed native handler, isolated Kahlua VM; only fixture engine classes may load. */
public final class NativeStatusTest {
 static int checks;static void check(boolean v,String why){if(!v)throw new AssertionError(why);checks++;}
 public static void main(String[] args)throws Exception{
  NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),1280,720);
  f.function("getText",(frame,n)->frame.push("本地化 / "+frame.get(0)));
  f.run("string={format=function(text,arg) return text..' / '..tostring(arg) end}; function ConnectToServer:OnConnectFailed(message,detail) self.failMessage=message;self.connectLabel.name=message end");
  f.run("connection.userName.name=setmetatable({},{__tostring=function() error('username must not be read') end}); connection.googleAuthEntry=setmetatable({},{__index=function() error('auth must not be read') end}); connection.qrCode=setmetatable({},{__index=function() error('QR must not be read') end})");
  var oldUser=f.child("userName");var userTable=oldUser.getTable();int[] privateReads={0};
  var guarded=(se.krka.kahlua.vm.KahluaTable)java.lang.reflect.Proxy.newProxyInstance(userTable.getClass().getClassLoader(),new Class<?>[]{se.krka.kahlua.vm.KahluaTable.class},(proxy,method,a)->{if(method.getName().equals("rawget")&&("name".equals(a[0])||"text".equals(a[0]))){privateReads[0]++;throw new AssertionError("private username read");}return method.invoke(userTable,a);});
  var guardedUser=new zombie.ui.UIElement(guarded);guarded.rawset("javaObject",guardedUser);f.connect.controls.set(f.connect.controls.indexOf(oldUser),guardedUser);f.connect.getTable().rawset("userName",guarded);
  StartupRuntime.serverBegin("Status fixture");new ConnectToServerState().enter();
  LuaEventManager.original=(event,a)->{if(event.equals("OnConnectionStateChanged")){Object[] values=new Object[a.length+1];values[0]=f.connect.getTable();System.arraycopy(a,0,values,1,a.length);for(int i=1;i<values.length;i++)if(values[i] instanceof Number n)values[i]=n.doubleValue();f.thread.call(f.thread.tableget(f.connect.getTable(),"OnConnectionStateChanged"),values);}};
  for(String state:new String[]{"SteamConnecting","AuthPending","UDPConnecting","GettingServerInfo","Connected"}){
   LuaEventManager.triggerEvent("OnConnectionStateChanged",state);
   String label="本地化 / UI_servers_"+state;
   check(StartupRuntime.snapshot().detail.equals(label),"exact localized status captured after original "+state);
   String[] activity=StartupRuntime.snapshot().activity;LuaEventManager.triggerEvent("OnConnectionStateChanged",state);check(Arrays.equals(StartupRuntime.snapshot().activity,activity),"duplicate status does not append "+state);
  }
  LuaEventManager.triggerEvent("OnConnectionStateChanged","Message","TestTCP");check(StartupRuntime.snapshot().detail.endsWith("TestTCP"),"TestTCP is not renamed Steam");
  check(!StartupRuntime.snapshot().world&&StartupRuntime.snapshot().active,"Connected is joining, never world completion");
  LuaEventManager.triggerEvent("OnConnectionStateChanged","Message","GettingServerInfo");check(StartupRuntime.snapshot().detail.equals("本地化 / UI_servers_GettingServerInfo"),"known Message suffix uses actual localized native label");
  LuaEventManager.triggerEvent("OnConnectionStateChanged","FormatMessage","PlaceInQueue",7);check(StartupRuntime.snapshot().detail.endsWith("PlaceInQueue / 7"),"validated native queue format captured");
  String known=StartupRuntime.snapshot().detail;
  for(Object[] a:new Object[][]{{"Alien"},{"Message","PrivateAuthData"},{"FormatMessage","PlaceInQueue",-1},{"FormatMessage","PlaceInQueue",1.5},{"FormatMessage","Unknown",7}}){
   if(a.length==1)LuaEventManager.triggerEvent("OnConnectionStateChanged",a[0]);else if(a.length==2)LuaEventManager.triggerEvent("OnConnectionStateChanged",a[0],a[1]);else LuaEventManager.triggerEvent("OnConnectionStateChanged",a[0],a[1],a[2]);
   check(StartupRuntime.snapshot().detail.equals(known),"unknown payload rejected");
  }
  f.connect.visible=false;LuaEventManager.triggerEvent("OnConnectionStateChanged","SteamConnecting");check(StartupRuntime.snapshot().detail.equals(known),"hidden page rejected");f.connect.visible=true;
  f.child("googleAuthPopup").visible=true;LuaEventManager.triggerEvent("OnConnectionStateChanged","SteamConnecting");check(StartupRuntime.snapshot().detail.equals(known),"auth page rejected");f.child("googleAuthPopup").visible=false;
  var original=LuaEventManager.original;
  LuaEventManager.original=(event,a)->{original.accept(event,a);new ConnectToServerState().enter();};LuaEventManager.triggerEvent("OnConnectionStateChanged","AuthPending");check(StartupRuntime.snapshot().detail.equals(known),"same-generation replacement owner rejected");LuaEventManager.original=original;
  LuaEventManager.original=(event,a)->{original.accept(event,a);zombie.Lua.LuaManager.env=f.platform.newTable();};LuaEventManager.triggerEvent("OnConnectionStateChanged","AuthPending");check(StartupRuntime.snapshot().detail.equals(known),"environment identity replacement rejected");zombie.Lua.LuaManager.env=f.env;LuaEventManager.original=original;
  LuaEventManager.original=(event,a)->{original.accept(event,a);throw new IllegalStateException("original handler failure");};boolean thrown=false;try{LuaEventManager.triggerEvent("OnConnectionStateChanged","AuthPending");}catch(IllegalStateException expected){thrown=true;}check(thrown&&StartupRuntime.snapshot().detail.equals(known),"throwing handler does not capture and preserves original exception");LuaEventManager.original=original;
  LuaEventManager.original=(event,a)->{original.accept(event,a);StartupRuntime.serverBegin("Replacement");new ConnectToServerState().enter();};LuaEventManager.triggerEvent("OnConnectionStateChanged","AuthPending");check(StartupRuntime.snapshot().detail.isEmpty(),"stale generation rejected after handler");LuaEventManager.original=original;
  LuaEventManager.triggerEvent("OnConnectionStateChanged","Failed","private failure");check(!StartupRuntime.snapshot().active&&Arrays.stream(StartupRuntime.snapshot().activity).noneMatch(s->s.contains("private")),"failure is handed off without capture");
  check(privateReads[0]==0,"username text is never read, including silently caught reads");
  System.out.println("PASS "+checks+" exact native passive status contracts");
 }
}
