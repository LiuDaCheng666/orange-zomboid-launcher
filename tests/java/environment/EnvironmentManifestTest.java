package ozl.environment.tests;

import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;
import java.lang.reflect.*;

public final class EnvironmentManifestTest {
 static int checks;static Class<?> type;
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 static String b64(Path path){return Base64.getEncoder().encodeToString(path.toString().getBytes(StandardCharsets.UTF_8));}
 static String hash(byte[] bytes)throws Exception{StringBuilder b=new StringBuilder();for(byte n:MessageDigest.getInstance("SHA-256").digest(bytes))b.append(String.format(Locale.ROOT,"%02x",n&255));return b.toString();}
 static Object read(Path file,String expected,Path cwd,Path home)throws Exception{return type.getMethod("read",Path.class,String.class,Path.class,Path.class).invoke(null,file,expected,cwd,home);}
 static Object load(Path file,String text,Path cwd,Path home)throws Exception{byte[] bytes=text.getBytes(StandardCharsets.US_ASCII);Files.write(file,bytes);return read(file,hash(bytes),cwd,home);}
 static void invalid(Path file,String text,Path cwd,Path home,String why)throws Exception{boolean failed=false;try{load(file,text,cwd,home);}catch(InvocationTargetException e){failed=e.getCause() instanceof IllegalArgumentException||e.getCause() instanceof java.io.IOException;}check(failed,why);}
 public static void main(String[] args)throws Exception{
  try{type=Class.forName("ozl.environment.EnvironmentManifest");}catch(ClassNotFoundException missing){throw new AssertionError("Frozen environment manifest validator is not implemented");}
  Path base=Files.createTempDirectory(Paths.get(args[0]),"manifest-"),snapshot=Files.createDirectories(base.resolve("snapshot")),game=Files.createDirectories(snapshot.resolve("game")),user=Files.createDirectories(base.resolve("live-user")),home=Files.createDirectories(snapshot.resolve("runtime/home")),a=Files.createDirectories(snapshot.resolve("mods/A")),b=Files.createDirectories(snapshot.resolve("mods/B")),file=snapshot.resolve("environment.properties");
  Files.write(a.resolve("mod.info"),"id=FlatMod\n".getBytes(StandardCharsets.UTF_8));Files.createDirectories(b.resolve("common"));Files.write(b.resolve("common/mod.info"),"id=VersionedMod\n".getBytes(StandardCharsets.UTF_8));
  Path mods=Files.createDirectories(snapshot.resolve("runtime/mods"));
  String text="version=OZLENV1\nid=1234567890abcdef1234567890abcdef\ngame="+b64(game)+"\nuser="+b64(user)+"\nhome="+b64(home)+"\nmods="+b64(mods)+"\nroots="+b64(b)+","+b64(a)+"\nbridge="+b64(user.resolve("mods/OrangeLauncherSession"))+"\n";
  Object accepted=load(file,text,game,home);check(accepted!=null,"fresh ASCII hash-pinned manifest accepted independently of presentation state");
  @SuppressWarnings("unchecked") List<Path> roots=(List<Path>)type.getField("roots").get(accepted);check(roots.equals(Arrays.asList(b,a)),"declared full mod roots preserve exact order");
  String choice="choices="+Base64.getEncoder().encodeToString("OldMod".getBytes(StandardCharsets.UTF_8))+":"+Base64.getEncoder().encodeToString("NewMod".getBytes(StandardCharsets.UTF_8))+","+Base64.getEncoder().encodeToString("SkippedMod".getBytes(StandardCharsets.UTF_8))+":\n";
  Object withChoices=load(file,text+choice,game,home);
  @SuppressWarnings("unchecked") Map<String,String> choices=(Map<String,String>)type.getField("choices").get(withChoices);
  check(choices.size()==2&&"NewMod".equals(choices.get("OldMod"))&&"".equals(choices.get("SkippedMod")),"optional explicit replacement and skip choices are decoded exactly");
  check(((Map<?,?>)type.getField("choices").get(accepted)).isEmpty(),"old manifests omit choices and retain the original behavior");
  check(load(file,text+"choices=\n",game,home)!=null,"explicit empty choices remain compatible");
  invalid(file,text+choice+choice,game,home,"duplicate choices property is rejected");
  invalid(file,text+"choices=T2xkTW9k:T3RoZXI=,T2xkTW9k:\n",game,home,"duplicate old choice identity is rejected");
  invalid(file,text+"choices=:TmV3TW9k\n",game,home,"empty old choice identity is rejected");
  invalid(file,text+"choices=Li4vZXNjYXBl:TmV3TW9k\n",game,home,"choice IDs cannot carry path traversal");
  invalid(file,text+"choices=T2xkTW9k:@@@\n",game,home,"malformed choice encoding is rejected");
  invalid(file,text+"choices=T2xkTW9k:TmV3TW9k,TmV3TW9k:TGF0ZXJNb2Q=\n",game,home,"chained replacements are rejected before a non-idempotent getter can run");
  invalid(file,text+"choices=T2xkTW9k:TmV3TW9k,TmV3TW9k:T2xkTW9k\n",game,home,"cyclic replacements are rejected");
  invalid(file,text+"choices=T3JhbmdlTGF1bmNoZXJTZXNzaW9u:\n",game,home,"choices cannot remove the owned launcher bridge");
  check(load(file,text.replace("\n","\r\n"),game,home)!=null,"ASCII Windows line endings accepted");
  invalid(file,text+"id=1234567890abcdef1234567890abcdef\n",game,home,"duplicate properties rejected");
  invalid(file,text+"expiry=0\n",game,home,"unrecognized contract fields rejected");
  invalid(file,text.replace("OZLENV1","OZLENV2"),game,home,"unknown protocol rejected");
  invalid(file,text.replace("1234567890abcdef1234567890abcdef","invalid"),game,home,"invalid snapshot identity rejected");
  invalid(file,text.replace(b64(b)+","+b64(a),b64(a)+","+b64(a)),game,home,"duplicate declared roots rejected");
  invalid(file,text.replace(b64(b)+","+b64(a),b64(user)),game,home,"live user mod roots outside snapshot are rejected");
  invalid(file,text.replace(b64(b),b64(snapshot.resolve("missing"))),game,home,"missing copied mod root is rejected");
  invalid(file,text.replace(b64(a),b64(snapshot.resolve("mods/A/../B"))),game,home,"noncanonical copied source path is rejected");
  invalid(file,text.replace(b64(user.resolve("mods/OrangeLauncherSession")),b64(user.resolve("mods/AnotherMod"))),game,home,"bridge cannot authorize another live mod");
  invalid(file,text,base,home,"working directory must equal copied game");
  invalid(file,text,game,user,"user.home must equal declared snapshot home");
  invalid(file,text.replace(b64(mods),b64(user)),game,home,"live mod settings directory rejected");
  check(load(file,text.replace("roots="+b64(b)+","+b64(a),"roots="),game,home)!=null,"empty enabled roots are valid");
  Files.write(file,text.getBytes(StandardCharsets.US_ASCII));boolean changed=false;try{read(file,new String(new char[64]).replace('\0','0'),game,home);}catch(InvocationTargetException e){changed=true;}check(changed,"manifest hash mismatch fails before acceptance");
  try{Files.createSymbolicLink(snapshot.resolve("linked"),a);invalid(file,text.replace(b64(b),b64(snapshot.resolve("linked"))),game,home,"symbolic mod roots are rejected");}catch(UnsupportedOperationException e){}catch(java.nio.file.FileSystemException unavailable){}
  System.out.println("PASS "+checks+" frozen manifest binding/order/path/hash checks");
 }
}
