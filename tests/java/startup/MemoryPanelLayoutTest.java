package ozl.startup.tests;

import ozl.startup.*;
import java.awt.*;
import java.awt.geom.Rectangle2D;
import java.awt.image.BufferedImage;
import java.nio.file.*;
import java.util.*;
import java.util.List;
import javax.imageio.ImageIO;

/** The production scene is rendered offscreen; this does not start a game or GL window. */
public final class MemoryPanelLayoutTest implements LoadingScene.Canvas {
    record Rect(double x,double y,double w,double h) { Rectangle2D bounds(){return new Rectangle2D.Double(x,y,w,h);} }
    record Word(String value,double size,Rectangle2D bounds) {}
    final SceneProbe drawing;
    final List<Rect> rectangles=new ArrayList<>();
    final List<Word> words=new ArrayList<>();
    static int checks;
    MemoryPanelLayoutTest(BufferedImage image,BufferedImage logo){drawing=new SceneProbe(image,logo);}
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    public void gradient(double x,double y,double w,double h,int a,int b){rectangles.add(new Rect(x,y,w,h));drawing.gradient(x,y,w,h,a,b);}
    public void glow(double x,double y,double r,int a,int b){drawing.glow(x,y,r,a,b);}
    public void arc(double x,double y,double r,double t,double a,double l,int c){drawing.arc(x,y,r,t,a,l,c);}
    public void line(double x,double y,double xx,double yy,double t,int c){drawing.line(x,y,xx,yy,t,c);}
    public void logo(double x,double y,double s,double alpha,double angle){drawing.logo(x,y,s,alpha,angle);}
    public void growingLogo(double x,double y,double s,double alpha,double angle,double leaf,double leafAngle,double sx,double sy){drawing.growingLogo(x,y,s,alpha,angle,leaf,leafAngle,sx,sy);}
    public double measure(String text,double size){return drawing.measure(text,size);}
    public void text(String text,double x,double y,double size,int color,boolean right){FontMetrics metrics=drawing.g.getFontMetrics(new Font("Microsoft YaHei",Font.PLAIN,16).deriveFont((float)size));double width=metrics.stringWidth(text);words.add(new Word(text,size,new Rectangle2D.Double(right?x-width:x,y-metrics.getAscent(),width,metrics.getHeight())));drawing.text(text,x,y,size,color,right);}
    static LoadingModel model(String mode){LoadingModel model=new LoadingModel();if(mode.equals("server")){model.serverBegin("橙子合作生存服务器","203.0.113.7",16261);model.serverStats("Riverside",8);model.worldStatus("正在同步世界数据");}else if(mode.equals("save")){model.worldBegin(false,"中文测试存档","Sandbox","Rosewood");model.worldStatus("正在读取地图区块");}else{model.phase("Loading Mods");model.mod("Project Viewpoint");}return model;}
    public static void main(String[] args)throws Exception{
        Path output=Path.of(args[0]);Files.createDirectories(output);BufferedImage logo=ImageIO.read(Path.of(args[1]).toFile());
        for(String mode:new String[]{"boot","save","server","native-server"})for(int[] size:new int[][]{{800,600},{1280,720},{1920,1080},{2560,1440}})for(boolean light:new boolean[]{false,true}){
            int w=size[0],h=size[1];BufferedImage image=new BufferedImage(w,h,BufferedImage.TYPE_INT_RGB);MemoryPanelLayoutTest c=new MemoryPanelLayoutTest(image,logo);
            LoadingScene.draw(c,w,h,model(mode.equals("native-server")?"server":mode).snapshot(),12,light,true,1010L<<20,8192L<<20,mode.equals("native-server")?.24:0,mode.equals("native-server")?.90:0);
            Word label=c.words.stream().filter(a->a.value.contains("Java 内存")).findFirst().orElseThrow();
            Word value=c.words.stream().filter(a->a.value.contains("MiB")||a.value.contains("GiB")).findFirst().orElseThrow();
            check(label.size>=14,"memory label must remain readable at "+w+" "+mode);
            check(value.size>=20,"memory used/maximum must be a prominent readable number at "+w+" "+mode);
            check(value.value.contains("1,010")&&value.value.contains("8,192"),"memory values remain the real used and maximum, not a loading percentage");
            Rect track=c.rectangles.stream().filter(a->a.x==0&&a.y==0&&a.h>=18&&a.h<=55&&a.w==w).findFirst().orElseThrow(()->new AssertionError("memory track must span the top edge of the loading page, without a separate framed card"));
            check(c.rectangles.stream().noneMatch(a->a.y<100&&a.h>55&&a.h<h*.5),"memory must not introduce a separate background panel");
            check(track.x>=0&&track.y>=0&&track.x+track.w<=w&&track.y+track.h<=h,"memory track stays within the game viewport");
            check(!track.bounds().intersects(value.bounds)&&!label.bounds.intersects(value.bounds),"memory value, title and bar occupy separate lines");
            Set<String> stages=Set.of("初始化","连接","创意工坊","模组","资源","脚本","界面","同步","世界","就绪","准备");
            check(c.words.stream().filter(a->stages.contains(a.value)).noneMatch(a->a.bounds.intersects(track.bounds())||a.bounds.intersects(value.bounds)),"memory panel never overlaps stage labels");
            Rect fill=c.rectangles.stream().filter(a->Math.abs(a.x-track.x)<.1&&Math.abs(a.y-track.y)<.1&&Math.abs(a.h-track.h)<.1&&a.w<track.w).findFirst().orElseThrow();
            check(Math.abs(fill.w/track.w-1010d/8192)<.00001,"memory fill is proportional to actual heap usage");
            c.drawing.g.dispose();if(w==1280||w==1920)ImageIO.write(image,"png",output.resolve(mode+"-"+w+"-"+(light?"light":"dark")+".png").toFile());
        }
        BufferedImage noHeap=new BufferedImage(1280,720,BufferedImage.TYPE_INT_RGB);MemoryPanelLayoutTest c=new MemoryPanelLayoutTest(noHeap,logo);LoadingScene.draw(c,1280,720,model("boot").snapshot(),12,false,true,0,0);check(c.words.stream().noneMatch(a->a.value.contains("Java 内存")),"unknown heap maximum does not invent a memory percentage");c.drawing.g.dispose();
        System.out.println("PASS "+checks+" memory panel actual scene layout/raster checks; no game or GL window");
    }
}
