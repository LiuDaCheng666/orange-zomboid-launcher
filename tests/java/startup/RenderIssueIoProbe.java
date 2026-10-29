package ozl.startup.tests;
import ozl.startup.*;
import java.lang.reflect.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.BiFunction;
import java.util.concurrent.atomic.AtomicLong;
import org.lwjgl.glfw.*;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Actual render entry remains available while the only replaced boundary, file I/O, is blocked. */
public final class RenderIssueIoProbe {
 static int checks;static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 public static void main(String[] args)throws Exception{
  Session session=Session.read(Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);check(session!=null,"real receipt accepted");
  if(!GLFW.glfwInit())throw new AssertionError("GL unavailable");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1440,900,"OZL nonblocking issue test",0,0);check(window!=0,"hidden context created");
  CountDownLatch release=new CountDownLatch(1);Object sampler=null;Method close=null;
  try{
   GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;StartupRuntime.begin(session);StartupRuntime.beforeSwap();
   Field field=StartupRuntime.class.getDeclaredField("issueSampler");field.setAccessible(true);Class<?> type=Class.forName("ozl.startup.IssueSampler");close=type.getDeclaredMethod("close");close.setAccessible(true);close.invoke(field.get(null));
   CountDownLatch entered=new CountDownLatch(1);AtomicLong readerThread=new AtomicLong();long rendererThread=Thread.currentThread().getId();
   BiFunction<Long,Long,String[]> source=(now,floor)->{readerThread.set(Thread.currentThread().getId());entered.countDown();try{release.await(3,TimeUnit.SECONDS);}catch(InterruptedException ignored){}return new String[]{"actual worker result","meaning","mod","frame","evidence"};};
   var constructor=type.getDeclaredConstructor(BiFunction.class);constructor.setAccessible(true);sampler=constructor.newInstance(source);field.set(null,sampler);
   long began=System.nanoTime();StartupRuntime.beforeSwap();check(System.nanoTime()-began<TimeUnit.MILLISECONDS.toNanos(500),"real render entry does not wait for blocked issue source");check(entered.await(3,TimeUnit.SECONDS),"render entry requested actual background read");check(readerThread.get()!=rendererThread,"issue I/O runs outside current GL thread");
   began=System.nanoTime();for(int i=0;i<60;i++){StartupRuntime.beforeSwap();GLFW.glfwPollEvents();}check(System.nanoTime()-began<TimeUnit.SECONDS.toNanos(1),"60 actual drawing/event-pump iterations continue while reader is blocked");
   release.countDown();long until=System.nanoTime()+TimeUnit.SECONDS.toNanos(3);while(StartupRuntime.snapshot().issue.length==0&&System.nanoTime()<until){StartupRuntime.beforeSwap();Thread.sleep(3);}check(StartupRuntime.snapshot().issue.length==5&&StartupRuntime.snapshot().issue[0].equals("actual worker result"),"completed evidence reaches the real loading model");
   close.invoke(sampler);sampler=constructor.newInstance((BiFunction<Long,Long,String[]>)session::readIssue);field.set(null,sampler);
   long now=System.currentTimeMillis();StringBuilder payload=new StringBuilder("OZL1\n"+session.nonce+"\n"+now+"\n");for(String value:new String[]{"real local issue","meaning","mod","frame","evidence"})payload.append(Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8))).append('\n');Files.writeString(session.issues,payload,StandardCharsets.UTF_8);
   until=System.nanoTime()+TimeUnit.SECONDS.toNanos(3);do{StartupRuntime.beforeSwap();Thread.sleep(3);}while((StartupRuntime.snapshot().issue.length!=5||!StartupRuntime.snapshot().issue[0].equals("real local issue"))&&System.nanoTime()<until);check(StartupRuntime.snapshot().issue.length==5&&StartupRuntime.snapshot().issue[0].equals("real local issue"),"real bounded issue-file reader updates rendered model");
   Files.writeString(session.issues,"wrong session\n",StandardCharsets.UTF_8);until=System.nanoTime()+TimeUnit.SECONDS.toNanos(3);while(StartupRuntime.snapshot().issue.length>0&&System.nanoTime()<until){StartupRuntime.beforeSwap();Thread.sleep(4);}check(StartupRuntime.snapshot().issue.length==0,"invalid actual file clears prior evidence after refresh");
   check(GL11.glGetError()==GL11.GL_NO_ERROR,"drawing continues without OpenGL error");StartupRuntime.worldEnd();check(!StartupRuntime.snapshot().active,"completion releases rendering to native game");
   System.out.println("PASS "+checks+" actual render entry and file I/O isolation contracts");
  }finally{release.countDown();if(close!=null&&sampler!=null)close.invoke(sampler);Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
}
