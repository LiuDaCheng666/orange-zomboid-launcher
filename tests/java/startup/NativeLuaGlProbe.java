package ozl.startup.tests;
import ozl.startup.*;
import zombie.ui.*;
import zombie.core.SpriteRenderer;
import zombie.gameStates.MainScreenState;
import org.lwjglx.opengl.Display;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import java.nio.*;
import java.nio.file.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;

/** Actual extracted Lua draw commands composite after the production queued GL scene. */
public final class NativeLuaGlProbe {
 static int checks;static void check(boolean v,String why){if(!v)throw new AssertionError(why);checks++;}
 static void rect(double x,double y,double w,double h,double r,double g,double b,double a){GL11.glDisable(GL11.GL_TEXTURE_2D);GL11.glColor4d(r,g,b,a);GL11.glBegin(GL11.GL_QUADS);GL11.glVertex2d(x,y);GL11.glVertex2d(x+w,y);GL11.glVertex2d(x+w,y+h);GL11.glVertex2d(x,y+h);GL11.glEnd();}
 static double number(UIElement e,String key){return ((Number)e.getTable().rawget(key)).doubleValue();}
 static BufferedImage capture(int w,int h,Path path)throws Exception{ByteBuffer p=ByteBuffer.allocateDirect(w*h*4);GL11.glReadPixels(0,0,w,h,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,p);BufferedImage image=new BufferedImage(w,h,BufferedImage.TYPE_INT_RGB);for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=((h-y-1)*w+x)*4;image.setRGB(x,y,((p.get(i)&255)<<16)|((p.get(i+1)&255)<<8)|(p.get(i+2)&255));}ImageIO.write(image,"png",path.toFile());return image;}
 static int white(BufferedImage image,int x,int y,int w,int h){int count=0;for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++){int p=image.getRGB(xx,yy);if(((p>>16)&255)>190&&((p>>8)&255)>190&&(p&255)>190)count++;}return count;}
 public static void main(String[] args)throws Exception{
  boolean before;try{StartupRuntime.class.getMethod("skipElement",Object.class);before=false;}catch(NoSuchMethodException expected){before=true;}
  Path output=Path.of(args[1]);Files.createDirectories(output);check(StartupRuntime.snapshot()!=null,"fixture premain accepted");
  check(GLFW.glfwInit(),"hidden GLFW initialized");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1920,1080,"OZL native Lua hidden composition",0,0);check(window!=0,"hidden GL context created");
  try{GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;GameRenderer nativeCanvas=new GameRenderer();SpriteRenderer sprites=SpriteRenderer.instance;
   for(int[] size:new int[][]{{1280,720},{1920,1080}}){int w=size[0],h=size[1];Display.width=w;Display.height=h;GL11.glViewport(0,0,w,h);GL11.glMatrixMode(GL11.GL_PROJECTION);GL11.glLoadIdentity();GL11.glOrtho(0,w,h,0,-1,1);GL11.glMatrixMode(GL11.GL_MODELVIEW);GL11.glLoadIdentity();GL11.glEnable(GL11.GL_BLEND);GL11.glBlendFunc(GL11.GL_SRC_ALPHA,GL11.GL_ONE_MINUS_SRC_ALPHA);
    NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),w,h);StartupRuntime.serverBegin("Native composition fixture");
    sprites.background=()->{GL11.glClearColor(.08f,.11f,.14f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);};
    UIElement.drawing=d->{f.draws.add(d);sprites.queue.add((Runnable)()->{double ox=d.element==f.connect?number(f.connect,"x"):0,oy=d.element==f.connect?number(f.connect,"y"):0;
      if(d.texture!=null&&d.texture==f.main.getTable().rawget("logoTexture")){nativeCanvas.text("PROJECT",d.x,d.y+30,32,0xffe4e4e4,false);nativeCanvas.text("ZOMBOID",d.x,d.y+70,48,0xffe4e4e4,false);}
      else rect(ox+d.x,oy+d.y,d.w,d.h,d.r,d.g,d.b,d.a);
    });};
    String[] names={"title","serverName1","serverName","userName1","userName","richText","connectLabel","backBtn"};
    for(String name:names){UIElement child=f.child(name);child.nativeRender=()->sprites.queue.add((Runnable)()->{double x=number(f.connect,"x")+number(child,"x"),y=number(f.connect,"y")+number(child,"y"),height=number(child,"height");String text=name.equals("connectLabel")?"Contacting server…":name.equals("backBtn")?"Abort":name.equals("richText")?"Native connection reminder":name.equals("title")?"Connect to server":"Fixture identity";
     if(name.equals("backBtn")){rect(x,y,100,height,.1,.1,.1,.9);rect(x,y,100,1,1,1,1,1);rect(x,y+height-1,100,1,1,1,1,1);nativeCanvas.text(text,x+29,y+height-6,14,0xffffffff,false);}
     else nativeCanvas.text(text,x-nativeCanvas.measure(text,name.equals("title")?20:16)/2,y+Math.min(height,20),name.equals("title")?20:16,0xffffffff,false);
    });}
    MainScreenState.instance.render();sprites.drain();BufferedImage first=capture(w,h,output.resolve("native-lua-"+(before?"before-":"after-")+w+"x"+h+".png"));
    int statusY=(int)(number(f.connect,"y")+number(f.child("connectLabel"),"y")),buttonY=(int)(number(f.connect,"y")+number(f.child("backBtn"),"y"));
    check(white(first,w/2-120,statusY,240,28)>150,"native status pixels survive real queued composition");check(white(first,w/2-50,buttonY,100,25)>100,"native Abort text and border survive real queued composition");
    boolean coherent=f.draws.isEmpty();sprites.recycle();
    if(before){check(!coherent,"pre-fix image reproduces native Lua logo and modal over owned scene");continue;}
    check(coherent,"actual native Lua logo/fill/border/arrows removed");check(f.child("title").renders==0&&f.child("connectLabel").renders==1&&f.child("backBtn").renders==1,"exact decorative child filter retains status and Abort");
    // A deferred owned failure cannot remove essential native commands, and next frame fails open.
    if(w==1920){StartupRuntime.begin(null);StartupRuntime.serverBegin("Renderer failure fixture");MainScreenState.instance.render();sprites.drain();BufferedImage failed=capture(w,h,output.resolve("native-lua-renderer-failure.png"));check(!StartupRuntime.snapshot().active&&white(failed,w/2-50,buttonY,100,25)>100,"owned draw failure preserves same-frame native Abort");sprites.recycle();f.draws.clear();MainScreenState.instance.render();sprites.drain();capture(w,h,output.resolve("native-lua-failure-next-native.png"));check(!f.draws.isEmpty(),"renderer failure restores full native decoration on following frame");sprites.recycle();}
   }
   check(GL11.glGetError()==GL11.GL_NO_ERROR,"native composite left no GL errors");System.out.println("PASS "+checks+" hidden actual-Lua GL composition/status/Abort/failure contracts"+(before?" (pre-fix reproduction)":""));
  }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
}
