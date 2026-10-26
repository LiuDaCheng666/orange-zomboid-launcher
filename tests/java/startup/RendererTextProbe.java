package ozl.startup.tests;
import ozl.startup.*;
import java.lang.reflect.*;
import java.nio.*;
import java.util.*;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;

/** Real hidden GL checks: recoloring the same glyph run must not rerasterize it. */
public final class RendererTextProbe {
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);System.out.println("PASS "+why);}
    public static void main(String[] args)throws Exception {
        if(!GLFW.glfwInit())throw new AssertionError("No hidden GL context");
        GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(800,240,"OZL text cache verification",0,0);
        try {
            check(window!=0,"hidden context created");GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();
            GL11.glViewport(0,0,800,240);GL11.glMatrixMode(GL11.GL_PROJECTION);GL11.glLoadIdentity();GL11.glOrtho(0,800,240,0,-1,1);GL11.glMatrixMode(GL11.GL_MODELVIEW);GL11.glLoadIdentity();
            GL11.glEnable(GL11.GL_BLEND);GL11.glBlendFunc(GL11.GL_SRC_ALPHA,GL11.GL_ONE_MINUS_SRC_ALPHA);GL11.glTexEnvi(GL11.GL_TEXTURE_ENV,GL11.GL_TEXTURE_ENV_MODE,GL11.GL_MODULATE);
            GameRenderer renderer=new GameRenderer();String value="同一条加载提示";
            renderer.text("字体预热",20,35,24,0xffffffff,false);
            long began=System.nanoTime();for(int i=0;i<128;i++)renderer.text(value,20,90,24,((i+1)<<24)|0x67d8f0,false);
            double elapsed=(System.nanoTime()-began)/1_000_000d;
            Field field=GameRenderer.class.getDeclaredField("words");field.setAccessible(true);Map<?,?> cache=(Map<?,?>)field.get(renderer);
            long entries=cache.keySet().stream().filter(k->k.toString().contains(value)).count();
            System.out.println("CACHE timed128DrawsMs="+elapsed+" recoloredGlyphEntries="+entries);
            check(entries==1,"caption fading reuses one glyph texture instead of rebuilding for every color");
            GL11.glClearColor(0,0,0,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);
            renderer.text(value,20,90,24,0xffff3333,false);renderer.text(value,410,90,24,0xff33ff55,false);
            ByteBuffer pixels=ByteBuffer.allocateDirect(800*240*4);GL11.glReadPixels(0,0,800,240,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);
            int red=0,green=0;for(int y=0;y<240;y++)for(int x=0;x<800;x++){int p=(y*800+x)*4,r=pixels.get(p)&255,g=pixels.get(p+1)&255;if(x<380&&r>80&&r>g*2)red++;if(x>400&&g>80&&g>r*2)green++;}
            check(red>100&&green>100,"reused glyphs retain requested red and green tint in actual GL");
            check(GL11.glGetError()==GL11.GL_NO_ERROR,"text cache rendering leaves no GL error");
        }finally{if(window!=0)GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
