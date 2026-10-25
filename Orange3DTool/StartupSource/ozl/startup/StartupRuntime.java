package ozl.startup;

import java.lang.reflect.Field;
import org.lwjgl.opengl.GL;

/** Entry points called by advice; none of these methods can own a game operation. */
public final class StartupRuntime {
    private static volatile LoadingModel model;
    private static Session session;
    private static GameRenderer renderer;
    private static long lastSwap,lastIssueRead;
    private static volatile long issueFloor;
    private static boolean drawing,reportedFailure;
    public static void begin(Session accepted){session=accepted;model=new LoadingModel();}
    public static LoadingModel.Snapshot snapshot(){LoadingModel value=model;return value==null?null:value.snapshot();}
    public static void phase(String value){LoadingModel m=model;if(m!=null)m.phase(value);}
    public static void translation(String key,String label){LoadingModel m=model;if(m!=null)m.translation(key,label);}
    public static void mod(String value){LoadingModel m=model;if(m!=null)m.mod(value);}
    public static void script(String value){LoadingModel m=model;if(m!=null)m.script(value);}
    public static void menuReady(){LoadingModel m=model;if(m!=null){m.menuReady();System.out.println("[OZL-STARTUP-JAVA] game-menu");}}
    public static void worldBegin(){LoadingModel m=model;if(m!=null){issueFloor=System.currentTimeMillis();m.worldBegin();}}
    public static void worldEnd(){LoadingModel m=model;if(m!=null)m.worldEnd();}
    private static boolean flag(Class<?> type,String name){try{Field f=type.getDeclaredField(name);f.setAccessible(true);return f.getBoolean(null);}catch(ReflectiveOperationException unavailable){return false;}}
    public static void worldFrame(Object state){
        LoadingModel m=model;if(m==null||state==null)return;
        Class<?> type=state.getClass();
        if(flag(type,"worldVersionError")||flag(type,"unexpectedError")||flag(type,"playerWrongIP")||flag(type,"done")){m.waitingForInput();return;}
        try{
            Class<?> game=Class.forName("zombie.GameWindow",false,type.getClassLoader());
            if(flag(game,"serverDisconnected")){m.waitingForInput();return;}
            Object value=type.getField("gameLoadingString").get(null);if(value instanceof String text)m.worldStatus(text);
        }catch(ReflectiveOperationException ignored){}
    }
    private static boolean currentContext(){try{return GameDisplay.created()&&GL.getCapabilities()!=null;}catch(Throwable notCurrent){return false;}}
    public static void beforeSwap(){
        lastSwap=System.nanoTime();LoadingModel m=model;
        if(m==null||drawing||!m.snapshot().active||!currentContext())return;
        drawing=true;
        try {
            long now=System.currentTimeMillis();if(now-lastIssueRead>=250){lastIssueRead=now;m.issue(session.readIssue(now,issueFloor));}
            if(renderer==null)renderer=new GameRenderer();
            java.lang.Runtime memory=java.lang.Runtime.getRuntime();
            renderer.draw(m.snapshot(),session,memory.totalMemory()-memory.freeMemory(),memory.maxMemory());
        }catch(Throwable failure){failed(failure);}finally{drawing=false;}
    }
    /** During game-thread loading the render queue can be empty. Use that same
     * render thread/context to present at most 30 idle frames per second. */
    public static void idle(){
        LoadingModel m=model;
        if(m==null||drawing||!m.snapshot().active||System.nanoTime()-lastSwap<33_333_333L||!currentContext())return;
        try{GameDisplay.swap();}catch(Throwable failure){failed(failure);}
    }
    public static void failed(Throwable failure){
        LoadingModel m=model;if(m!=null)m.disable();
        if(!reportedFailure){reportedFailure=true;System.err.println("[OZL-STARTUP-JAVA] presentation disabled: "+failure.getClass().getSimpleName());}
    }
}
