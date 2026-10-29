package ozl.startup.tests;
import ozl.startup.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;
import java.nio.*;
import java.nio.file.*;
import java.lang.management.ManagementFactory;
import java.util.*;
import java.util.concurrent.atomic.AtomicLong;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Shared production renderer on a hidden actual GL context; not a full game acceptance. */
public final class LoadingRenderCostProbe {
 static double percentile(long[] values,double p){long[] sorted=values.clone();Arrays.sort(sorted);return sorted[(int)Math.floor((sorted.length-1)*p)]/1_000_000d;}
 static void image(Path output)throws Exception {int w=Display.width,h=Display.height;ByteBuffer data=ByteBuffer.allocateDirect(w*h*4);GL11.glReadPixels(0,0,w,h,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,data);BufferedImage image=new BufferedImage(w,h,BufferedImage.TYPE_INT_RGB);for(int y=0;y<h;y++)for(int x=0;x<w;x++){int at=((h-y-1)*w+x)*4;image.setRGB(x,y,((data.get(at)&255)<<16)|((data.get(at+1)&255)<<8)|(data.get(at+2)&255));}ImageIO.write(image,"png",output.toFile());}
 public static void main(String[] args)throws Exception{
  Path output=Path.of(args[0]);Files.createDirectories(output);
  if(!GLFW.glfwInit())throw new AssertionError("GL unavailable");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1440,900,"OZL hidden cost probe",0,0);
  if(window==0)throw new AssertionError("Hidden window unavailable");
  try{
   GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;
   Session session=Session.read(Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);if(session==null)throw new AssertionError("Fixture receipt rejected");
   var bean=ManagementFactory.getThreadMXBean();bean.setThreadCpuTimeEnabled(true);List<String> metrics=new ArrayList<>();
   for(String scene:new String[]{"boot","server","save","download"}){
    LoadingModel model=new LoadingModel();
    if(scene.equals("server")||scene.equals("download")){model.serverBegin("橙子加载性能测试","127.0.0.1",16261);model.serverStats("Muldraugh, KY",143);model.serverStage("Finish",132,143);}
    if(scene.equals("save"))model.worldBegin(false,"测试存档","Sandbox","Muldraugh, KY");
    if(scene.equals("download")){model.downloadPhase("download");model.workshop(132,4194304,8388608);model.download(true,"1234567890","实际下载项目",14,4194304,8388608,1048576);}
    String detail=("长目录/模组原始文件/".repeat(40)).substring(0,240);for(int i=0;i<8;i++)model.script("media/lua/client/"+detail+i+".lua");model.issue(new String[]{"模组加载提示",detail,detail,detail,detail});
    AtomicLong clock=new AtomicLong();GameRenderer renderer=new GameRenderer(clock::get);var state=model.snapshot();
    for(int i=0;i<24;i++){clock.addAndGet(16666667);renderer.draw(state,session,2L<<30,8L<<30);}
    long[] wall=new long[90];long cpuStart=bean.getCurrentThreadCpuTime();
    for(int i=0;i<90;i++){clock.addAndGet(16666667);long start=System.nanoTime();renderer.draw(state,session,(2L<<30)+(i/4L<<20),8L<<30);GL11.glFinish();wall[i]=System.nanoTime()-start;GLFW.glfwPollEvents();}
    double cpuAverage=(bean.getCurrentThreadCpuTime()-cpuStart)/90_000_000d;String metric=String.format(Locale.ROOT,"{\"scene\":\"%s\",\"frames\":90,\"cpuAverageMs\":%.4f,\"wallP50Ms\":%.4f,\"wallP95Ms\":%.4f}",scene,cpuAverage,percentile(wall,.5),percentile(wall,.95));System.out.println(metric);metrics.add(metric);image(output.resolve(scene+".png"));
    if(GL11.glGetError()!=GL11.GL_NO_ERROR)throw new AssertionError("GL state error: "+scene);
   }
   Files.writeString(output.resolve("metrics.json"),"[\n"+String.join(",\n",metrics)+"\n]\n");
  }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
}
