package ozl.environment;
import java.nio.file.*;import java.nio.charset.*;import java.io.*;
/** Owned real filesystem checks shared by frozen and live discovery. */
public final class SafePathsTest {
 static int count;interface Operation{void run()throws Exception;}
 static void check(boolean ok,String why){count++;if(!ok)throw new AssertionError(why);}
 static void reject(Operation op,String why)throws Exception{try{op.run();throw new AssertionError(why);}catch(IOException expected){count++;}}
 public static void main(String[] args)throws Exception{
  Path base=Paths.get(args[0]).toAbsolutePath();Files.createDirectories(base);Path dir=base.resolve("directory"),file=dir.resolve("mod.info");Files.createDirectories(dir);Files.write(file,"id=Owned\n".getBytes(StandardCharsets.UTF_8));
  check(SafePaths.canonical(file,true,false).equals(file),"regular file");check(SafePaths.canonical(dir,true,true).equals(dir),"directory");check(SafePaths.bytes(file,32).length==9,"bounded bytes");
  reject(()->SafePaths.canonical(file,true,true),"file as directory");reject(()->SafePaths.canonical(dir,true,false),"directory as file");reject(()->SafePaths.canonical(base.resolve("missing"),true,false),"required missing");
  check(SafePaths.canonical(base.resolve("missing/nested"),false,false).equals(base.resolve("missing/nested")),"optional missing descendant");
  reject(()->SafePaths.canonical(Paths.get("relative"),false,false),"relative path");reject(()->SafePaths.canonical(base.resolve("directory/../directory/mod.info"),true,false),"unnormalized path");reject(()->SafePaths.bytes(file,3),"oversized bytes");
  try{Path link=base.resolve("file-link");if(!Files.exists(link,LinkOption.NOFOLLOW_LINKS))Files.createSymbolicLink(link,file);reject(()->SafePaths.canonical(link,true,false),"leaf symbolic link");Path parent=base.resolve("parent-link");if(!Files.exists(parent,LinkOption.NOFOLLOW_LINKS))Files.createSymbolicLink(parent,dir);reject(()->SafePaths.canonical(parent.resolve("mod.info"),true,false),"symbolic ancestor");reject(()->SafePaths.canonical(parent.resolve("missing"),false,false),"symbolic ancestor of missing path");}catch(FileSystemException|UnsupportedOperationException unavailable){System.out.println("SKIP symbolic-link creation unavailable: "+unavailable.getClass().getSimpleName());}
  if(args.length>1){Path junction=Paths.get(args[1]);reject(()->SafePaths.canonical(junction,true,true),"junction root");reject(()->SafePaths.canonical(junction.resolve("mod.info"),true,false),"junction ancestor");reject(()->SafePaths.canonical(junction.resolve("missing"),false,false),"junction ancestor of optional path");}
  System.out.println("PASS "+count+" real filesystem canonical/identity/linked-path checks");
 }
}
