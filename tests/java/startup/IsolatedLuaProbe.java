package ozl.startup.tests;
import java.net.*;
import java.nio.file.*;
import java.lang.reflect.*;

/** Every zombie dependency must come from fixture classes; game initialization is rejected. */
public final class IsolatedLuaProbe {
 public static void main(String[] args)throws Exception{
  URL classes=Path.of(IsolatedLuaProbe.class.getProtectionDomain().getCodeSource().getLocation().toURI()).toUri().toURL();
  URL game=Path.of(args[1],"projectzomboid.jar").toUri().toURL();
  try(URLClassLoader loader=new URLClassLoader(new URL[]{classes,game},IsolatedLuaProbe.class.getClassLoader()){
   @Override protected Class<?> loadClass(String name,boolean resolve)throws ClassNotFoundException{
    if(name.startsWith("zombie.")||name.startsWith("se.krka.")||name.startsWith("org.luaj.")||name.startsWith("ozl.startup.tests.")&&!name.equals(IsolatedLuaProbe.class.getName())){
     synchronized(getClassLoadingLock(name)){Class<?> value=findLoadedClass(name);if(value==null){
      if(name.startsWith("zombie.")){Path path;try{path=Path.of(classes.toURI()).resolve(name.replace('.','/')+".class");}catch(Exception error){throw new ClassNotFoundException(name,error);}if(!Files.isRegularFile(path))throw new ClassNotFoundException("Real zombie class rejected: "+name);}
      value=findClass(name);
     }if(resolve)resolveClass(value);return value;}
    }
    return super.loadClass(name,resolve);
   }
  }){
   try{loader.loadClass("zombie.SoundManager");throw new AssertionError("Guard allowed real engine");}catch(ClassNotFoundException expected){}
   try{loader.loadClass("ozl.startup.tests."+args[0]).getMethod("main",String[].class).invoke(null,(Object)java.util.Arrays.copyOfRange(args,1,args.length));}
   catch(InvocationTargetException error){Throwable cause=error.getCause();if(cause instanceof Error e)throw e;if(cause instanceof Exception e)throw e;throw error;}
  }
 }
}
