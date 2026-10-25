package ozl.startup.tests;

import java.nio.file.*;
import java.util.*;
import se.krka.kahlua.vm.*;
import zombie.ui.UIElement;

/** Executes the shipped bridge submission with installed native connect/failure/Back Lua. */
public final class BridgeParentVisibilityTest {
 static int checks;
 static void check(boolean value,String why){if(!value)throw new AssertionError(why);checks++;}
 static String submission(Path bridge)throws Exception{
  String text=Files.readString(bridge);
  int branch=text.indexOf("elseif lines[4]=='server'");
  int start=text.indexOf("ok=pcall(function()",branch);
  int end=text.indexOf("if not ok then",start);
  if(branch<0||start<0||end<0)throw new AssertionError("Shipped bridge server submission absent");
  return text.substring(start,end);
 }
 static void visibleControls(NativeLuaFixture f,String phase)throws Exception{
  check(f.main.visible,phase+": MainScreen root remains visible (passing MainScreen to native connect hides all descendants)");
  check(f.connect.visible,phase+": native connection panel remains visible");
  int root=f.main.renders,panel=f.connect.renders,label=f.child("connectLabel").renders,back=f.child("backBtn").renders;
  f.main.render();
  check(f.main.renders==root+1&&f.connect.renders==panel+1,phase+": root and native connection traverse");
  check(f.child("connectLabel").renders==label+1&&f.child("backBtn").renders==back+1,phase+": native status and Back/Abort traverse the complete hierarchy");
 }
 static UIElement menu(NativeLuaFixture f){return NativeLuaFixture.element((KahluaTable)f.main.getTable().rawget("bottomPanel"));}
 static void menuVisible(NativeLuaFixture f,boolean visible,String phase){
  check(menu(f).visible==visible,phase+": normal menu buttons visibility");
  for(String name:new String[]{"resetLua","modListDetail","termsOfService","reportBug"})
   check(NativeLuaFixture.element((KahluaTable)f.main.getTable().rawget(name)).visible==visible,phase+": native bottom-panel companion "+name+" visibility");
 }
 public static void main(String[] args)throws Exception{
  Path game=Path.of(args[0]),repo=Path.of(args[1]).toAbsolutePath();
  while(repo!=null&&!Files.isRegularFile(repo.resolve("Orange3DTool/Assets/OrangeLaunchBridge.lua")))repo=repo.getParent();
  if(repo==null)throw new AssertionError("Fixture output must be inside source repository");
  String submit=submission(repo.resolve("Orange3DTool/Assets/OrangeLaunchBridge.lua"));
  NativeLuaFixture f=new NativeLuaFixture(game,1280,720);
  f.thread.getClass().getField("debugOwnerThread").set(f.thread,Thread.currentThread());
  KahluaTable meta=(KahluaTable)KahluaUtil.getClassMetatables(f.platform,f.env).rawget(UIElement.class);
  KahluaTable methods=(KahluaTable)meta.rawget("__index");
  methods.rawset("setVisible",(JavaFunction)(frame,n)->{((UIElement)frame.get(0)).visible=Boolean.TRUE.equals(frame.get(1));return 0;});
  methods.rawset("isVisible",(JavaFunction)(frame,n)->frame.push(((UIElement)frame.get(0)).visible));
  // Only the external engine resize/network operations are replaced; native connect owns UI state.
  List<List<Object>> requests=new ArrayList<>();
  f.function("serverConnect",(frame,n)->{List<Object> values=new ArrayList<>();for(int i=0;i<n;i++)values.add(frame.get(i));requests.add(values);return 0;});
  f.function("fixtureModUrlMatch",(frame,n)->{
   check("MODURL=".equals(frame.get(1)),"native failure requests only the expected MODURL pattern");
   return frame.push(((String)frame.get(0)).contains("MODURL=")?"MODURL=":null);
  });
  f.run("ISPanelJoypad.onResize=function() end;string={match=fixtureModUrlMatch}");
  Path connection=game.resolve("media/lua/client/OptionScreens/ConnectToServer.lua");
  for(String name:new String[]{"connect","onResize","OnConnectFailed"})f.load(connection,"ConnectToServer:"+name);
  f.load(game.resolve("media/lua/client/OptionScreens/MainScreen.lua"),"MainScreen:setBottomPanelVisible");
  f.load(game.resolve("media/lua/client/ISUI/ISUIElement.lua"),"ISUIElement:getIsVisible");
  f.run("main.bottomPanel.setVisible=MainScreen.setBottomPanelVisible;main.bottomPanel:setVisible(true);connection:setVisible(false);menu=main;"
   +"for _,name in ipairs({'resetLua','modListDetail','termsOfService','reportBug'}) do main[name]=ISPanel:new(0,0,10,10);main:addChild(main[name]) end;"
   +"lines={[7]='fixture.invalid',[8]='16261',[9]='fixture-user',[10]='fixture-login-password',[11]='fixture-server-password',[12]='1'}");
  menuVisible(f,true,"before submit");
  f.run(submit);
  check(Boolean.TRUE.equals(f.env.rawget("ok")),"actual shipped pcall completes");
  check(requests.equals(List.of(List.of("fixture-user","fixture-login-password","fixture.invalid","","16261","fixture-server-password","",true,true,1d,""))),"native serverConnect receives one unchanged direct-server request");
  visibleControls(f,"connecting");
  menuVisible(f,false,"connecting");
  check("UI_servers_Connecting".equals(f.child("connectLabel").getTable().rawget("name")),"native connecting message preserved");
  check("UI_coopscreen_btn_abort".equals(f.child("backBtn").getTable().rawget("name")),"native Abort preserved");
  f.run("connection:OnConnectFailed('Workshop item unavailable','3793163745');connection:OnConnectionStateChanged('Disconnected',nil)");
  visibleControls(f,"failure followed by disconnect");
  menuVisible(f,false,"failure");
  check("Workshop item unavailable\n3793163745".equals(f.child("connectLabel").getTable().rawget("name")),"original failure plus detail survive disconnect");
  check("UI_btn_back".equals(f.child("backBtn").getTable().rawget("name")),"native failure restores Back title");
  check(Boolean.FALSE.equals(f.connect.getTable().rawget("connecting")),"failure stops native connecting state");
  f.call("onBackButton");
  check(f.main.visible&&!f.connect.visible,"Back closes native connection without hiding MainScreen");
  menuVisible(f,true,"Back after failure");
  check(f.cancellations==0,"Back after completed failure does not abort an inactive connection");
  f.run("lines[12]='0'");f.run(submit);
  check(requests.size()==2&&Boolean.FALSE.equals(requests.get(1).get(7)),"second explicit submission preserves relay=false");
  visibleControls(f,"second connection before Abort");
  f.call("onBackButton");
  check(f.cancellations==1&&f.main.visible&&!f.connect.visible,"native Abort cancels once and returns to visible MainScreen");
  menuVisible(f,true,"Abort");
  check(requests.size()==2,"failure/Back/Abort never submit an extra connection");
  System.out.println("PASS "+checks+" shipped bridge/native parent visibility contracts");
 }
}
