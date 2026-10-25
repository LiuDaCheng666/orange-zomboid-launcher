package ozl.startup;

import java.lang.reflect.Method;

/** The game ships a newer JVM class version than our Java 17 component.
 * Resolve this small, stable display boundary without linking game classes
 * during compilation or distributing any of those classes. */
final class GameDisplay {
    private static final class Api {
        static final Class<?> TYPE=type();
        static final Method CREATED=method("isCreated"),WIDTH=method("getWidth"),HEIGHT=method("getHeight"),SWAP=method("swapBuffers");
        static Class<?> type(){try{return Class.forName("org.lwjglx.opengl.Display",false,ClassLoader.getSystemClassLoader());}catch(Exception error){throw new IllegalStateException(error);}}
        static Method method(String name){try{return TYPE.getMethod(name);}catch(Exception error){throw new IllegalStateException(error);}}
    }
    static boolean created()throws Exception{return (Boolean)Api.CREATED.invoke(null);}
    static int width()throws Exception{return (Integer)Api.WIDTH.invoke(null);}
    static int height()throws Exception{return (Integer)Api.HEIGHT.invoke(null);}
    static void swap()throws Exception{Api.SWAP.invoke(null);}
}
