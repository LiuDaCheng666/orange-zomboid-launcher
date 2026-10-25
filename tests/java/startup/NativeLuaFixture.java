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
  methods.rawset("setY",(JavaFunction)(f,n)->{((UIElement)f.get(0)).setY(num(f.get(1)));return 0;});
  methods.rawset("setHeight",(JavaFunction)(f,n)->{((UIElement)f.get(0)).setHeight(num(f.get(1)));return 0;});
  methods.rawset("DrawTextCentre",(JavaFunction)(f,n)->{((UIElement)f.get(0)).DrawTextCentre((UIFont)f.get(1),(String)f.get(2),num(f.get(3)),num(f.get(4)),1,1,1,1);return 0;});
  methods.rawset("DrawText",(JavaFunction)(f,n)->{((UIElement)f.get(0)).DrawText((UIFont)f.get(1),(String)f.get(2),num(f.get(3)),num(f.get(4)),num(f.get(5)),num(f.get(6)),num(f.get(7)),num(f.get(8)));return 0;});
  function("nativeMeasure",(f,n)->f.push((double)TextManager.instance.MeasureStringX((UIFont)f.get(0),(String)f.get(1))));
  function("nativeFontHeight",(f,n)->f.push((double)((UIFont)f.get(0)).size));
  env.rawset("fontSmall",UIFont.Small);env.rawset("fontMedium",UIFont.Medium);env.rawset("fontLarge",UIFont.Large);
  function("fixtureText",(f,n)->{if(UIElement.textDrawing!=null)UIElement.textDrawing.accept(new UIElement.Text((UIElement)f.get(0),(String)f.get(1),num(f.get(2)),num(f.get(3)),num(f.get(4))));return 0;});
  KahluaTable texMeta=platform.newTable(),texMethods=platform.newTable();texMeta.rawset("__index",texMethods);KahluaUtil.getClassMetatables(platform,env).rawset(Texture.class,texMeta);
  texMethods.rawset("getWidth",(JavaFunction)(f,n)->f.push((double)((Texture)f.get(0)).width));texMethods.rawset("getHeight",(JavaFunction)(f,n)->f.push((double)((Texture)f.get(0)).height));
  function("getTexture",(f,n)->f.push(new Texture((String)f.get(0),15,15)));
  env.rawset("screenW",(double)width);env.rawset("screenH",(double)height);
  run(BOOTSTRAP);
  Path ui=game.resolve("media/lua/client/ISUI/ISUIElement.lua");
  for(String name:new String[]{"setY","setHeight","drawTextureScaled","drawTextureScaledAspect3","drawTexture","drawRect","drawRectStatic","drawRectBorderStatic","drawRectBorder","drawProgressBar"})load(ui,"ISUIElement:"+name);
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
  zombie.Lua.LuaManager.env=env;UIManager.thread=thread;UIManager.UI.clear();UIManager.UI.add(main);UIManager.luaRender=main::render;
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
 public static int stateReads;
 public static final class Details {public final String id,title,state;public String getIDString(){return id;}public String getTitle(){return title;}public Details(String id,String title,String state){this.id=id;this.title=title;this.state=state;}}
 void metadata(Details d){KahluaTable meta=platform.newTable(),methods=platform.newTable();meta.rawset("__index",methods);KahluaUtil.getClassMetatables(platform,env).rawset(Details.class,meta);
  methods.rawset("getIDString",(JavaFunction)(f,n)->f.push(((Details)f.get(0)).id));methods.rawset("getTitle",(JavaFunction)(f,n)->f.push(((Details)f.get(0)).title));methods.rawset("getFileSize",(JavaFunction)(f,n)->f.push(10485760d));methods.rawset("getState",(JavaFunction)(f,n)->{stateReads++;return f.push(((Details)f.get(0)).state);});}
 void workshop(Path game,zombie.gameStates.ConnectToServerState owner)throws Exception{
  KahluaTable meta=platform.newTable(),methods=platform.newTable();meta.rawset("__index",methods);KahluaUtil.getClassMetatables(platform,env).rawset(ArrayList.class,meta);
  methods.rawset("size",(JavaFunction)(f,n)->f.push((double)((ArrayList<?>)f.get(0)).size()));methods.rawset("get",(JavaFunction)(f,n)->f.push(((ArrayList<?>)f.get(0)).get((int)num(f.get(1)))));
  function("connectToServerStateCallback",(f,n)->{owner.FromLua((String)f.get(0));return 0;});
  run(WORKSHOP_BOOTSTRAP);
  Path source=game.resolve("media/lua/client/OptionScreens/ServerWorkshopItemScreen.lua");
  String full=Files.readString(source);run(full.substring(full.indexOf("ServerWorkshopItemScreen ="),full.indexOf("function WorkshopItemList:doDrawItem"))+"\nWorkshopItemListFixture=WorkshopItemList");
  // Original functions retain the lexical WorkshopItemList reference.
  String functions=full.substring(full.indexOf("function WorkshopItemList:doDrawItem"),full.indexOf("local function OnServerWorkshopItems"));
  run("local WorkshopItemList=WorkshopItemListFixture; local FONT_HGT_SMALL=14;local FONT_HGT_MEDIUM=20\n"+functions);
  run("workshop=ServerWorkshopItemScreen:new(screenW*.05,screenH*.05,screenW*.9,screenH*.9);workshop:instantiate();workshop.background=true;workshop.backgroundColor={r=0,g=0,b=0,a=.8};workshop.borderColor={r=1,g=1,b=1,a=.5};workshop:create();workshop:setVisible(false);main.serverWorkshopItem=workshop;main:addChild(workshop);CoopOptionsScreen={instance={getIsVisible=function() return false end}};Events={OnServerWorkshopItems={Add=function(fn) workshopEvent=fn end}};function getSteamModeActive() return true end");
  Path scrolling=game.resolve("media/lua/client/ISUI/ISScrollingListBox.lua");for(String name:new String[]{"rowAt","topOfItem","prevVisibleIndex","nextVisibleItem","onMouseWheel","onJoypadDirDown","onJoypadDirUp","ensureVisible"})load(scrolling,"ISScrollingListBox:"+name);run("ISScrollingListBox.nextVisibleIndex=ISScrollingListBox.nextVisibleItem");
  String text=Files.readString(source);run(text.substring(text.indexOf("local function OnServerWorkshopItems(")));
  zombie.Lua.LuaEventManager.original=(event,arguments)->{
   if(event.equals("OnServerWorkshopItems")){Object[] lua=arguments.clone();for(int i=0;i<lua.length;i++)if(lua[i] instanceof Number number)lua[i]=number.doubleValue();thread.call(env.rawget("workshopEvent"),lua);}
   else if(event.equals("OnGoogleAuthRequest")||event.equals("OnConnected"))thread.call(thread.tableget(connect.getTable(),event),new Object[]{connect.getTable()});
  };
 }
 static final String WORKSHOP_BOOTSTRAP="""
 ISScrollingListBox=ISUIElement:derive('ISScrollingListBox')
 function ISScrollingListBox:new(x,y,w,h) local o=ISUIElement.new(self,x,y,w,h);o.items={};o.selected=1;o.borderColor={r=1,g=1,b=1,a=.5};o.vscroll={getWidth=function() return 12 end};return o end
 function ISScrollingListBox:initialise() self:instantiate();self.vscroll=ISUIElement:new(self.width-12,0,12,self.height);self.vscroll.Type='ISScrollBar';self:addChild(self.vscroll) end
 function ISScrollingListBox:invokeOnMouseDownFunction() end
 function ISScrollingListBox:setFont(f,p) end
 function ISScrollingListBox:clear() self.items={} end
 function ISScrollingListBox:addItem(id,data) self.items[#self.items+1]={item=data,text=id,index=#self.items+1,height=54} end
 function ISScrollingListBox:getScrollHeight() return #self.items*54 end
 function ISScrollingListBox:isMouseOverScrollBar() return false end
 function ISScrollingListBox:render() local y=self:getYScroll();for i=1,#self.items do if y+self.items[i].height>0 and y<self.height then y=self:doDrawItem(y,self.items[i],false) else y=y+self.items[i].height end end end
 function ISUIElement:insertNewLineOfButtons(b) self.joypadIndexY=1 end
 function ISButton:new(x,y,w,h,name,target,onclick) local o=ISUIElement.new(self,x,y,w,h);o.name=name;o.target=target;o.onclick=onclick;o.enable=true;return o end
 function ISButton:setEnable(v) self.enable=v;self.enabled=v end
 function ISButton:clearJoypadButton() self.joyCleared=true end
 function ISPanelJoypad:setISButtonForA(b) self.ISButtonA=b end
 function ISPanelJoypad:setISButtonForB(b) self.ISButtonB=b end
 function ISButton:render() self:drawRect(0,0,self.width,self.height,.8,.07,.06,.1);self:drawRectBorder(0,0,self.width,self.height,.7,.8,.8,.9);self:drawText(self.name,12,5,1,1,1,1,UIFont.Small) end
 function ISLabel:render() self:drawText(self.name,0,0,1,1,1,1,UIFont.Medium) end
 function getSoundManager() return {playUISound=function() end} end
 function round(n,p) return n end
 function getTextManager() return {getFontHeight=function(self,font) return nativeFontHeight(font) end,MeasureStringX=function(self,font,t) return nativeMeasure(font,t) end,getFontFromEnum=function(self,font) return {getLineHeight=function() return nativeFontHeight(font) end} end} end

 """;
 static final String BOOTSTRAP="""
 UI_BORDER_SPACING=10;JOYPAD_TEX_SIZE=20;UIFont={Small=fontSmall,Medium=fontMedium,Large=fontLarge}
 function pairs(t) local i=0;return function() i=i+1;if t[i] then return i,t[i] end end,t,nil end
 function ipairs(t) return pairs(t) end
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
 function ISUIElement:initialise() self:instantiate() end
 function ISUIElement:getKeepOnScreen() return false end
 function ISUIElement:instantiate() if not self.javaObject then self.javaObject=fixtureElement(self) end end
 function ISUIElement:addChild(c) c:instantiate();c.parent=self;self.children[#self.children+1]=c;fixtureAdd(self.javaObject,c.javaObject) end
 function ISUIElement:setVisible(v) self.visible=v;fixtureVisible(self.javaObject,v) end
 function ISUIElement:getIsVisible() return self.visible end
 function ISUIElement:getChildren() return self.children end
 function ISUIElement:getWidth() return self.width end
 function ISUIElement:getHeight() return self.height end
 function ISUIElement:getBottom() return self.y+self.height end
 function ISUIElement:getX() return self.x end
 function ISUIElement:getY() return self.y end
 function ISUIElement:setX(v) self.x=v end

 function ISUIElement:setWidth(v) self.width=v end
 function ISUIElement:setName(v) self.name=v end
 function ISUIElement:setTitle(v) self.name=v end
 function ISUIElement:setText(v) self.text=v end
 function ISUIElement:getXScroll() return 0 end
 function ISUIElement:getYScroll() return self.yscroll or 0 end
 function ISUIElement:setAnchorLeft(v) end
 function ISUIElement:setAnchorRight(v) end
 function ISUIElement:setAnchorTop(v) end
 function ISUIElement:setAnchorBottom(v) end
 function ISUIElement:noBackground() self.background=false end
 function ISUIElement:setMargins(a,b,c,d) end
 function ISUIElement:paginate() end
 function ISUIElement:prerender() end
 function ISUIElement:render() end
 function ISUIElement:drawText(t,x,y,r,g,b,a,font) self.javaObject:DrawText(font or UIFont.Small,t,x,y,r,g,b,a) end
 function ISUIElement:drawTextCentre(t,x,y,r,g,b,a,font) self.javaObject:DrawTextCentre(font,t,x,y,r,g,b,a) end
 function ISUIElement:calcLogoHeight() return screenH*.2 end
 ISPanel=ISUIElement:derive('ISPanel');ISPanelJoypad=ISUIElement:derive('ISPanelJoypad');ISPanelJoypad.SuperType=ISUIElement
 ISLabel=ISUIElement:derive('ISLabel');ISRichTextPanel=ISUIElement:derive('ISRichTextPanel');ISButton=ISUIElement:derive('ISButton');ISTextEntryBox=ISUIElement:derive('ISTextEntryBox')
 function ISLabel:new(x,y,h,name) local o=ISUIElement.new(self,x,y,120,h);o.name=name;return o end
 function ISTextEntryBox:new(text,x,y,w,h) return ISUIElement.new(self,x,y,w,h) end
 """;
}
