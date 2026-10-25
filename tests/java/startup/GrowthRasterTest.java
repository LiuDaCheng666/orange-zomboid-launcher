package ozl.startup.tests;
import ozl.startup.*;
import java.awt.image.*;
import java.nio.file.*;
import java.util.*;
import javax.imageio.ImageIO;

public final class GrowthRasterTest {
 static int checks;static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 static BufferedImage draw(BufferedImage logo,LoadingModel model,int w,int h,double time,boolean reduced,boolean nativeLane){
  BufferedImage image=new BufferedImage(w,h,BufferedImage.TYPE_INT_ARGB);SceneProbe canvas=new SceneProbe(image,logo);
  double status=(h*.1+228)/h,cancel=(h*.9-50)/h;
  LoadingScene.draw(canvas,w,h,model.snapshot(),time,false,reduced,4097L<<20,12288L<<20,nativeLane?status:0,nativeLane?cancel:0);
  if(nativeLane){canvas.text("Trying Steam connection…",w*.5-canvas.measure("Trying Steam connection…",16)/2,h*.1+224,16,0xffffffff,false);canvas.text("Abort",w*.5-17,h*.9-30,14,0xffffffff,false);}
  canvas.g.dispose();return image;
 }
 static int[] pixels(BufferedImage image){return ((DataBufferInt)image.getRaster().getDataBuffer()).getData();}
 public static void main(String[] args)throws Exception{
  Path out=Path.of(args[0]);Files.createDirectories(out);BufferedImage logo=ImageIO.read(Path.of(args[1]).toFile());
  BufferedImage white=new BufferedImage(4,4,BufferedImage.TYPE_INT_ARGB);for(int y=0;y<4;y++)for(int x=0;x<4;x++)white.setRGB(x,y,0xffffffff);
  BufferedImage test=new BufferedImage(4,4,BufferedImage.TYPE_INT_ARGB);SceneProbe probe=new SceneProbe(test,white);probe.logo(2,2,4,.5,0);probe.g.dispose();check((test.getRGB(2,2)>>>24)==128,"raster adapter respects fractional logo alpha");
  LogoGrowth.Parts parts=LogoGrowth.split(logo);for(int y=0;y<logo.getHeight();y++)for(int x=0;x<logo.getWidth();x++){int a=parts.body().getRGB(x,y),b=parts.leaf().getRGB(x,y);if((a|b)!=logo.getRGB(x,y)||(a!=0&&b!=0))throw new AssertionError("Partition changed source pixel");}checks++;
  LoadingModel boot=new LoadingModel(),save=new LoadingModel(),server=new LoadingModel();boot.phase("Loading Mods");boot.mod("Project Viewpoint");save.worldBegin(false,"Rosewood — Day 48","Sandbox","Rosewood");save.worldStatus("正在读取地图区块");server.serverBegin("橙子社区服务器","203.0.113.7",16261);server.serverStats("Riverside",84);server.serverStage("WorkshopUpdate",132,84);server.queueInformation(server.snapshot().generation,17,32,1993,7,9,14,35);server.serverClock(server.snapshot().generation,1993,6,8,14,35,48);server.nativeStatus(server.snapshot().generation,"Trying Steam connection…");
  LoadingModel[] models={boot,server,save};String[] names={"boot","server","save"};
  for(int i=0;i<models.length;i++){
   BufferedImage still=draw(logo,models[i],640,480,0,true,false);
   for(double t:new double[]{4,7,11,16,60})check(Arrays.equals(pixels(still),pixels(draw(logo,models[i],640,480,t,true,false))),"reduced motion pixel-identical for "+names[i]+" at "+t);
   for(double t:new double[]{0,.45,1.4,4,7,8.399,8.401,11,16}){ImageIO.write(draw(logo,models[i],1280,720,t,false,false),"png",out.resolve(names[i]+"-t"+t+".png").toFile());checks++;}
   ImageIO.write(still,"png",out.resolve(names[i]+"-reduced.png").toFile());
  }
  for(int[] viewport:new int[][]{{640,480},{1280,720},{1920,1080},{3840,2160}}){BufferedImage frame=draw(logo,server,viewport[0],viewport[1],4,false,true);ImageIO.write(frame,"png",out.resolve("native-lanes-"+viewport[0]+".png").toFile());checks++;}
  for(int i=0;i<64;i++)ImageIO.write(draw(logo,boot,640,480,2+i*8.4/64,false,false),"png",out.resolve(String.format("motion-%03d.png",i)).toFile());
  System.out.println("PASS "+checks+" growth raster/alpha/partition/reduced/viewport contracts");
 }
}
