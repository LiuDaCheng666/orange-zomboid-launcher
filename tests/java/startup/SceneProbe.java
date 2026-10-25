package ozl.startup.tests;
import ozl.startup.*;
import java.awt.*;
import java.awt.geom.*;
import java.awt.image.BufferedImage;
import java.nio.file.*;
import javax.imageio.ImageIO;

public final class SceneProbe implements LoadingScene.Canvas {
    final Graphics2D g;final BufferedImage logo;final int width,height;
    int labels;final Font font=new Font("Microsoft YaHei",Font.PLAIN,16);
    SceneProbe(BufferedImage image,BufferedImage logo){this.g=image.createGraphics();this.logo=logo;width=image.getWidth();height=image.getHeight();g.setRenderingHint(RenderingHints.KEY_ANTIALIASING,RenderingHints.VALUE_ANTIALIAS_ON);g.setRenderingHint(RenderingHints.KEY_TEXT_ANTIALIASING,RenderingHints.VALUE_TEXT_ANTIALIAS_ON);}
    public void gradient(double x,double y,double w,double h,int top,int bottom){g.setPaint(new GradientPaint((float)x,(float)y,new Color(top,true),(float)x,(float)(y+h),new Color(bottom,true)));g.fill(new Rectangle2D.Double(x,y,w,h));}
    public void glow(double x,double y,double radius,int center,int edge){g.setPaint(new RadialGradientPaint(new Point2D.Double(x,y),(float)radius,new float[]{0,1},new Color[]{new Color(center,true),new Color(edge,true)}));g.fill(new Ellipse2D.Double(x-radius,y-radius,radius*2,radius*2));}
    public void arc(double x,double y,double r,double thickness,double start,double length,int color){g.setColor(new Color(color,true));g.setStroke(new BasicStroke((float)thickness));g.draw(new Arc2D.Double(x-r,y-r,r*2,r*2,-Math.toDegrees(start),-Math.toDegrees(length),Arc2D.OPEN));}
    public void logo(double x,double y,double size,double alpha){g.drawImage(logo,(int)(x-size/2),(int)(y-size/2),(int)size,(int)size,null);}
    public double measure(String text,double size){return g.getFontMetrics(font.deriveFont((float)size)).stringWidth(text);}
    public void text(String value,double x,double y,double size,int color,boolean right){
        g.setFont(font.deriveFont((float)size));g.setColor(new Color(color,true));FontMetrics metrics=g.getFontMetrics();double xx=right?x-metrics.stringWidth(value):x;
        if(xx<0||xx+metrics.stringWidth(value)>width+1||y-metrics.getAscent()<0||y+metrics.getDescent()>height)throw new AssertionError("Text escapes game viewport: "+value);
        g.drawString(value,(float)xx,(float)y);labels++;
    }
    public static void main(String[] args)throws Exception {
        Path output=Path.of(args[0]);Files.createDirectories(output);BufferedImage logo=ImageIO.read(Path.of(args[1]).toFile());
        LoadingModel model=new LoadingModel();model.phase("Loading Mods");model.mod("Project Viewpoint");model.script("/mods/AnotherAuthor/42/media/lua/client/RegisterWorld.lua");
        int checks=0;
        for(int[] size:new int[][]{{1024,768},{1280,720},{1920,1080},{3840,2160}}){
            BufferedImage image=new BufferedImage(size[0],size[1],BufferedImage.TYPE_INT_RGB);SceneProbe c=new SceneProbe(image,logo);
            LoadingScene.draw(c,size[0],size[1],model.snapshot(),1.4,false,false,3L<<30,12L<<30);c.g.dispose();
            if(c.labels<5)throw new AssertionError("Missing layout text");checks++;
            if(size[0]==1920)ImageIO.write(image,"png",output.resolve("loading-dark.png").toFile());
        }
        model.issue(new String[]{"模组脚本执行中断","脚本调用了当前不存在的对象","NewAuthorMod","attempted index: nil / RegisterWorld.lua:58","检查该模组的依赖与游戏版本"});
        BufferedImage firstMotion=null;
        for(int frame=0;frame<4;frame++){
            BufferedImage image=new BufferedImage(1440,900,BufferedImage.TYPE_INT_RGB);SceneProbe c=new SceneProbe(image,logo);LoadingScene.draw(c,1440,900,model.snapshot(),frame*.7,frame==3,false,4L<<30,12L<<30);c.g.dispose();ImageIO.write(image,"png",output.resolve("animation-"+frame+".png").toFile());checks++;
            if(frame==0)firstMotion=image;
            if(frame==1){int changed=0;for(int y=240;y<530;y++)for(int x=570;x<870;x++)if(firstMotion.getRGB(x,y)!=image.getRGB(x,y))changed++;if(changed<100)throw new AssertionError("Orange animation does not advance while logs are quiet");checks++;}
        }
        BufferedImage[] still=new BufferedImage[2];
        for(int i=0;i<2;i++){still[i]=new BufferedImage(1440,900,BufferedImage.TYPE_INT_RGB);SceneProbe c=new SceneProbe(still[i],logo);LoadingScene.draw(c,1440,900,model.snapshot(),i*5,false,true,4L<<30,12L<<30);c.g.dispose();}
        if(!java.util.Arrays.equals(((java.awt.image.DataBufferInt)still[0].getRaster().getDataBuffer()).getData(),((java.awt.image.DataBufferInt)still[1].getRaster().getDataBuffer()).getData()))throw new AssertionError("Reduced-motion setting still animates");checks++;
        System.out.println("PASS "+checks+" scene viewport/frame checks");
    }
}
