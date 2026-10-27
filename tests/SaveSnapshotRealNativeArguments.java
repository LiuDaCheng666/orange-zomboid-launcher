import java.lang.instrument.Instrumentation;
import java.lang.reflect.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.io.*;
import java.util.*;

/** Read-only premain diagnostics; never changes JVM arguments, game state or files. */
public final class SaveSnapshotRealNativeArguments {
    static String quote(String value){return value==null?"null":"\""+value.replace("\\","\\\\").replace("\"","\\\"").replace("\n","\\n").replace("\r","\\r")+"\"";}
    public static void premain(String output,Instrumentation ignored)throws Exception {
        Map<String,String> values=new LinkedHashMap<>();
        for(String name:new String[]{"user.dir","user.home","ozl.environment","ozl.environment.sha256","java.home","java.class.path","sun.jnu.encoding","native.encoding","file.encoding"})values.put(name,System.getProperty(name));
        String file=System.getProperty("ozl.environment");
        try{values.put("manifestRealPath",Path.of(file).toRealPath().toString());values.put("manifestBytes",Long.toString(Files.size(Path.of(file))));}catch(Throwable error){values.put("manifestReadError",error.toString());}
        try{Class<?> manifest=Class.forName("ozl.environment.EnvironmentManifest");manifest.getMethod("read",Path.class,String.class,Path.class,Path.class).invoke(null,Path.of(file),System.getProperty("ozl.environment.sha256"),Path.of(System.getProperty("user.dir")),Path.of(System.getProperty("user.home")));values.put("actualProductionManifestRead","PASS");}
        catch(Throwable error){while(error instanceof InvocationTargetException&&error.getCause()!=null)error=error.getCause();values.put("actualProductionManifestRead",error.toString());StringWriter stack=new StringWriter();error.printStackTrace(new PrintWriter(stack));Files.writeString(Path.of(output+".stack.txt"),stack.toString(),StandardCharsets.UTF_8);}
        StringJoiner json=new StringJoiner(",","{","}");for(Map.Entry<String,String> value:values.entrySet())json.add(quote(value.getKey())+":"+quote(value.getValue()));Files.writeString(Path.of(output),json.toString(),StandardCharsets.UTF_8);
    }
}
