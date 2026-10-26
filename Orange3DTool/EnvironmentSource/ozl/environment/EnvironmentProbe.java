package ozl.environment;

import java.nio.file.Path;
import java.nio.file.Paths;

/** Pure pre-copy compatibility check: no game class loading or instrumentation. */
public final class EnvironmentProbe {
    private EnvironmentProbe() {}
    public static void main(String[] arguments){
        boolean supported=false;
        try{
            if(arguments==null||arguments.length!=1)throw new IllegalArgumentException("one game path required");
            Path game=SafePaths.canonical(Paths.get(arguments[0]),true,true);
            TargetContract.verifyJar(game);supported=true;
        }catch(Throwable unsupported){/* The caller receives a stable machine result only. */}
        System.out.println(supported?"PASS":"UNSUPPORTED");
        if(!supported)System.exit(2);
    }
}
