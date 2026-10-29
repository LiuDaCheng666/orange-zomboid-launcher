package ozl.startup.tests;

import ozl.startup.*;
import java.awt.*;
import java.awt.image.BufferedImage;
import java.awt.geom.Rectangle2D;
import java.nio.file.*;
import java.util.*;
import java.util.List;
import javax.imageio.ImageIO;

/** Production scene commands and pixels, not the browser approval mock. */
public final class ApprovedLoadingLayoutTest implements LoadingScene.Canvas {
    record Rect(double x,double y,double w,double h){}
    record Word(String value,double x,double y,double size,boolean right){}
    final SceneProbe drawing;final List<Rect> rects=new ArrayList<>();final List<Word> words=new ArrayList<>();
    ApprovedLoadingLayoutTest(BufferedImage image,BufferedImage logo){drawing=new SceneProbe(image,logo);}
    public void gradient(double x,double y,double w,double h,int a,int b){rects.add(new Rect(x,y,w,h));drawing.gradient(x,y,w,h,a,b);}
    public void glow(double x,double y,double r,int a,int b){drawing.glow(x,y,r,a,b);}
    public void arc(double x,double y,double r,double t,double a,double l,int c){drawing.arc(x,y,r,t,a,l,c);}
    public void line(double x,double y,double xx,double yy,double t,int c){drawing.line(x,y,xx,yy,t,c);}
    public void logo(double x,double y,double s,double a,double angle){drawing.logo(x,y,s,a,angle);}
    public void growingLogo(double x,double y,double s,double a,double angle,double leaf,double leafAngle,double sx,double sy){drawing.growingLogo(x,y,s,a,angle,leaf,leafAngle,sx,sy);}
    public double measure(String t,double s){return drawing.measure(t,s);}
    public void text(String t,double x,double y,double s,int color,boolean right){words.add(new Word(t,x,y,s,right));drawing.text(t,x,y,s,color,right);}
    static void check(boolean b,String why){if(!b)throw new AssertionError(why);}
    Rectangle2D bounds(Word word){FontMetrics fm=drawing.g.getFontMetrics(new Font("Microsoft YaHei",Font.PLAIN,16).deriveFont((float)word.size));double width=fm.stringWidth(word.value);return new Rectangle2D.Double(word.right?word.x-width:word.x,word.y-fm.getAscent(),width,fm.getHeight());}
    static void compact(BufferedImage logo){
        LoadingModel m=new LoadingModel();m.serverBegin("Workshop fixture","203.0.113.7",16261);m.serverStage("WorkshopUpdate",3,8);m.nativeStatus(m.snapshot().generation,"正在连接服务器");m.download(true,"101","当前工坊项目",1,3145728,10485760,2097152);
        ApprovedLoadingLayoutTest c=new ApprovedLoadingLayoutTest(new BufferedImage(640,480,BufferedImage.TYPE_INT_RGB),logo);LoadingScene.draw(c,640,480,m.snapshot(),4,false,false,4097L<<20,12288L<<20,0,0,238.8/480);
        for(int i=0;i<c.words.size();i++)for(int j=i+1;j<c.words.size();j++)check(!c.bounds(c.words.get(i)).intersects(c.bounds(c.words.get(j))),"640 workshop header text collision: "+c.words.get(i).value+" / "+c.words.get(j).value);c.drawing.g.dispose();
        m=new LoadingModel();m.serverBegin("真实服务器","a".repeat(63)+"."+"b".repeat(63)+"."+"c".repeat(63)+"."+"d".repeat(61),16261);m.serverStage("WorkshopUpdate",132,8);m.serverStats("Riverside",8);m.serverClock(m.snapshot().generation,1993,6,8,14,35,48);
        c=new ApprovedLoadingLayoutTest(new BufferedImage(640,480,BufferedImage.TYPE_INT_RGB),logo);LoadingScene.draw(c,640,480,m.snapshot(),4,false,false,4097L<<20,12288L<<20,276d/480,382d/480);
        Rectangle2D nativeStatus=new Rectangle2D.Double(170,256,300,20);for(Word word:c.words)check(!c.bounds(word).intersects(nativeStatus),"long endpoint facts cover native connection status: "+word.value+" "+c.bounds(word));c.drawing.g.dispose();
    }
    public static void main(String[] args)throws Exception{
        Path out=Path.of(args[0]);Files.createDirectories(out);BufferedImage logo=ImageIO.read(Path.of(args[1]).toFile());
        for(String mode:new String[]{"boot","server","save","download"})for(boolean light:new boolean[]{false,true}){
            LoadingModel m=new LoadingModel();if(!mode.equals("boot"))m.serverBegin("橙子官方服务器","203.0.113.7",16261);
            if(mode.equals("save"))m.worldBegin(false,"中文测试存档","Sandbox","Rosewood");
            if(mode.equals("boot")){m.phase("Loading Mods");m.mod("VisibleActivity");}else if(mode.equals("save"))m.worldStatus("VisibleActivity");else m.nativeStatus(m.snapshot().generation,"VisibleActivity");
            if(mode.equals("download")){m.workshop(3,-1,-1);m.download(true,"101","当前工坊项目",1,30,100,2097152);}
            m.issue(new String[]{"模组脚本执行中断","原始错误依据","ErrorExample","Example.lua:84","检查游戏日志"});
            BufferedImage image=new BufferedImage(1280,720,BufferedImage.TYPE_INT_RGB);ApprovedLoadingLayoutTest c=new ApprovedLoadingLayoutTest(image,logo);
            LoadingScene.draw(c,1280,720,m.snapshot(),6.5,light,true,2416L<<20,8192L<<20);
            Rect memory=c.rects.stream().filter(r->r.y>0&&r.y<60&&r.h>=16&&r.h<=22&&r.w>640&&r.w<900).findFirst().orElseThrow(()->new AssertionError("approved heap slot is centered with margins, not edge-to-edge"));
            check(Math.abs(memory.x+memory.w/2-640)<1,"heap slot is centered");
            Rect fill=c.rects.stream().filter(r->Math.abs(r.x-memory.x-2)<.1&&Math.abs(r.y-memory.y-2)<.1&&Math.abs(r.h-memory.h+4)<.1).findFirst().orElseThrow();
            check(Math.abs(fill.w/(memory.w-4)-2416d/8192)<.0001,"real heap ratio inside 2px inset");
            Word title=c.words.stream().filter(t->t.value.equals(mode.equals("download")?"正在下载工坊模组":m.snapshot().title)).findFirst().orElseThrow();
            check(Math.abs(title.x+c.measure(title.value,title.size)/2-640)<1,"current stage message is centered");
            check(c.words.stream().anyMatch(t->t.value.contains("VisibleActivity")&&t.x<500&&!t.right),"loading activity remains on the left in "+mode);
            check(c.words.stream().anyMatch(t->t.value.contains("ErrorExample")&&t.x>900&&t.right),"error information remains on the right and right-aligned");
            if(mode.equals("download")){check(c.rects.stream().anyMatch(r->r.y>150&&r.h>=20&&r.h<=26&&r.w>800&&Math.abs(r.x+r.w/2-640)<1),"current project transfer slot is substantial and centered");check(c.words.stream().anyMatch(t->t.value.equals("30%")),"actual current project percentage is displayed");}
            c.drawing.g.dispose();ImageIO.write(image,"png",out.resolve(mode+"-1280-"+(light?"light":"dark")+".png").toFile());
        }
        check(LogoGrowth.at(6.2,false).angle()-LogoGrowth.at(.5,false).angle()>6,"orange completes a full turn early in the repertoire");
        for(double t:new double[]{0,1,7,19,60})check(LogoGrowth.at(t,true).equals(LogoGrowth.at(0,true)),"reduced motion is static");
        compact(logo);
        System.out.println("PASS approved production loading layout, real counters, left/right logs, whole rotation and static reduced motion");
    }
}
