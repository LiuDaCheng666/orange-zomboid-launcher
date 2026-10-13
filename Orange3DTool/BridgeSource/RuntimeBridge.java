package orange.ads;
import java.lang.reflect.*;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;
public final class RuntimeBridge {
 private static boolean reached,failed,ready;
 private static Object player,env;
 private static Class<?> ads;
 private static Method tick;
 private static Properties config;
 private static List<String> activeSnapshot;
 private static ReticleMode mode;
 private static int modeKey;
 private static boolean modeCtrl,modeAlt,modeShift;
 private static long noticeUntil;
 private static String notice;
 private static Method rawKey;
 private static boolean owned,luaActive;
 private static Class<?> cls(String n)throws Exception{return Class.forName(n,false,ClassLoader.getSystemClassLoader());}
 private static Object call(Class<?> c,String n,Class<?>[] types,Object... values)throws Exception{return c.getMethod(n,types).invoke(null,values);}
 private static void set(String name,Class<?>[] types,Object... values)throws Exception{call(ads,name,types,values);}
 private static boolean flag(String c,String n)throws Exception{return cls(c).getField(n).getBoolean(null);}
 public static void afterUpdate(){if(!reached){reached=true;System.out.println("[ORANGE-ADS] update hook reached");}if(failed)return;try{
  if(!flag("zombie.network.GameClient","client")||flag("zombie.network.GameServer","server")){releaseOwned();ready=false;player=null;return;}
  Object current=call(cls("zombie.characters.IsoPlayer"),"getInstance",new Class<?>[0]);if(current==null){releaseOwned();ready=false;player=null;return;}
  Object luaEnv=cls("zombie.Lua.LuaManager").getField("env").get(null);
  Object fs=cls("zombie.ZomboidFileSystem").getField("instance").get(null);Object ids=fs.getClass().getMethod("getModIDs").invoke(fs);
  if(!(ids instanceof List<?>))throw new IllegalStateException("active mod list unavailable");
  if(ready&&current==player&&luaEnv==env&&activeSnapshot!=null&&activeSnapshot.equals(ids)){pollMode();if(!luaActive)tick.invoke(null);return;}
  Set<String> active=new HashSet<>();for(Object id:(Iterable<?>)ids)active.add(String.valueOf(id).replace("\\",""));
  for(String conflict:new String[]{"ShootToThrill","ShootToThrillMultiplayerTest","ISeeThem","ISeeThemMultiplayer","ProjectViewpointADS","ViewpointTrueBallistics"})if(active.contains(conflict)){releaseOwned();failed=true;ready=false;System.out.println("[ORANGE-ADS] stopped: active runtime conflict "+conflict);return;}
  luaActive=active.contains("ShootToThrillMultiplayer");
  if(!ready||current!=player||luaEnv!=env||!activeSnapshot.equals(ids)){initialize(fs);player=current;env=luaEnv;activeSnapshot=new ArrayList<>();for(Object id:(List<?>)ids)activeSnapshot.add(String.valueOf(id));ready=true;System.out.println("[ORANGE-ADS] local presentation initialized; native/server combat retained; render and hit alignment UNVERIFIED");}
  pollMode();if(!luaActive)tick.invoke(null);
 }catch(Throwable e){failed=true;ready=false;try{releaseOwned();}catch(Throwable restore){System.out.println("[ORANGE-ADS] native fallback failed: "+restore);}System.out.println("[ORANGE-ADS] stopped for this launch: "+e);}}
 private static void releaseOwned()throws Exception{if(!owned)return;set("configure",new Class<?>[]{boolean.class,double.class,double.class,double.class},false,1d,100d,0d);set("reset",new Class<?>[0]);owned=false;showNotice("原版瞄准 / Viewpoint (extension stopped)");}
 private static void showNotice(String text){notice=text;noticeUntil=System.nanoTime()+3_000_000_000L;System.out.println("[ORANGE-ADS] aim mode: "+text);}
 private static void applyMode()throws Exception{set("configure",new Class<?>[]{boolean.class,double.class,double.class,double.class},!mode.nativeMode(),1d,100d,0d);set("reset",new Class<?>[0]);set("showOriginalReticle",new Class<?>[]{boolean.class},true);set("showMuzzleMarker",new Class<?>[]{boolean.class},true);showNotice(mode.nativeMode()?"原版瞄准 / Viewpoint":"扩展开镜 / Shoot to Thrill (experimental)");}
 private static boolean raw(int key)throws Exception{return (Boolean)rawKey.invoke(null,key);}
 private static void pollMode()throws Exception{if(modeKey==0)return;boolean down=raw(modeKey)&&(!modeCtrl||raw(29)||raw(157))&&(!modeShift||raw(42)||raw(54))&&(!modeAlt||raw(56)||raw(184));Object entry=cls("zombie.core.Core").getField("currentTextEntryBox").get(null);boolean typing=entry!=null&&(Boolean)cls("zombie.ui.UITextEntryInterface").getMethod("isDoingTextEntry").invoke(entry);if(mode.poll(down,!typing))applyMode();}
 public static void renderNotice(){if(notice==null||System.nanoTime()>noticeUntil)return;try{Class<?> tm=cls("zombie.ui.TextManager");Object manager=tm.getField("instance").get(null);tm.getMethod("DrawString",double.class,double.class,String.class,double.class,double.class,double.class,double.class).invoke(manager,24d,90d,notice,1d,.8d,.2d,1d);}catch(Throwable ignored){}}
 private static void initialize(Object fs)throws Exception{
  if(config==null){config=new Properties();Path self=Path.of(RuntimeBridge.class.getProtectionDomain().getCodeSource().getLocation().toURI());try(var input=Files.newBufferedReader(self.getParent().resolve("OrangeShootPresentation.properties"))){config.load(input);}}
  Path source=Path.of(config.getProperty("shoot.jar","")).toRealPath();MessageDigest digest=MessageDigest.getInstance("SHA-256");String actual=HexFormat.of().formatHex(digest.digest(Files.readAllBytes(source))).toUpperCase(Locale.ROOT);if(!actual.equals(config.getProperty("shoot.sha256","")))throw new IllegalStateException("shooting JAR changed since API preflight; install again to recheck current version");
  ads=cls("iseethem.ADS");Path loaded=Path.of(ads.getProtectionDomain().getCodeSource().getLocation().toURI()).toRealPath();if(!loaded.equals(source))throw new IllegalStateException("loaded shooting edition differs from verified source");
  if((Boolean)call(cls("iseethem.NetworkMode"),"customCombat",new Class<?>[0]))throw new IllegalStateException("custom combat active; visual-only bridge refuses");
  Object core=call(cls("zombie.core.Core"),"getInstance",new Class<?>[0]);Method get=core.getClass().getMethod("getKey",String.class),add=core.getClass().getMethod("addKeyBinding",String.class,int.class,int.class,boolean.class,boolean.class,boolean.class);
  String[] names={"ISTSwitchMode","ISTBreath","ISTZeroUp","ISTZeroDown"};int[] keys={47,42,201,209};for(int i=0;i<names.length;i++)if((Integer)get.invoke(core,names[i])<=0)add.invoke(core,names[i],keys[i],0,false,i==0,false);
  @SuppressWarnings("unchecked") Map<String,String> map=(Map<String,String>)fs.getClass().getField("activeFileMap").get(fs);Path media=source.getParent().getParent().getParent();Path ui=media.resolve("ui/ISeeThem").toRealPath();
  try(var files=Files.walk(ui)){for(Path file:files.filter(Files::isRegularFile).toList())if(file.getFileName().toString().toLowerCase(Locale.ROOT).endsWith(".png")){Path real=file.toRealPath();if(!real.startsWith(ui))throw new IllegalStateException("UI asset escapes original directory");String key="media/ui/ISeeThem/"+ui.relativize(real).toString().replace('\\','/');map.putIfAbsent(key.toLowerCase(Locale.ROOT),real.toString());}}
  // Only original public presentation initialization. No networking or damage API.
  owned=true;
  set("configure",new Class<?>[]{boolean.class,double.class,double.class,double.class},true,1d,100d,0d);
  set("configureAimStyle",new Class<?>[]{int.class,double.class},0,.20d);
  set("configureModes",new Class<?>[]{boolean.class,double.class,int.class,int.class},false,.20d,0,0);
  set("configureInput",new Class<?>[]{boolean.class,boolean.class},false,false);
  set("configureView",new Class<?>[]{boolean.class},false);
  set("configureScope",new Class<?>[]{boolean.class,int.class,int.class,boolean.class},false,1,30,true);
  set("configureHipLens",new Class<?>[]{boolean.class},false);
  tick=ads.getMethod("tick");
  mode=new ReticleMode(!"extension".equals(config.getProperty("aim.mode","native")));modeKey=Integer.parseInt(config.getProperty("aim.key","0"));if(modeKey<0||modeKey>255)throw new IllegalStateException("invalid aim toggle key");modeCtrl=Boolean.parseBoolean(config.getProperty("aim.ctrl","false"));modeShift=Boolean.parseBoolean(config.getProperty("aim.shift","false"));modeAlt=Boolean.parseBoolean(config.getProperty("aim.alt","false"));rawKey=cls("zombie.input.GameKeyboard").getMethod("isKeyDownRaw",int.class);applyMode();
 }
}
