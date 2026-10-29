package ozl.startup.tests;

import java.nio.file.*;
import java.util.*;
import se.krka.kahlua.vm.*;

/** Execute the production order branch on the installed game's actual Kahlua VM. */
public final class BuddyOptionAvailabilityTest {
 static void check(boolean value,String why){if(!value)throw new AssertionError(why);}
 public static void main(String[] args)throws Exception{
  Path repo=Path.of(args[1]).toAbsolutePath();while(repo!=null&&!Files.isRegularFile(repo.resolve("Orange3DTool/Assets/OrangeLaunchBridge.lua")))repo=repo.getParent();
  check(repo!=null,"repository fixture output");String bridge=Files.readString(repo.resolve("Orange3DTool/Assets/OrangeLaunchBridge.lua"));String order=bridge.substring(0,bridge.indexOf("local inputNonce="));
  String original="tickbox|ZombieBuddy|autoFixModOrder|true\r\nslider|OtherMod|amount|0.65\r\n";
  for(String state:new String[]{"no-api","no-group","no-option","no-setter","ready"}){
   NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),1280,720);f.thread.getClass().getField("debugOwnerThread").set(f.thread,Thread.currentThread());Map<String,String> files=new HashMap<>();files.put("ModOptions.ini",original);int[] thrown={0},runtime={0},ui={0};
   // The game's StringLib initializes engine ObjectPools. Keep the closed VM
   // boundary and implement only this INI match; installed-Lua tests cover the real string library.
   KahluaTable stringMeta=f.platform.newTable(),stringMethods=f.platform.newTable();stringMeta.rawset("__index",stringMethods);KahluaUtil.getClassMetatables(f.platform,f.env).rawset(String.class,stringMeta);
   stringMethods.rawset("match",(JavaFunction)(frame,n)->{check("^tickbox|ZombieBuddy|autoFixModOrder|([^|]+)".equals(frame.get(1)),"expected saved-option line pattern");String row=(String)frame.get(0),prefix="tickbox|ZombieBuddy|autoFixModOrder|";return frame.push(row.startsWith(prefix)?row.substring(prefix.length()).split("\\|",2)[0]:null);});
   JavaFunction builtin=(JavaFunction)f.env.rawget("error");f.function("error",(frame,n)->{thrown[0]++;return builtin.call(frame,n);});
   f.function("getFileReader",(frame,n)->{String value=files.get((String)frame.get(0));if(value==null)return frame.push((Object)null);String[] lines=value.split("\\r?\\n");int[] at={0};KahluaTable reader=f.platform.newTable();reader.rawset("readLine",(JavaFunction)(call,count)->call.push(at[0]<lines.length?lines[at[0]++]:null));reader.rawset("close",(JavaFunction)(call,count)->0);return frame.push(reader);});
   f.function("getFileWriter",(frame,n)->{String name=(String)frame.get(0);boolean append=Boolean.TRUE.equals(frame.get(2));StringBuilder text=new StringBuilder(append?files.getOrDefault(name,""):"");KahluaTable writer=f.platform.newTable();writer.rawset("write",(JavaFunction)(call,count)->{text.append(call.get(1));return 0;});writer.rawset("close",(JavaFunction)(call,count)->{files.put(name,text.toString());return 0;});return frame.push(writer);});
   f.function("runtimeOrder",(frame,n)->{check(Boolean.FALSE.equals(frame.get(0)),"original Buddy runtime API receives false");runtime[0]++;return 0;});
   f.function("uiOrder",(frame,n)->{check(Boolean.FALSE.equals(frame.get(1)),"ready settings option receives false");ui[0]++;return 0;});
   f.run("ZombieBuddy={setAutoFixModOrder=runtimeOrder};PZAPI=nil");
   if(!state.equals("no-api"))f.run("PZAPI={ModOptions={getOptions=function() return "+(state.equals("no-group")?"nil":"{getOption=function() return "+(state.equals("no-option")?"nil":state.equals("no-setter")?"{}":"{setValue=uiOrder}")+" end}")+" end}};");
   f.run(order+"\nserverOrder()");
   check(thrown[0]==0,"missing optional UI must not invoke Lua error: "+state);check(runtime[0]==1,"runtime order still applied once: "+state);check(ui[0]==(state.equals("ready")?1:0),"only available UI option is updated");check(files.get("ModOptions.ini").startsWith(original)&&files.get("ModOptions.ini").endsWith("tickbox|ZombieBuddy|autoFixModOrder|false\r\n"),"original option bytes and delayed-reload preference retained");
  }
  System.out.println("PASS actual Kahlua production Buddy branch: five readiness states, zero artificial errors, runtime/INI preference preserved");
 }
}
