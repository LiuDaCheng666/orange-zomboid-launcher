package ozl.startup;

import java.lang.instrument.Instrumentation;
import java.nio.file.Path;

public final class Agent {
    public static void premain(String arguments,Instrumentation instrumentation){
        try {
            Path jar=Path.of(Agent.class.getProtectionDomain().getCodeSource().getLocation().toURI());
            Session session=Session.read(jar,Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);
            if(session==null)return;
            StartupRuntime.begin(session);Hooks.install(instrumentation);
            System.out.println("[OZL-STARTUP-JAVA] component active");
        }catch(Throwable failure){StartupRuntime.failed(failure);}
    }
}
