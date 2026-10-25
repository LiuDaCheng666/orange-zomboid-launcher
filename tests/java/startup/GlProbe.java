package ozl.startup.tests;
import ozl.startup.*;
import java.nio.*;
import java.nio.file.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** A hidden real OpenGL context exercises the production renderer, including
 * the non-default texture state left by an actual game renderer. */
public final class GlProbe {
    static void check(boolean value,String message){if(!value)throw new AssertionError(message);}
    public static void main(String[] args)throws Exception {
        if(!GLFW.glfwInit())throw new AssertionError("No GLFW context available");
        GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);
        long window=GLFW.glfwCreateWindow(1440,900,"OZL render regression",0,0);
        if(window==0)throw new AssertionError("No compatible GL window");
        try {
            GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;
            Session session=Session.read(Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);
            check(session!=null,"render fixture receipt rejected");
            GL11.glViewport(7,9,700,500);GL11.glEnable(GL11.GL_SCISSOR_TEST);GL11.glScissor(20,20,4,4);
            GL13.glActiveTexture(GL13.GL_TEXTURE1);int black=GL11.glGenTextures();GL11.glBindTexture(GL11.GL_TEXTURE_2D,black);GL11.glEnable(GL11.GL_TEXTURE_2D);GL11.glTexParameteri(GL11.GL_TEXTURE_2D,GL11.GL_TEXTURE_MIN_FILTER,GL11.GL_NEAREST);GL11.glTexParameteri(GL11.GL_TEXTURE_2D,GL11.GL_TEXTURE_MAG_FILTER,GL11.GL_NEAREST);GL11.glTexImage2D(GL11.GL_TEXTURE_2D,0,GL11.GL_RGBA,1,1,0,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,ByteBuffer.allocateDirect(4));
            GL13.glActiveTexture(GL13.GL_TEXTURE0);GL11.glMatrixMode(GL11.GL_TEXTURE);GL11.glLoadIdentity();GL11.glTranslated(.35,0,0);
            GL13.glActiveTexture(GL13.GL_TEXTURE1);
            int unpack=GL15.glGenBuffers();GL15.glBindBuffer(GL21.GL_PIXEL_UNPACK_BUFFER,unpack);GL15.glBufferData(GL21.GL_PIXEL_UNPACK_BUFFER,32,GL15.GL_STATIC_DRAW);
            // A short row stride keeps the regression safe even before isolation:
            // the inherited offsets corrupt the image without exceeding its full buffer.
            GL11.glPixelStorei(GL11.GL_UNPACK_ROW_LENGTH,1);GL11.glPixelStorei(GL11.GL_UNPACK_SKIP_ROWS,1);GL11.glPixelStorei(GL11.GL_UNPACK_SKIP_PIXELS,1);
            LoadingModel model=new LoadingModel();model.phase("Loading Mods");model.mod("Actual API fixture");
            new GameRenderer().draw(model.snapshot(),session,2L<<30,12L<<30);
            check(GL11.glIsEnabled(GL11.GL_SCISSOR_TEST),"scissor enable state changed");
            check(GL11.glGetInteger(GL13.GL_ACTIVE_TEXTURE)==GL13.GL_TEXTURE1,"active texture changed");
            check(GL11.glIsEnabled(GL11.GL_TEXTURE_2D),"secondary texture enable state changed");
            check(GL11.glGetInteger(GL21.GL_PIXEL_UNPACK_BUFFER_BINDING)==unpack,"game pixel upload buffer changed");
            check(GL11.glGetInteger(GL11.GL_UNPACK_ROW_LENGTH)==1,"game unpack row length changed");
            check(GL11.glGetInteger(GL11.GL_UNPACK_SKIP_ROWS)==1,"game unpack row skip changed");
            check(GL11.glGetInteger(GL11.GL_UNPACK_SKIP_PIXELS)==1,"game unpack pixel skip changed");
            IntBuffer viewport=ByteBuffer.allocateDirect(16).order(ByteOrder.nativeOrder()).asIntBuffer();GL11.glGetIntegerv(GL11.GL_VIEWPORT,viewport);
            check(viewport.get(0)==7&&viewport.get(2)==700,"game viewport changed");
            GL11.glReadBuffer(GL11.GL_BACK);ByteBuffer pixels=ByteBuffer.allocateDirect(1440*900*4);GL11.glReadPixels(0,0,1440,900,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);
            BufferedImage image=new BufferedImage(1440,900,BufferedImage.TYPE_INT_ARGB);int orange=0;long sumX=0;
            for(int y=0;y<900;y++)for(int x=0;x<1440;x++){int at=((899-y)*1440+x)*4,r=pixels.get(at)&255,g=pixels.get(at+1)&255,b=pixels.get(at+2)&255;image.setRGB(x,y,0xff000000|(r<<16)|(g<<8)|b);if(x>630&&x<810&&y>290&&y<480&&r>150&&r>g*1.25&&g>b*1.1){orange++;sumX+=x;}}
            Files.createDirectories(Path.of(args[0]));ImageIO.write(image,"png",Path.of(args[0],"actual-gl.png").toFile());
            check(orange>700,"game texture state prevented logo rendering");
            check(Math.abs(sumX/(double)orange-720)<18,"game texture matrix moved the orange mark");
            check(GL11.glGetError()==GL11.GL_NO_ERROR,"renderer left an OpenGL error");
            System.out.println("PASS real OpenGL renderer and preserved game state");
        }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
