package ozl.startup.tests;

import ozl.startup.*;
import java.awt.image.*;
import java.nio.file.*;
import java.util.*;
import javax.imageio.ImageIO;

/** Real scene raster frames with a fixed clock; no process/game initialization. */
public final class GrowthMotionProbe {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static BufferedImage frame(BufferedImage logo,LoadingModel model,double time,boolean reduced){
        BufferedImage image=new BufferedImage(960,600,BufferedImage.TYPE_INT_RGB);SceneProbe canvas=new SceneProbe(image,logo);
        LoadingScene.draw(canvas,960,600,model.snapshot(),time,false,reduced,4097L<<20,12288L<<20);canvas.g.dispose();return image;
    }
    static int orangeHeight(BufferedImage image){int lo=image.getHeight(),hi=-1;for(int y=140;y<360;y++)for(int x=370;x<590;x++){int p=image.getRGB(x,y),r=(p>>16)&255,g=(p>>8)&255,b=p&255;if(r>160&&g>50&&r>g*1.5&&g>b*1.5){lo=Math.min(lo,y);hi=Math.max(hi,y);}}return hi<lo?0:hi-lo+1;}
    static int[] pixels(BufferedImage image){return ((DataBufferInt)image.getRaster().getDataBuffer()).getData();}
    public static void main(String[] args)throws Exception{
        double min=2,max=0,leafMin=2,leafMax=0;
        for(int i=0;i<=1200;i++){LogoGrowth.Frame f=LogoGrowth.at(3+i*.05,false);min=Math.min(min,f.bodyScale());max=Math.max(max,f.bodyScale());leafMin=Math.min(leafMin,f.leafScale());leafMax=Math.max(leafMax,f.leafScale());}
        check(min>=.94&&max<=1.06,"long waits preserve a fully grown orange");
        check(leafMin>=.93&&leafMax<=1.08,"idle leaf stays unfolded");
        check(LogoGrowth.at(0,false).bodyScale()<.3&&LogoGrowth.at(2.8,false).bodyScale()>.95,"seed entrance occurs once");
        LoadingModel model=new LoadingModel();model.phase("Loading Mods");model.mod("Project Viewpoint");String stage=model.snapshot().stage;int count=model.snapshot().activity.length;
        BufferedImage logo=ImageIO.read(Path.of(args[1]).toFile());Path out=Path.of(args[0]);Files.createDirectories(out);int smallest=1000,largest=0;
        for(int i=0;i<180;i++){double t=i*.2;BufferedImage image=frame(logo,model,t,false);ImageIO.write(image,"png",out.resolve(String.format("motion-%03d.png",i)).toFile());int height=orangeHeight(image);if(height>0){smallest=Math.min(smallest,height);largest=Math.max(largest,height);}}
        check(largest/(double)smallest>3,"actual raster silhouette clearly grows, instead of only rotating");
        check(model.snapshot().stage.equals(stage)&&model.snapshot().activity.length==count,"animation cannot fabricate observed progress or activity");
        BufferedImage still=frame(logo,model,0,true);check(Arrays.equals(pixels(still),pixels(frame(logo,model,60,true))),"reduced-motion output stays pixel-identical");ImageIO.write(still,"png",out.resolve("reduced.png").toFile());
        LoadingModel server=new LoadingModel();server.serverBegin("橙子社区服务器","203.0.113.7",16261);
        for(int[] size:new int[][]{{640,480},{1280,720},{1440,820},{1920,1080},{3840,2160}})for(double time:new double[]{0,.5,1.4,2.2,3.0,4.4,6.4,7.4,8.399,8.401,16.8}){
            SceneContractTest.Frame commands=new SceneContractTest.Frame();double status=size[1]*.1+228,cancel=size[1]*.9-50;
            LoadingScene.draw(commands.canvas(),size[0],size[1],server.snapshot(),time,false,false,4097L<<20,12288L<<20,status/size[1],cancel/size[1]);
            check(commands.y-commands.size*.58>status&&commands.y+commands.size*.58<cancel,"growth, lift and elastic leaf remain within native status/Abort lanes at "+size[0]+" t"+time);
        }
        System.out.println("PASS "+checks+" once entrance/idle repertoire/raster/reduced checks; orange height "+smallest+".."+largest);
    }
}
