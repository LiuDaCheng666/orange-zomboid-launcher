package ozl.startup.tests;

import ozl.startup.*;
import java.awt.image.BufferedImage;
import java.nio.file.*;
import javax.imageio.ImageIO;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Hidden production renderer captures the complete idle repertoire and native connection lanes. */
public final class RefinementGlProbe {
    public static void main(String[] args)throws Exception {
        if(!GLFW.glfwInit())throw new AssertionError("No GLFW context available");
        GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);
        long window=GLFW.glfwCreateWindow(1440,900,"OZL refinement hidden verification",0,0);
        if(window==0)throw new AssertionError("No hidden GL context");
        Path out=Path.of(args[0]);Files.createDirectories(out);
        try{
            GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;Display.width=1440;Display.height=900;
            Session session=Session.read(Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);
            if(session==null)throw new AssertionError("isolated session rejected");
            LoadingModel boot=new LoadingModel();boot.phase("Loading Mods");boot.mod("Project Viewpoint");long[] clock={0};GameRenderer renderer=new GameRenderer(()->clock[0]);
            int frames=180;
            for(int i=0;i<frames;i++){
                clock[0]=(long)(i*.2*1e9);BufferedImage image=HumorGlProbe.draw(renderer,boot,session);
                ImageIO.write(image.getSubimage(470,205,500,450),"png",out.resolve(String.format("gl-repertoire-%03d.png",i)).toFile());
                if(i==30)ImageIO.write(image,"png",out.resolve("gl-boot-context.png").toFile());
            }
            LoadingModel server=new LoadingModel();server.serverBegin("橙子社区服务器","203.0.113.7",16261);server.serverStage("WorkshopUpdate",132,84);server.serverStats("Riverside",84);server.queueInformation(server.snapshot().generation,17,32,1993,7,9,14,35);server.serverClock(server.snapshot().generation,1993,6,8,14,35,48);
            clock[0]=0;renderer=new GameRenderer(()->clock[0]);HumorGlProbe.draw(renderer,server,session);clock[0]=9_000_000_000L;
            ImageIO.write(HumorGlProbe.draw(renderer,server,session),"png",out.resolve("gl-server-context.png").toFile());
            renderer.draw(server.snapshot(),session,4097L<<20,12288L<<20,318d/900,760d/900);
            // This capture draws through the production overload with authoritative native bounds.
            java.nio.ByteBuffer bytes=java.nio.ByteBuffer.allocateDirect(1440*900*4);GL11.glReadPixels(0,0,1440,900,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,bytes);
            BufferedImage nativeFrame=new BufferedImage(1440,900,BufferedImage.TYPE_INT_RGB);
            for(int y=0;y<900;y++)for(int x=0;x<1440;x++){int at=((899-y)*1440+x)*4;nativeFrame.setRGB(x,y,((bytes.get(at)&255)<<16)|((bytes.get(at+1)&255)<<8)|(bytes.get(at+2)&255));}
            ImageIO.write(nativeFrame,"png",out.resolve("gl-server-native-lanes.png").toFile());
            if(GL11.glGetError()!=GL11.GL_NO_ERROR)throw new AssertionError("refined scene left GL errors");
            System.out.println("PASS hidden production GL 36-second repertoire, BOOT/SERVER context and native-bounds capture");
        }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
