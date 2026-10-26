package ozl.startup;

import java.awt.*;
import java.awt.image.BufferedImage;
import java.nio.*;
import java.io.*;
import java.util.*;
import java.util.function.LongSupplier;
import javax.imageio.ImageIO;
import org.lwjgl.opengl.*;

/** Called exclusively on the game's current render context; owns no window. */
public final class GameRenderer implements LoadingScene.Canvas {
    private record Texture(int id,int width,int height) {}
    private final LinkedHashMap<String,Texture> words=new LinkedHashMap<>(96,.75f,true);
    private Texture logo,body,leaf;
    private final Font font=new Font("Microsoft YaHei",Font.PLAIN,16);
    private final BufferedImage measureImage=new BufferedImage(1,1,BufferedImage.TYPE_INT_ARGB);
    private final Graphics2D measure=measureImage.createGraphics();
    private final AnimationClock animation=new AnimationClock();
    private final LongSupplier clock;
    public GameRenderer(){this(System::nanoTime);}
    public GameRenderer(LongSupplier clock){this.clock=Objects.requireNonNull(clock);measure.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING,RenderingHints.VALUE_TEXT_ANTIALIAS_ON);}
    public void draw(LoadingModel.Snapshot state,Session session,long used,long maximum) throws Exception {draw(state,session,used,maximum,state.fullStatusBottom,state.fullCancelTop);}
    public void draw(LoadingModel.Snapshot state,Session session,long used,long maximum,double statusBottom,double cancelTop) throws Exception {
        if(!GameDisplay.created()||GL.getCapabilities()==null)return;
        int width=GameDisplay.width(),height=GameDisplay.height();if(width<320||height<240)return;
        int program=GL11.glGetInteger(GL20.GL_CURRENT_PROGRAM),active=GL11.glGetInteger(GL13.GL_ACTIVE_TEXTURE),framebuffer=GL11.glGetInteger(GL30.GL_DRAW_FRAMEBUFFER_BINDING),matrixMode=GL11.glGetInteger(GL11.GL_MATRIX_MODE),unpack=GL11.glGetInteger(GL21.GL_PIXEL_UNPACK_BUFFER_BINDING);
        GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);GL11.glPushClientAttrib(GL11.GL_CLIENT_PIXEL_STORE_BIT);
        boolean projection=false,modelview=false,textureMatrix=false;
        try {
            GL30.glBindFramebuffer(GL30.GL_DRAW_FRAMEBUFFER,0);GL20.glUseProgram(0);GL15.glBindBuffer(GL21.GL_PIXEL_UNPACK_BUFFER,0);
            int textureUnits=Math.min(32,GL11.glGetInteger(GL13.GL_MAX_TEXTURE_UNITS));
            for(int unit=0;unit<textureUnits;unit++){GL13.glActiveTexture(GL13.GL_TEXTURE0+unit);GL11.glDisable(GL11.GL_TEXTURE_1D);GL11.glDisable(GL11.GL_TEXTURE_2D);GL11.glDisable(GL12.GL_TEXTURE_3D);GL11.glDisable(GL13.GL_TEXTURE_CUBE_MAP);}
            GL13.glActiveTexture(GL13.GL_TEXTURE0);GL11.glTexEnvi(GL11.GL_TEXTURE_ENV,GL11.GL_TEXTURE_ENV_MODE,GL11.GL_MODULATE);
            GL11.glMatrixMode(GL11.GL_TEXTURE);GL11.glPushMatrix();textureMatrix=true;GL11.glLoadIdentity();
            GL11.glViewport(0,0,width,height);GL11.glDisable(GL11.GL_DEPTH_TEST);GL11.glDisable(GL11.GL_STENCIL_TEST);GL11.glDisable(GL11.GL_SCISSOR_TEST);GL11.glDisable(GL11.GL_CULL_FACE);GL11.glDisable(GL11.GL_LIGHTING);GL11.glDisable(GL11.GL_ALPHA_TEST);GL11.glDisable(GL11.GL_TEXTURE_2D);
            GL11.glEnable(GL11.GL_BLEND);GL11.glBlendFunc(GL11.GL_SRC_ALPHA,GL11.GL_ONE_MINUS_SRC_ALPHA);GL14.glBlendEquation(GL14.GL_FUNC_ADD);GL11.glColorMask(true,true,true,true);GL11.glPolygonMode(GL11.GL_FRONT_AND_BACK,GL11.GL_FILL);
            GL11.glMatrixMode(GL11.GL_PROJECTION);GL11.glPushMatrix();projection=true;GL11.glLoadIdentity();GL11.glOrtho(0,width,height,0,-1,1);
            GL11.glMatrixMode(GL11.GL_MODELVIEW);GL11.glPushMatrix();modelview=true;GL11.glLoadIdentity();
            if(logo==null){try(InputStream input=GameRenderer.class.getResourceAsStream("/ozl/startup/orange.png")){if(input==null)throw new IOException("Missing OZL mark");BufferedImage source=ImageIO.read(input);logo=upload(source);LogoGrowth.Parts parts=LogoGrowth.split(source);body=upload(parts.body());leaf=upload(parts.leaf());}}
            // UI lease invalidation is independent of this operation's visible animation.
            double visible=animation.seconds(state.generation,state.context.toString(),clock.getAsLong());
            LoadingScene.draw(this,width,height,state,visible,"light".equals(session.theme),session.reducedMotion,used,maximum,statusBottom,cancelTop);
        } finally {
            if(modelview){GL11.glMatrixMode(GL11.GL_MODELVIEW);GL11.glPopMatrix();}
            if(projection){GL11.glMatrixMode(GL11.GL_PROJECTION);GL11.glPopMatrix();}
            if(textureMatrix){GL13.glActiveTexture(GL13.GL_TEXTURE0);GL11.glMatrixMode(GL11.GL_TEXTURE);GL11.glPopMatrix();}
            GL11.glMatrixMode(matrixMode);GL11.glPopClientAttrib();GL11.glPopAttrib();
            GL30.glBindFramebuffer(GL30.GL_DRAW_FRAMEBUFFER,framebuffer);GL20.glUseProgram(program);GL13.glActiveTexture(active);GL15.glBindBuffer(GL21.GL_PIXEL_UNPACK_BUFFER,unpack);
        }
    }
    private static void color(int color){GL11.glColor4f(((color>>16)&255)/255f,((color>>8)&255)/255f,(color&255)/255f,((color>>>24)&255)/255f);}
    public void gradient(double x,double y,double w,double h,int top,int bottom){
        GL11.glDisable(GL11.GL_TEXTURE_2D);GL11.glBegin(GL11.GL_QUADS);color(top);GL11.glVertex2d(x,y);GL11.glVertex2d(x+w,y);color(bottom);GL11.glVertex2d(x+w,y+h);GL11.glVertex2d(x,y+h);GL11.glEnd();
    }
    public void glow(double x,double y,double radius,int center,int edge){
        GL11.glDisable(GL11.GL_TEXTURE_2D);GL11.glBegin(GL11.GL_TRIANGLE_FAN);color(center);GL11.glVertex2d(x,y);color(edge);for(int i=0;i<=64;i++){double a=i*Math.PI*2/64;GL11.glVertex2d(x+Math.cos(a)*radius,y+Math.sin(a)*radius);}GL11.glEnd();
    }
    public void arc(double x,double y,double radius,double thickness,double start,double length,int color){
        GL11.glDisable(GL11.GL_TEXTURE_2D);color(color);int steps=Math.max(8,(int)(length*32));GL11.glBegin(GL11.GL_TRIANGLE_STRIP);for(int i=0;i<=steps;i++){double a=start+length*i/steps,dx=Math.cos(a),dy=Math.sin(a);GL11.glVertex2d(x+dx*(radius-thickness/2),y+dy*(radius-thickness/2));GL11.glVertex2d(x+dx*(radius+thickness/2),y+dy*(radius+thickness/2));}GL11.glEnd();
    }
    public void line(double x1,double y1,double x2,double y2,double thickness,int color){
        double length=Math.hypot(x2-x1,y2-y1);if(length==0)return;double dx=-(y2-y1)/length*thickness/2,dy=(x2-x1)/length*thickness/2;
        GL11.glDisable(GL11.GL_TEXTURE_2D);color(color);GL11.glBegin(GL11.GL_QUADS);GL11.glVertex2d(x1+dx,y1+dy);GL11.glVertex2d(x2+dx,y2+dy);GL11.glVertex2d(x2-dx,y2-dy);GL11.glVertex2d(x1-dx,y1-dy);GL11.glEnd();
    }
    public void logo(double x,double y,double size,double alpha,double angle){if(logo!=null){GL11.glPushMatrix();try{GL11.glTranslated(x,y,0);GL11.glRotated(Math.toDegrees(angle),0,0,1);quad(logo,-size/2,-size/2,size,size,alpha);}finally{GL11.glPopMatrix();}}}
    public void growingLogo(double x,double y,double size,double alpha,double angle,double leafScale,double leafAngle){
        growingLogo(x,y,size,alpha,angle,leafScale,leafAngle,1,1);
    }
    public void growingLogo(double x,double y,double size,double alpha,double angle,double leafScale,double leafAngle,double stretchX,double stretchY){
        if(body==null)return;
        for(boolean isLeaf:new boolean[]{false,true}){
            java.awt.geom.AffineTransform transform=LogoGrowth.transform(x,y,size,angle,leafScale,leafAngle,isLeaf,stretchX,stretchY);
            double[] points={0,0,1,0,1,1,0,1};transform.transform(points,0,points,0,4);
            GL11.glEnable(GL11.GL_TEXTURE_2D);GL11.glBindTexture(GL11.GL_TEXTURE_2D,(isLeaf?leaf:body).id);GL11.glColor4d(1,1,1,alpha);GL11.glBegin(GL11.GL_QUADS);
            for(int i=0;i<4;i++){GL11.glTexCoord2d(i==1||i==2?1:0,i>=2?1:0);GL11.glVertex2d(points[i*2],points[i*2+1]);}GL11.glEnd();GL11.glDisable(GL11.GL_TEXTURE_2D);
        }
    }
    private static void quad(Texture texture,double x,double y,double w,double h,double alpha){
        quad(texture,x,y,w,h,alpha,0xffffffff);
    }
    private static void quad(Texture texture,double x,double y,double w,double h,double alpha,int tint){
        GL11.glEnable(GL11.GL_TEXTURE_2D);GL11.glBindTexture(GL11.GL_TEXTURE_2D,texture.id);GL11.glColor4d(((tint>>16)&255)/255d,((tint>>8)&255)/255d,(tint&255)/255d,alpha*((tint>>>24)/255d));GL11.glBegin(GL11.GL_QUADS);GL11.glTexCoord2d(0,0);GL11.glVertex2d(x,y);GL11.glTexCoord2d(1,0);GL11.glVertex2d(x+w,y);GL11.glTexCoord2d(1,1);GL11.glVertex2d(x+w,y+h);GL11.glTexCoord2d(0,1);GL11.glVertex2d(x,y+h);GL11.glEnd();GL11.glDisable(GL11.GL_TEXTURE_2D);
    }
    private Font sized(double size){return font.deriveFont((float)LoadingScene.font(size));}
    public double measure(String text,double size){return measure.getFontMetrics(sized(size)).stringWidth(text);}
    public void text(String value,double x,double y,double size,int color,boolean right){
        if(value.isEmpty())return;
        String key=Math.round(size*10)+":"+value;Texture texture=words.get(key);
        Font selected=sized(size);FontMetrics metrics=measure.getFontMetrics(selected);
        if(texture==null){
            int w=Math.min(2048,Math.max(1,metrics.stringWidth(value)+4)),h=Math.max(1,metrics.getHeight()+4);
            BufferedImage image=new BufferedImage(w,h,BufferedImage.TYPE_INT_ARGB);Graphics2D g=image.createGraphics();
            try{g.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING,RenderingHints.VALUE_TEXT_ANTIALIAS_ON);g.setFont(selected);g.setColor(Color.WHITE);g.drawString(value,2,metrics.getAscent()+2);}finally{g.dispose();}
            texture=upload(image);words.put(key,texture);
            while(words.size()>96){var iterator=words.entrySet().iterator();Texture old=iterator.next().getValue();iterator.remove();GL11.glDeleteTextures(old.id);}
        }
        quad(texture,(right?x-texture.width:x)-2,y-metrics.getAscent()-2,texture.width,texture.height,1,color);
    }
    private static Texture upload(BufferedImage image){
        int w=image.getWidth(),h=image.getHeight();ByteBuffer bytes=ByteBuffer.allocateDirect(w*h*4);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=image.getRGB(x,y);bytes.put((byte)(p>>16));bytes.put((byte)(p>>8));bytes.put((byte)p);bytes.put((byte)(p>>>24));}bytes.flip();
        // draw() owns a saved client pixel-store scope; uploaded images are tightly packed.
        GL11.glPixelStorei(GL11.GL_UNPACK_ROW_LENGTH,0);GL11.glPixelStorei(GL11.GL_UNPACK_SKIP_ROWS,0);GL11.glPixelStorei(GL11.GL_UNPACK_SKIP_PIXELS,0);
        int id=GL11.glGenTextures();GL11.glBindTexture(GL11.GL_TEXTURE_2D,id);GL11.glPixelStorei(GL11.GL_UNPACK_ALIGNMENT,1);GL11.glTexParameteri(GL11.GL_TEXTURE_2D,GL11.GL_TEXTURE_MIN_FILTER,GL11.GL_LINEAR);GL11.glTexParameteri(GL11.GL_TEXTURE_2D,GL11.GL_TEXTURE_MAG_FILTER,GL11.GL_LINEAR);GL11.glTexParameteri(GL11.GL_TEXTURE_2D,GL11.GL_TEXTURE_WRAP_S,GL12.GL_CLAMP_TO_EDGE);GL11.glTexParameteri(GL11.GL_TEXTURE_2D,GL11.GL_TEXTURE_WRAP_T,GL12.GL_CLAMP_TO_EDGE);GL11.glTexImage2D(GL11.GL_TEXTURE_2D,0,GL11.GL_RGBA,w,h,0,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,bytes);return new Texture(id,w,h);
    }
}
