package ozl.startup.tests;

import ozl.startup.*;
import zombie.gameStates.*;
import zombie.core.SpriteRenderer;
import zombie.core.textures.TextureDraw.GenericDrawer;
import zombie.ui.UIManager;
import org.lwjglx.opengl.Display;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import java.nio.*;
import java.nio.file.*;
import java.lang.reflect.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;

/** Real generated queued adapter, actual hidden compatibility GL, game-shaped CPU queue. */
public final class QueuedGlProbe {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static void rect(double x,double y,double w,double h,float r,float g,float b){GL11.glColor3f(r,g,b);GL11.glBegin(GL11.GL_QUADS);GL11.glVertex2d(x,y);GL11.glVertex2d(x+w,y);GL11.glVertex2d(x+w,y+h);GL11.glVertex2d(x,y+h);GL11.glEnd();}
    static int pixel(int x,int y){ByteBuffer p=ByteBuffer.allocateDirect(4);GL11.glReadPixels(x,899-y,1,1,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,p);return ((p.get(0)&255)<<16)|((p.get(1)&255)<<8)|(p.get(2)&255);}
    static BufferedImage capture(Path path)throws Exception{ByteBuffer p=ByteBuffer.allocateDirect(1440*900*4);GL11.glReadPixels(0,0,1440,900,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,p);BufferedImage image=new BufferedImage(1440,900,BufferedImage.TYPE_INT_RGB);for(int y=0;y<900;y++)for(int x=0;x<1440;x++){int a=((899-y)*1440+x)*4;image.setRGB(x,y,((p.get(a)&255)<<16)|((p.get(a+1)&255)<<8)|(p.get(a+2)&255));}ImageIO.write(image,"png",path.toFile());return image;}
    static int orange(BufferedImage image){int n=0;for(int y=315;y<440;y++)for(int x=660;x<780;x++){int p=image.getRGB(x,y),r=(p>>16)&255,g=(p>>8)&255,b=p&255;if(r>150&&r>g*1.25&&g>b*1.1)n++;}return n;}
    static Object renderer()throws Exception{Field f=StartupRuntime.class.getDeclaredField("renderer");f.setAccessible(true);return f.get(null);}
    static GenericDrawer drawer(){return (GenericDrawer)SpriteRenderer.instance.queue.get(1);}
    public static void main(String[] args)throws Exception{
        check(StartupRuntime.snapshot()!=null,"real premain accepted fixture session");
        if(!GLFW.glfwInit())throw new AssertionError("No GLFW context");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);
        long window=GLFW.glfwCreateWindow(1440,900,"OZL queued hidden probe",0,0);check(window!=0,"hidden GL context created");
        try{
            GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;
            GL11.glViewport(0,0,1440,900);GL11.glMatrixMode(GL11.GL_PROJECTION);GL11.glLoadIdentity();GL11.glOrtho(0,1440,900,0,-1,1);GL11.glMatrixMode(GL11.GL_MODELVIEW);GL11.glLoadIdentity();
            int shader=GL20.glCreateProgram(),vertex=GL20.glCreateShader(GL20.GL_VERTEX_SHADER),fragment=GL20.glCreateShader(GL20.GL_FRAGMENT_SHADER);
            GL20.glShaderSource(vertex,"#version 120\nvoid main(){gl_Position=ftransform();gl_FrontColor=gl_Color;}");GL20.glCompileShader(vertex);
            GL20.glShaderSource(fragment,"#version 120\nvoid main(){gl_FragColor=gl_Color;}");GL20.glCompileShader(fragment);GL20.glAttachShader(shader,vertex);GL20.glAttachShader(shader,fragment);GL20.glLinkProgram(shader);check(GL20.glGetProgrami(shader,GL20.GL_LINK_STATUS)!=0,"native-control shader linked");GL20.glUseProgram(shader);
            int vao=GL30.glGenVertexArrays(),vbo=GL15.glGenBuffers();GL30.glBindVertexArray(vao);GL15.glBindBuffer(GL15.GL_ARRAY_BUFFER,vbo);GL11.glEnable(GL11.GL_SCISSOR_TEST);GL11.glScissor(0,0,1440,900);
            SpriteRenderer sprites=SpriteRenderer.instance;StartupRuntime.serverBegin("Hidden GL server");MainScreenState.instance.render();GenericDrawer stale=drawer();StartupRuntime.serverBegin("Current GL server");
            stale.render(new zombie.core.textures.TextureDraw());check(renderer()==null,"stale ticket never creates renderer or starts entrance");sprites.recycle();
            // Recycle can run without any GPU render. Inherited cleanup must stay CPU-only.
            MainScreenState.instance.render();sprites.recycle();check(renderer()==null&&GL11.glGetError()==GL11.GL_NO_ERROR,"skipped-frame recycle performs no GL work");
            String prior=StartupRuntime.enterFrame("zombie.gameStates.MainScreenState");UIManager.useUiFbo=true;MainScreenState.instance.renderBackground();StartupRuntime.exitFrame(prior);check(sprites.queue.size()==1,"enqueue-time UI-FBO rejects route");sprites.recycle();
            MainScreenState.instance.render();GenericDrawer rejected=drawer();
            int fbo=GL30.glGenFramebuffers(),texture=GL11.glGenTextures();GL11.glBindTexture(GL11.GL_TEXTURE_2D,texture);GL11.glTexImage2D(GL11.GL_TEXTURE_2D,0,GL11.GL_RGBA8,16,16,0,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,(ByteBuffer)null);GL30.glBindFramebuffer(GL30.GL_FRAMEBUFFER,fbo);GL30.glFramebufferTexture2D(GL30.GL_FRAMEBUFFER,GL30.GL_COLOR_ATTACHMENT0,GL11.GL_TEXTURE_2D,texture,0);check(GL30.glCheckFramebufferStatus(GL30.GL_FRAMEBUFFER)==GL30.GL_FRAMEBUFFER_COMPLETE,"sentinel FBO complete");
            GL11.glClearColor(.1f,.2f,.8f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);rejected.render(new zombie.core.textures.TextureDraw());ByteBuffer sentinel=ByteBuffer.allocateDirect(4);GL11.glReadPixels(0,0,1,1,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,sentinel);
            check(GL11.glGetInteger(GL30.GL_DRAW_FRAMEBUFFER_BINDING)==fbo&&(sentinel.get(2)&255)>190&&renderer()==null,"unexpected actual FBO stays bound and untouched; no entrance origin");
            GL30.glBindFramebuffer(GL30.GL_FRAMEBUFFER,0);GL30.glDeleteFramebuffers(fbo);GL11.glDeleteTextures(texture);sprites.recycle();
            Thread.sleep(1200); // Operation elapsed must not finish the unseen entrance.
            sprites.background=()->{GL11.glClearColor(.7f,.05f,.03f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);};
            sprites.controls=()->rect(1100,75,210,80,0,1,0);
            MainScreenState.instance.render();check(UIManager.useUiFbo,"restored true CPU flag is valid for the captured non-FBO ticket");sprites.drain();
            check(GL11.glGetInteger(GL20.GL_CURRENT_PROGRAM)==shader&&GL11.glGetInteger(GL30.GL_VERTEX_ARRAY_BINDING)==vao&&GL11.glGetInteger(GL15.GL_ARRAY_BUFFER_BINDING)==vbo&&GL11.glIsEnabled(GL11.GL_SCISSOR_TEST),"queued renderer restores shader, VAO, VBO and scissor for native controls");
            check(pixel(1200,100)==0x00ff00&&pixel(20,200)!=0xb20d08,"owned backdrop draws before original native-control pixels");
            Path output=Path.of(args[0]);Files.createDirectories(output);BufferedImage first=capture(output.resolve("queued-gl-first.png"));int firstOrange=orange(first);check(firstOrange>10&&firstOrange<400,"long pre-context operation still starts with a small orange");
            StartupRuntime.beforeSwap();StartupRuntime.idle();check(pixel(1200,100)==0x00ff00,"swap and idle never repaint over behind-UI controls");sprites.recycle();Thread.sleep(1250);
            MainScreenState.instance.render();sprites.drain();BufferedImage grown=capture(output.resolve("queued-gl-grown.png"));check(orange(grown)>firstOrange*3,"later accepted queue frame visibly grows the actual orange");sprites.recycle();
            MainScreenState.instance.render();GenericDrawer cancelled=drawer();StartupRuntime.handoff(false);GL11.glClearColor(.7f,.05f,.03f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);cancelled.render(new zombie.core.textures.TextureDraw());check((pixel(20,200)>>16)>150,"cancelled queued ticket cannot paint later original frame");sprites.recycle();
            // Exercise owned failure containment through the actual generated render() bridge.
            StartupRuntime.begin(null);StartupRuntime.serverBegin("Failure containment");MainScreenState.instance.render();sprites.drain();check(pixel(1200,100)==0x00ff00&&!StartupRuntime.snapshot().active,"owned callback failure is caught and subsequent native controls still execute");sprites.recycle();
            check(GL11.glGetError()==GL11.GL_NO_ERROR,"queued callback left no GL errors");
            GL20.glUseProgram(0);GL20.glDeleteProgram(shader);GL20.glDeleteShader(vertex);GL20.glDeleteShader(fragment);GL30.glBindVertexArray(0);GL30.glDeleteVertexArrays(vao);GL15.glBindBuffer(GL15.GL_ARRAY_BUFFER,0);GL15.glDeleteBuffers(vbo);
            System.out.println("PASS "+checks+" queued real GL ordering/state/FBO/epoch/first-visible contracts");
        }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
    }
}
