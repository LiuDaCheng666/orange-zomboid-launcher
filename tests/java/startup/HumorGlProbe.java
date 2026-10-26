package ozl.startup.tests;

import ozl.startup.*;
import java.nio.*;
import java.nio.file.*;
import java.awt.image.BufferedImage;
import java.util.Arrays;
import java.util.function.LongSupplier;
import javax.imageio.ImageIO;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Hidden production-GL verification; runs only in a disposable fixture directory. */
public final class HumorGlProbe {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static BufferedImage draw(GameRenderer renderer,LoadingModel model,Session session)throws Exception{
        renderer.draw(model.snapshot(),session,4097L<<20,12288L<<20);
        ByteBuffer bytes=ByteBuffer.allocateDirect(1440*900*4);GL11.glReadPixels(0,0,1440,900,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,bytes);
        BufferedImage image=new BufferedImage(1440,900,BufferedImage.TYPE_INT_ARGB);
        for(int y=0;y<900;y++)for(int x=0;x<1440;x++){int at=((899-y)*1440+x)*4;image.setRGB(x,y,0xff000000|((bytes.get(at)&255)<<16)|((bytes.get(at+1)&255)<<8)|(bytes.get(at+2)&255));}
        return image;
    }
    public static void main(String[] args)throws Exception{
        java.lang.reflect.Constructor<GameRenderer> constructor;
        try{constructor=GameRenderer.class.getConstructor(LongSupplier.class);}catch(NoSuchMethodException missing){throw new AssertionError("production renderer needs an explicit monotonic clock for deterministic waiting captions",missing);}
        if(!GLFW.glfwInit())throw new AssertionError("No GLFW context available");
        GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1440,900,"OZL caption verification",0,0);check(window!=0,"hidden GL context exists");
        Path receipt=Path.of("OrangeLauncherRuntime/startup/session.properties");String original=Files.readString(receipt);
        try{
            GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;Display.width=1440;Display.height=900;
            Path agent=Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),cwd=Path.of(System.getProperty("user.dir"));Session session=Session.read(agent,cwd,System.currentTimeMillis()/1000);check(session!=null,"isolated session is valid");
            long[] nanos={0};GameRenderer renderer=constructor.newInstance((LongSupplier)()->nanos[0]);LoadingModel model=new LoadingModel();model.phase("Loading Mods");model.mod("Actual API fixture");
            BufferedImage initial=draw(renderer,model,session);nanos[0]=7_500_000_000L;BufferedImage joke=draw(renderer,model,session);nanos[0]=13_000_000_000L;BufferedImage quiet=draw(renderer,model,session);
            int changed=0;for(int y=550;y<570;y++)for(int x=560;x<880;x++)if(joke.getRGB(x,y)!=quiet.getRGB(x,y))changed++;
            check(changed>100,"actual GL paints the delayed caption beneath the observed loading phase");
            check(GL11.glGetError()==GL11.GL_NO_ERROR,"caption rendering leaves no GL error");Path out=Path.of(args[0]);Files.createDirectories(out);ImageIO.write(initial,"png",out.resolve("gl-boot-initial.png").toFile());ImageIO.write(joke,"png",out.resolve("gl-boot-humor.png").toFile());ImageIO.write(quiet,"png",out.resolve("gl-boot-quiet.png").toFile());
            Files.writeString(receipt,original.replace("reducedMotion=false","reducedMotion=true"));Session reduced=Session.read(agent,cwd,System.currentTimeMillis()/1000);check(reduced!=null&&reduced.reducedMotion,"reduced-motion fixture is valid");nanos[0]=0;renderer=constructor.newInstance((LongSupplier)()->nanos[0]);BufferedImage a=draw(renderer,model,reduced);nanos[0]=90_000_000_000L;BufferedImage b=draw(renderer,model,reduced);
            check(Arrays.equals(((java.awt.image.DataBufferInt)a.getRaster().getDataBuffer()).getData(),((java.awt.image.DataBufferInt)b.getRaster().getDataBuffer()).getData()),"actual GL remains pixel-identical with reduced motion");ImageIO.write(a,"png",out.resolve("gl-boot-reduced.png").toFile());
            System.out.println("PASS "+checks+" deterministic-clock/actual-GL caption/reduced checks");
        }finally{Files.writeString(receipt,original);Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
