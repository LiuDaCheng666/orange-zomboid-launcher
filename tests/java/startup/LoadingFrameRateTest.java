package ozl.startup.tests;

import ozl.startup.*;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;
import java.lang.reflect.Field;

/** Real premain/swap hooks on a hidden GL context; no game or Steam initialization. */
public final class LoadingFrameRateTest {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static Field last;
    static void elapsed(long nanos)throws Exception{last.setLong(null,System.nanoTime()-nanos);StartupRuntime.idle();}
    public static void main(String[] args)throws Exception{
        check(StartupRuntime.snapshot()!=null&&StartupRuntime.snapshot().active,"isolated premain starts in boot loading");
        last=StartupRuntime.class.getDeclaredField("lastSwap");last.setAccessible(true);
        if(!GLFW.glfwInit())throw new AssertionError("No GLFW context");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);
        long window=GLFW.glfwCreateWindow(1440,900,"OZL hidden loading cadence",0,0);check(window!=0,"hidden GL context created");
        try{
            GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;
            elapsed(1_000_000_000L); // warm fonts, textures and the actual swap hook
            int swaps=Display.swaps;
            for(int i=0;i<12;i++)elapsed(20_000_000L);
            check(Display.swaps==swaps+12,"full loading redraws at 20ms intervals instead of waiting for the old 33ms gate");
            swaps=Display.swaps;elapsed(-1_000_000_000L);check(Display.swaps==swaps,"idle redraw is still paced rather than a busy loop");
            for(int i=0;i<12;i++)Display.swapBuffers();check(Display.swaps==swaps+12,"game-produced frames have no extra loading-page cap");
            StartupRuntime.serverBegin("Fixture server");swaps=Display.swaps;elapsed(20_000_000L);check(Display.swaps==swaps,"native connection controls are never repainted by full-page idle");
            StartupRuntime.handoff(false);elapsed(20_000_000L);check(Display.swaps==swaps,"completed presentation stops idle drawing");
            check(GL11.glGetError()==GL11.GL_NO_ERROR,"cadence change preserves GL state");
            System.out.println("PASS "+checks+" actual-GL loading cadence and native-handoff contracts");
        }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
