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
            if(accepted.activeConfigured)ActiveTargetContract.verifyJar(accepted.game);else TargetContract.verifyJar(accepted.game);
            for(Class<?> loaded:instrumentation.getAllLoadedClasses())if((accepted.activeConfigured?ActiveTargetContract.target(loaded.getName().replace('.','/')):TargetContract.target(loaded.getName().replace('.','/'))))throw new IllegalStateException("mod discovery loaded before mandatory agent");
            environment=accepted;if(accepted.activeConfigured)ActiveSelectionHooks.configure(accepted.game,accepted.user,accepted.mods.getParent(),true,accepted::normalizeActive,accepted::mergeClient);
            final java.lang.reflect.Method modUserFolder=EnvironmentAgent.class.getMethod("modUserFolder");
            instrumentation.addTransformer(new ClassFileTransformer(){
                @Override public byte[] transform(ClassLoader loader,String name,Class<?> previous,ProtectionDomain domain,byte[] bytes){
                    if(accepted.activeConfigured?ActiveTargetContract.target(name):TargetContract.target(name))try{guardSource(loader,domain);if(accepted.activeConfigured)ActiveTargetContract.verify(name,bytes);else TargetContract.verify(name,bytes);}catch(Throwable failure){stop("discovery input contract failed",failure);}return null;
                }
            });
            new AgentBuilder.Default().disableClassFormatChanges()
                .with(AgentBuilder.InjectionStrategy.Disabled.INSTANCE).with(AgentBuilder.InitializationStrategy.NoOp.INSTANCE)
                .with(new AgentBuilder.Listener.Adapter(){
                    @Override public void onError(String name,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,Throwable failure){stop("required discovery transformation failed",failure);}
                    @Override public void onTransformation(TypeDescription type,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,net.bytebuddy.dynamic.DynamicType dynamicType){System.out.println("[OZL-ENVIRONMENT] mod discovery isolated");}
                })
                .type(namedOneOf("zombie.ZomboidFileSystem","zombie.modding.ActiveMods","zombie.modding.ActiveModsFile","zombie.gameStates.ChooseGameInfo$Mod"))
                .transform((builder,type,loader,module,domain)->{
                    if(accepted.activeConfigured)builder=ActiveSelectionHooks.apply(builder,type.getName());
                    if(type.getName().equals("zombie.modding.ActiveModsFile"))return builder;
                    if(type.getName().equals("zombie.modding.ActiveMods")&&accepted.activeConfigured)return builder;
                    if(type.getName().equals("zombie.modding.ActiveMods"))return builder.visit(Advice.to(ModChoices.class).on(named("getMods").and(takesArguments(0)).and(returns(ArrayList.class)).and(not(isStatic()))));
                    if(type.getName().equals("zombie.gameStates.ChooseGameInfo$Mod")&&accepted.activeConfigured)return builder.visit(Advice.to(ScopedRequirements.class).on(named("getRequire").and(takesArguments(0)).and(returns(ArrayList.class)).and(not(isStatic()))));
                    if(type.getName().equals("zombie.gameStates.ChooseGameInfo$Mod"))return builder
                        .visit(Advice.to(ModChoices.class).on(named("getRequire").and(takesArguments(0)).and(returns(ArrayList.class)).and(not(isStatic()))))
                        .visit(Advice.to(ParsedRequirements.class).on(named("setRequire").and(takesArguments(ArrayList.class)).and(returns(void.class)).and(not(isStatic()))));
                    return builder
                    .visit(Advice.to(Discovery.class).on(named("getAllModFolders").and(takesArguments(List.class)).and(returns(void.class)).and(not(isStatic()))))
                    .visit(MemberSubstitution.strict().method(named("getMyDocumentFolder").and(isDeclaredBy(named("zombie.core.Core"))).and(takesArguments(0)).and(returns(String.class)).and(isStatic()))
                        .replaceWith(modUserFolder)
                        .on(namedOneOf("readDefaultModsTxt","readLoadedDotTxt","saveModsFile")));})
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
    public static void normalizeMods(List<String> mods){
        try{EnvironmentManifest accepted=environment;if(accepted==null||mods==null)throw new IllegalStateException("missing active mods state");accepted.normalizeMods(mods);}
        catch(Throwable failure){stop("explicit mod choice application failed",failure);}
    }
    private static void stop(String reason,Throwable failure){
        System.err.println("[OZL-ENVIRONMENT] isolation failure: "+reason+" ("+failure.getClass().getSimpleName()+")");System.err.flush();Runtime.getRuntime().halt(78);throw new AssertionError("Unable to stop unsafe launch",failure);
    }
    public static final class Discovery {
        @Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class)
        public static boolean enter(@Advice.Argument(0)List<String> folders){return EnvironmentAgent.replace(folders);}
    }
    public static final class ModChoices {
        @Advice.OnMethodExit public static void exit(@Advice.Return ArrayList<String> mods){if(mods!=null)EnvironmentAgent.normalizeMods(mods);}
    }
    public static ArrayList<String> requirements(Object owner,ArrayList<String> original){
        try{if(original==null||ActiveSelectionHooks.client(owner.getClass().getClassLoader()))return original;ArrayList<String> copy=new ArrayList<String>(original);environment.normalizeMods(copy);return copy;}
        catch(Throwable failure){stop("scoped dependency choice failed",failure);return original;}
    }
    public static final class ScopedRequirements {
        @Advice.OnMethodExit public static void exit(@Advice.This Object owner,@Advice.Return(readOnly=false) ArrayList<String> mods){mods=EnvironmentAgent.requirements(owner,mods);}
    }
    public static final class ParsedRequirements {
        @Advice.OnMethodEnter public static void enter(@Advice.Argument(0) ArrayList<String> mods){if(mods!=null)EnvironmentAgent.normalizeMods(mods);}
    }
}
