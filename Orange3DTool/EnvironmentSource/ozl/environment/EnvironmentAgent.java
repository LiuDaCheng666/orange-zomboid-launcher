package ozl.environment;

import java.lang.instrument.*;
import java.nio.file.*;
import java.security.ProtectionDomain;
import java.util.*;
import net.bytebuddy.agent.builder.AgentBuilder;
import net.bytebuddy.asm.*;
import net.bytebuddy.description.type.TypeDescription;
import static net.bytebuddy.matcher.ElementMatchers.*;

/** Mandatory launch isolation. Failure terminates this JVM before live discovery. */
public final class EnvironmentAgent {
    private static volatile EnvironmentManifest environment;
    private EnvironmentAgent() {}
    public static void premain(String arguments,Instrumentation instrumentation){
        try{
            String file=System.getProperty("ozl.environment");if(file==null||file.length()==0)throw new IllegalArgumentException("environment manifest is required");
            final EnvironmentManifest accepted=EnvironmentManifest.read(Paths.get(file),System.getProperty("ozl.environment.sha256"),Paths.get(System.getProperty("user.dir")),Paths.get(System.getProperty("user.home")));
            TargetContract.verifyJar(accepted.game);
            for(Class<?> loaded:instrumentation.getAllLoadedClasses())if(loaded.getName().equals("zombie.ZomboidFileSystem"))throw new IllegalStateException("mod discovery loaded before mandatory agent");
            environment=accepted;
            final java.lang.reflect.Method modUserFolder=EnvironmentAgent.class.getMethod("modUserFolder");
            instrumentation.addTransformer(new ClassFileTransformer(){
                @Override public byte[] transform(ClassLoader loader,String name,Class<?> previous,ProtectionDomain domain,byte[] bytes){
                    if(TargetContract.TARGET.equals(name))try{guardSource(loader,domain);TargetContract.verify(bytes);}catch(Throwable failure){stop("discovery input contract failed",failure);}return null;
                }
            });
            new AgentBuilder.Default().disableClassFormatChanges()
                .with(AgentBuilder.InjectionStrategy.Disabled.INSTANCE).with(AgentBuilder.InitializationStrategy.NoOp.INSTANCE)
                .with(new AgentBuilder.Listener.Adapter(){
                    @Override public void onError(String name,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,Throwable failure){stop("required discovery transformation failed",failure);}
                    @Override public void onTransformation(TypeDescription type,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,net.bytebuddy.dynamic.DynamicType dynamicType){System.out.println("[OZL-ENVIRONMENT] mod discovery isolated");}
                })
                .type(named("zombie.ZomboidFileSystem"))
                .transform((builder,type,loader,module,domain)->builder
                    .visit(Advice.to(Discovery.class).on(named("getAllModFolders").and(takesArguments(List.class)).and(returns(void.class)).and(not(isStatic()))))
                    .visit(MemberSubstitution.strict().method(named("getMyDocumentFolder").and(isDeclaredBy(named("zombie.core.Core"))).and(takesArguments(0)).and(returns(String.class)).and(isStatic()))
                        .replaceWith(modUserFolder)
                        .on(namedOneOf("readDefaultModsTxt","readLoadedDotTxt","saveModsFile"))))
                .installOn(instrumentation);
            System.out.println("[OZL-ENVIRONMENT] frozen environment required");
        }catch(Throwable failure){stop("mandatory environment initialization failed",failure);}
    }
    private static void guardSource(ClassLoader loader,ProtectionDomain domain)throws Exception{
        if(environment==null||loader==null||Class.forName(EnvironmentAgent.class.getName(),false,loader)!=EnvironmentAgent.class||domain==null||domain.getCodeSource()==null)throw new IllegalStateException("isolated agent is not visible to game loader");
        Path actual=SafePaths.canonical(Paths.get(domain.getCodeSource().getLocation().toURI()),true,false);
        if(!actual.equals(environment.game.resolve("projectzomboid.jar")))throw new IllegalStateException("discovery class is outside frozen game");
    }
    public static boolean replace(List<String> output){
        try{
            EnvironmentManifest accepted=environment;if(accepted==null||output==null)throw new IllegalStateException("missing required discovery state");
            List<String> roots=accepted.discoveryRoots();output.clear();output.addAll(roots);return true;
        }catch(Throwable failure){stop("frozen mod discovery failed",failure);throw new AssertionError("JVM termination returned");}
    }
    public static String modUserFolder(){
        try{EnvironmentManifest accepted=environment;if(accepted==null)throw new IllegalStateException("missing required mod settings state");SafePaths.canonical(accepted.mods,true,true);return accepted.mods.getParent().toString();}
        catch(Throwable failure){stop("frozen mod settings path failed",failure);throw new AssertionError("JVM termination returned");}
    }
    private static void stop(String reason,Throwable failure){
        System.err.println("[OZL-ENVIRONMENT] isolation failure: "+reason+" ("+failure.getClass().getSimpleName()+")");System.err.flush();Runtime.getRuntime().halt(78);throw new AssertionError("Unable to stop unsafe launch",failure);
    }
    public static final class Discovery {
        @Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class)
        public static boolean enter(@Advice.Argument(0)List<String> folders){return EnvironmentAgent.replace(folders);}
    }
}
