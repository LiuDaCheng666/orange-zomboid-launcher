package ozl.startup;

import java.lang.reflect.*;
import net.bytebuddy.ByteBuddy;
import net.bytebuddy.ClassFileVersion;
import net.bytebuddy.description.modifier.*;
import net.bytebuddy.dynamic.loading.ClassLoadingStrategy;
import net.bytebuddy.implementation.*;
import static net.bytebuddy.matcher.ElementMatchers.*;

/** One fixed owned adapter. Game classes are resolved, never bundled or replaced. */
public final class QueuedBackdrop {
    public static final class Ticket {
        public final LoadingModel.Snapshot state;
        public final String scope;
        public final double statusBottom,cancelTop;
        public Ticket(LoadingModel.Snapshot state,String scope){this(state,scope,null);}
        public Ticket(LoadingModel.Snapshot state,String scope,NativeConnectionPresentation.Lease lease){this.state=state;this.scope=scope;statusBottom=lease==null||lease.queueScope?0:lease.statusBottom;cancelTop=lease==null||lease.queueScope?0:lease.cancelTop;}
    }
    private static ClassLoader parent;
    private static Constructor<?> constructor;
    private static Method enqueue;
    private static Field instance;
    public static synchronized boolean enqueue(ClassLoader loader,Ticket ticket)throws Exception{
        if(constructor==null){
            if(Class.forName(StartupRuntime.class.getName(),false,loader)!=StartupRuntime.class||Class.forName(Ticket.class.getName(),false,loader)!=Ticket.class)return false;
            Class<?> drawer=Class.forName("zombie.core.textures.TextureDraw$GenericDrawer",false,loader),sprites=Class.forName("zombie.core.SpriteRenderer",false,loader);
            Method render=drawer.getDeclaredMethod("render");Constructor<?> base=drawer.getConstructor();
            if(!Modifier.isPublic(drawer.getModifiers())||!Modifier.isAbstract(render.getModifiers())||render.getReturnType()!=void.class)return false;
            Method submit=sprites.getMethod("drawGeneric",drawer);if(submit.getReturnType()!=Class.forName("zombie.core.textures.TextureDraw",false,loader))return false;
            Class<?> generated=new ByteBuddy(ClassFileVersion.JAVA_V17).subclass(drawer).name("ozl.startup.GameQueuedDrawer")
                .defineField("ticket",Ticket.class,Visibility.PRIVATE,FieldManifestation.FINAL)
                .defineConstructor(Visibility.PUBLIC).withParameters(Ticket.class)
                .intercept(MethodCall.invoke(base).andThen(FieldAccessor.ofField("ticket").setsArgumentAt(0)))
                .method(named("render").and(takesArguments(0)).and(returns(void.class)))
                .intercept(MethodCall.invoke(StartupRuntime.class.getMethod("queuedDraw",Ticket.class)).withField("ticket"))
                .make().load(loader,ClassLoadingStrategy.Default.WRAPPER).getLoaded();
            constructor=generated.getConstructor(Ticket.class);enqueue=submit;instance=sprites.getField("instance");parent=loader;
        }
        if(parent!=loader)return false;
        return enqueue.invoke(instance.get(null),constructor.newInstance(ticket))!=null;
    }
}
