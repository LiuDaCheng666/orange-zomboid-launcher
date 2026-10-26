package ozl.startup.tests;

import ozl.startup.*;
import java.awt.image.*;
import java.nio.file.*;
import java.util.Arrays;
import javax.imageio.ImageIO;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Production GL silhouette and reduced-motion check with an explicit clock. */
public final class GrowthGlProbe {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static int orangeHeight(BufferedImage image){int lo=900,hi=-1;for(int y=250;y<550;y++)for(int x=550;x<890;x++){int p=image.getRGB(x,y),r=(p>>16)&255,g=(p>>8)&255,b=p&255;if(r>160&&g>50&&r>g*1.5&&g>b*1.5){lo=Math.min(lo,y);hi=Math.max(hi,y);}}return hi<lo?0:hi-lo+1;}
    static int[] pixels(BufferedImage image){return ((DataBufferInt)image.getRaster().getDataBuffer()).getData();}
    public static void main(String[] args)throws Exception{
        if(!GLFW.glfwInit())throw new AssertionError("No GLFW context available");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1440,900,"OZL growth verification",0,0);check(window!=0,"hidden context exists");
        Path receipt=Path.of("OrangeLauncherRuntime/startup/session.properties");String original=Files.readString(receipt);Path out=Path.of(args[0]);Files.createDirectories(out);
        try{
            GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;Display.width=1440;Display.height=900;
            Path agent=Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),cwd=Path.of(System.getProperty("user.dir"));Session session=Session.read(agent,cwd,System.currentTimeMillis()/1000);check(session!=null,"isolated receipt valid");
            LoadingModel boot=new LoadingModel();boot.phase("Loading Mods");boot.mod("Project Viewpoint");long[] clock={0};GameRenderer renderer=new GameRenderer(()->clock[0]);int smallest=1000,largest=0;
            for(double time:new double[]{0,.5,1.3,2.2,3.1,4.4,6.4,7.4,8.4,10.6}){clock[0]=(long)(time*1e9);BufferedImage image=HumorGlProbe.draw(renderer,boot,session);int height=orangeHeight(image);smallest=Math.min(smallest,height);largest=Math.max(largest,height);ImageIO.write(image,"png",out.resolve("gl-growth-t"+time+".png").toFile());}
            check(smallest>0&&largest/(double)smallest>3,"actual GL body clearly repeats seed-to-grown motion");
            check(GL11.glGetError()==GL11.GL_NO_ERROR,"growth leaves no GL errors");
            Files.writeString(receipt,original.replace("reducedMotion=false","reducedMotion=true"));Session reduced=Session.read(agent,cwd,System.currentTimeMillis()/1000);check(reduced!=null&&reduced.reducedMotion,"reduced receipt valid");
            for(int context=0;context<3;context++){LoadingModel model=new LoadingModel();if(context==1)model.worldBegin(false,"Rosewood","Sandbox","Rosewood");if(context==2)model.serverBegin("橙子社区服务器","203.0.113.7",16261);clock[0]=0;renderer=new GameRenderer(()->clock[0]);BufferedImage a=HumorGlProbe.draw(renderer,model,reduced);clock[0]=90_000_000_000L;BufferedImage b=HumorGlProbe.draw(renderer,model,reduced);check(Arrays.equals(pixels(a),pixels(b)),"actual GL reduced motion stable in context "+context);}
            System.out.println("PASS "+checks+" actual-GL seed/growth/reduced contracts; orange height "+smallest+".."+largest);
        }finally{Files.writeString(receipt,original);Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
