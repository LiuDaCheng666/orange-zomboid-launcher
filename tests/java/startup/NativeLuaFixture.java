package ozl.startup.tests;

import java.nio.file.*;
import java.util.*;
import se.krka.kahlua.vm.*;
import se.krka.kahlua.j2se.*;
import se.krka.kahlua.stdlib.*;
import se.krka.kahlua.luaj.compiler.LuaCompiler;
import zombie.ui.*;
import zombie.core.textures.Texture;

/** Runs selected unmodified installed Lua functions, with a closed fixture engine surface. */
public final class NativeLuaFixture {
 final J2SEPlatform platform=new J2SEPlatform();
 final KahluaTable env=platform.newTable();final KahluaThread thread;
 public final UIElement main,connect;public final List<UIElement.Draw> draws=new ArrayList<>();
 public final int width,height;public int cancellations;
 NativeLuaFixture(Path game,int width,int height)throws Exception{
  this.width=width;this.height=height;
  BaseLib.register(env);MathLib.register(platform,env);TableLib.register(platform,env);
  KahluaTable math=(KahluaTable)env.rawget("math");math.rawset("max",(JavaFunction)(f,n)->f.push(Math.max(num(f.get(0)),num(f.get(1)))));math.rawset("min",(JavaFunction)(f,n)->f.push(Math.min(num(f.get(0)),num(f.get(1)))));
  env.rawset("_G",env);thread=new KahluaThread(System.out,platform,env);
  function("fixtureElement",(f,n)->{KahluaTable table=(KahluaTable)f.get(0);return f.push(new UIElement(table));});
  function("fixtureAdd",(f,n)->{UIElement p=(UIElement)f.get(0),c=(UIElement)f.get(1);p.controls.add(c);return 0;});
  function("fixtureVisible",(f,n)->{((UIElement)f.get(0)).visible=Boolean.TRUE.equals(f.get(1));return 0;});
  function("backToSinglePlayer",(f,n)->{cancellations++;return 0;});
  function("assert",(f,n)->{if(f.get(0)==null||Boolean.FALSE.equals(f.get(0)))throw new AssertionError("Extracted native Lua assertion");return 0;});
  KahluaTable meta=platform.newTable(),methods=platform.newTable();meta.rawset("__index",methods);
  KahluaUtil.getClassMetatables(platform,env).rawset(UIElement.class,meta);
  for(String method:new String[]{"DrawTextureScaledColor","DrawTextureColor","DrawTextureScaledAspect3"}){
   methods.rawset(method,(JavaFunction)(f,n)->{UIElement e=(UIElement)f.get(0);Texture t=(Texture)f.get(1);double x=num(f.get(2)),y=num(f.get(3));
    if(method.equals("DrawTextureColor"))e.DrawTextureColor(t,x,y,num(f.get(4)),num(f.get(5)),num(f.get(6)),num(f.get(7)));
    else if(method.equals("DrawTextureScaledColor"))e.DrawTextureScaledColor(t,x,y,num(f.get(4)),num(f.get(5)),num(f.get(6)),num(f.get(7)),num(f.get(8)),num(f.get(9)));
    else e.DrawTextureScaledAspect3(t,x,y,num(f.get(4)),num(f.get(5)),num(f.get(6)),num(f.get(7)),num(f.get(8)),num(f.get(9)));return 0;});
  }
  KahluaTable texMeta=platform.newTable(),texMethods=platform.newTable();texMeta.rawset("__index",texMethods);KahluaUtil.getClassMetatables(platform,env).rawset(Texture.class,texMeta);
  texMethods.rawset("getWidth",(JavaFunction)(f,n)->f.push((double)((Texture)f.get(0)).width));texMethods.rawset("getHeight",(JavaFunction)(f,n)->f.push((double)((Texture)f.get(0)).height));
  function("getTexture",(f,n)->f.push(new Texture((String)f.get(0),15,15)));
  env.rawset("screenW",(double)width);env.rawset("screenH",(double)height);
  run(BOOTSTRAP);
  Path ui=game.resolve("media/lua/client/ISUI/ISUIElement.lua");
  for(String name:new String[]{"drawTextureScaled","drawTextureScaledAspect3","drawTexture","drawRect","drawRectStatic","drawRectBorderStatic","drawRectBorder"})load(ui,"ISUIElement:"+name);
  load(game.resolve("media/lua/client/ISUI/ISPanelJoypad.lua"),"ISPanelJoypad:prerender");
  run("ConnectToServer = ISPanelJoypad:derive('ConnectToServer')\nMainScreen = ISPanelJoypad:derive('MainScreen')");
  Path connection=game.resolve("media/lua/client/OptionScreens/ConnectToServer.lua");
  // create() closes over the exact installed font constants; keep its original lexical environment.
  run("local FONT_HGT_SMALL=14; local FONT_HGT_MEDIUM=20\n"+source(connection,"ConnectToServer:create"));
  for(String name:new String[]{"prerender","render","onBackButton","OnConnected","OnGoogleAuthRequest","OnConnectionStateChanged"})load(connection,"ConnectToServer:"+name);
  load(game.resolve("media/lua/client/OptionScreens/MainScreen.lua"),"MainScreen:prerender");
  env.rawset("logo",new Texture("native-PZ-logo",880,220));
  run("main=MainScreen:new(0,0,screenW,screenH);main:instantiate();MainScreen.instance=main;main.inGame=false;main.delay=0;main.time=0;main.warningFade=0;main.warningFadeMax=1;main.maxMenuItemWidth=0;main.logoTexture=logo;main.bottomPanel=ISPanel:new(0,0,0,0);main.bottomPanel:instantiate();main.bottomPanel:setVisible(false);main:addChild(main.bottomPanel);\n"
   +"connection=ConnectToServer:new(screenW*.15,screenH*.1,screenW*.7,screenH*.8);connection:instantiate();connection:create();main.connectToServer=connection;main:addChild(connection);connection.background=true;connection.backgroundColor={r=0,g=0,b=0,a=.8};connection.borderColor={r=1,g=1,b=1,a=.5};connection.connecting=true;connection.animOffset=-1;connection.connectLabel.name='Connecting to server';connection.backBtn.name='Abort';connection.previousScreen=main.bottomPanel;\n");
  main=element((KahluaTable)env.rawget("main"));connect=element((KahluaTable)env.rawget("connection"));
  zombie.Lua.LuaManager.env=env;UIManager.UI.clear();UIManager.UI.add(main);UIManager.luaRender=main::render;
  UIElement.call=(e,name)->{Object fn=thread.tableget(e.getTable(),name);if(fn!=null)thread.call(fn,new Object[]{e.getTable()});};
  UIElement.drawing=draws::add;
 }
 static double num(Object value){return ((Number)value).doubleValue();}
 void function(String name,JavaFunction fn){env.rawset(name,fn);}
 void run(String lua)throws Exception{thread.call(LuaCompiler.loadstring(lua,"fixture-extracted",env),new Object[0]);}
 void load(Path path,String name)throws Exception{run(source(path,name));}
 static String source(Path path,String name)throws Exception{
  String text=Files.readString(path);String prefix="function "+name+"(";int start=text.indexOf(prefix);if(start<0)throw new AssertionError("Installed function absent: "+name);
  int end=text.indexOf("\nfunction ",start+prefix.length());int local=text.indexOf("\nlocal function ",start+prefix.length());if(local>=0&&(end<0||local<end))end=local;
  return text.substring(start,end<0?text.length():end);
 }
 static UIElement element(KahluaTable table){return (UIElement)table.rawget("javaObject");}
 UIElement child(String name){return element((KahluaTable)connect.getTable().rawget(name));}
 void call(String name)throws Exception{run("connection:"+name+"()");}
 void workshop(Path game,zombie.gameStates.ConnectToServerState owner)throws Exception{
  KahluaTable meta=platform.newTable(),methods=platform.newTable();meta.rawset("__index",methods);KahluaUtil.getClassMetatables(platform,env).rawset(ArrayList.class,meta);
  methods.rawset("size",(JavaFunction)(f,n)->f.push((double)((ArrayList<?>)f.get(0)).size()));methods.rawset("get",(JavaFunction)(f,n)->f.push(((ArrayList<?>)f.get(0)).get((int)num(f.get(1)))));
  function("connectToServerStateCallback",(f,n)->{owner.FromLua((String)f.get(0));return 0;});
  run("ServerWorkshopItemScreen=ISPanelJoypad:derive('ServerWorkshopItemScreen');workshop=ServerWorkshopItemScreen:new(0,0,700,500);workshop:instantiate();workshop:setVisible(false);ServerWorkshopItemScreen.instance=workshop;main.serverWorkshopItem=workshop;main:addChild(workshop);workshop.label={};workshop.installButton={setEnable=function(self,v) self.enabled=v end};workshop.listbox={items={},clear=function(self) self.items={} end,addItem=function(self,id,data) self.items[#self.items+1]={item=data} end};CoopOptionsScreen={instance={getIsVisible=function() return false end}};Events={OnServerWorkshopItems={Add=function(fn) workshopEvent=fn end}};function getSteamModeActive() return true end");
  Path source=game.resolve("media/lua/client/OptionScreens/ServerWorkshopItemScreen.lua");
  for(String name:new String[]{"setRequiredItems","updateItemDetails","progress","onButtonInstall","onButtonDisconnect"})load(source,"ServerWorkshopItemScreen:"+name);
  String text=Files.readString(source);run(text.substring(text.indexOf("local function OnServerWorkshopItems(")));
  zombie.Lua.LuaEventManager.original=(event,arguments)->{
   if(event.equals("OnServerWorkshopItems")){Object[] lua=arguments.clone();for(int i=0;i<lua.length;i++)if(lua[i] instanceof Number number)lua[i]=number.doubleValue();thread.call(env.rawget("workshopEvent"),lua);}
   else if(event.equals("OnGoogleAuthRequest")||event.equals("OnConnected"))thread.call(thread.tableget(connect.getTable(),event),new Object[]{connect.getTable()});
  };
 }
 static final String BOOTSTRAP="""
 UI_BORDER_SPACING=10;JOYPAD_TEX_SIZE=20;UIFont={Small=1,Medium=2,Large=3}
 function pairs(t) local i=0;return function() i=i+1;if t[i] then return i,t[i] end end,t,nil end
 function getText(s) return s end
 function getTextOrNull(s) return nil end
 function getSteamModeActive() return false end
 function getDebug() return false end
 function isClient() return false end
 function isDemo() return false end
 function getTextManager() return {} end
 function getGameTime() return {getMultiplier=function() return 1 end} end
 function getCore() return {getScreenWidth=function() return screenW end} end
 UIManager={getMillisSinceLastRender=function() return 16 end}
 MainScreenState={getInstance=function() return {} end};ISDemoPopup={};JoypadState={getMainMenuJoypad=function() return nil end}
 ISUITextureGetter={checkGetTexture=function(t) return t end}
 ISUIElement={Type='ISUIElement'}; ISUIElement.__index=ISUIElement
 function ISUIElement:derive(t) local d={Type=t};d.__index=d;setmetatable(d,self);return d end
 function ISUIElement:new(x,y,w,h) local o={x=x,y=y,width=w,height=h,visible=true,children={}};setmetatable(o,self);return o end
 function ISUIElement:initialise() end
 function ISUIElement:instantiate() if not self.javaObject then self.javaObject=fixtureElement(self) end end
 function ISUIElement:addChild(c) c.parent=self;self.children[#self.children+1]=c;fixtureAdd(self.javaObject,c.javaObject) end
 function ISUIElement:setVisible(v) self.visible=v;fixtureVisible(self.javaObject,v) end
 function ISUIElement:getIsVisible() return self.visible end
 function ISUIElement:getChildren() return self.children end
 function ISUIElement:getWidth() return self.width end
 function ISUIElement:getHeight() return self.height end
 function ISUIElement:getBottom() return self.y+self.height end
 function ISUIElement:getX() return self.x end
 function ISUIElement:getY() return self.y end
 function ISUIElement:setX(v) self.x=v end
 function ISUIElement:setY(v) self.y=v end
 function ISUIElement:setWidth(v) self.width=v end
 function ISUIElement:setName(v) self.name=v end
 function ISUIElement:setTitle(v) self.name=v end
 function ISUIElement:setText(v) self.text=v end
 function ISUIElement:getXScroll() return 0 end
 function ISUIElement:getYScroll() return 0 end
 function ISUIElement:setAnchorLeft(v) end
 function ISUIElement:setAnchorRight(v) end
 function ISUIElement:setAnchorTop(v) end
 function ISUIElement:setAnchorBottom(v) end
 function ISUIElement:noBackground() self.background=false end
 function ISUIElement:setMargins(a,b,c,d) end
 function ISUIElement:paginate() end
 function ISUIElement:prerender() end
 function ISUIElement:render() end
 function ISUIElement:calcLogoHeight() return screenH*.2 end
 ISPanel=ISUIElement:derive('ISPanel');ISPanelJoypad=ISUIElement:derive('ISPanelJoypad');ISPanelJoypad.SuperType=ISUIElement
 ISLabel=ISUIElement:derive('ISLabel');ISRichTextPanel=ISUIElement:derive('ISRichTextPanel');ISButton=ISUIElement:derive('ISButton');ISTextEntryBox=ISUIElement:derive('ISTextEntryBox')
 function ISLabel:new(x,y,h,name) local o=ISUIElement.new(self,x,y,120,h);o.name=name;return o end
 function ISTextEntryBox:new(text,x,y,w,h) return ISUIElement.new(self,x,y,w,h) end
 """;
}
