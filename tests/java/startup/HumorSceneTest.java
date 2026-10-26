package ozl.startup.tests;

import ozl.startup.*;
import java.awt.image.BufferedImage;
import java.nio.file.*;
import java.util.*;
import javax.imageio.ImageIO;

/** Exercises production scene commands and raster output with an explicit clock. */
public final class HumorSceneTest {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static final Set<String> PHRASES=Set.of("狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊","正在给僵尸系鞋带","正在说服直升机绕路");
    static String joke(SceneContractTest.Frame frame){return frame.texts.stream().filter(a->((int)a[4]>>>24)>0&&((int)a[4]>>>24)<170).map(a->(String)a[0]).filter(v->v.startsWith("正在")||v.startsWith("狗正在")).findFirst().orElse("");}
    static SceneContractTest.Frame frame(LoadingModel model,int width,int height,double time,boolean reduced,boolean nativeLane){
        SceneContractTest.Frame frame=new SceneContractTest.Frame();
        LoadingScene.draw(frame.canvas(),width,height,model.snapshot(),time,false,reduced,4097L<<20,12288L<<20,nativeLane?(height*.1+228)/height:0,nativeLane?(height*.9-50)/height:0);
        return frame;
    }
    static int[] pixels(BufferedImage image){return ((java.awt.image.DataBufferInt)image.getRaster().getDataBuffer()).getData();}
    public static void main(String[] args)throws Exception {
        LoadingModel boot=new LoadingModel();boot.phase("Loading Mods");boot.mod("ActualViewpointMod");
        String stage=boot.snapshot().stage;int activity=boot.snapshot().activity.length;
        Set<String> phrases=new HashSet<>();String previous="";double entered=-1,exited=-1;int appearances=0;
        for(int tick=0;tick<=2400;tick++){
            double time=tick/10d;String current=joke(frame(boot,1280,720,time,false,false));
            if(tick==0)check(current.isEmpty(),"short loads do not immediately show a joke");
            if(!current.isEmpty()&&previous.isEmpty()){
                if(exited>=0)check(time-exited>=1.8&&time-exited<=3.0,"jokes have a short quiet interval between readable phrases");
                else check(time>=6&&time<=8,"first joke waits for a real loading pause");
                entered=time;appearances++;phrases.add(current);
            }
            if(current.isEmpty()&&!previous.isEmpty()){
                check(time-entered>=4.5&&time-entered<=5.2,"each phrase is readable for 3-6 seconds");exited=time;
            }
            previous=current;
        }
        check(appearances>=30&&appearances<=33,"jokes are occasional during a long quiet wait");
        for(String phrase:new String[]{"狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊"})check(phrases.stream().anyMatch(v->v.contains(phrase)),"requested phrase reaches the actual scene: "+phrase);
        check(boot.snapshot().stage.equals(stage)&&boot.snapshot().activity.length==activity,"humor never fabricates phase or activity observations");
        LoadingModel save=new LoadingModel(),server=new LoadingModel();save.worldBegin(false,"Rosewood — Day 48","Sandbox","Rosewood");server.serverBegin("橙子社区服务器","203.0.113.7",16261);
        LoadingModel[] models={boot,server,save};String[] names={"boot","server","save"};
        BufferedImage logo=ImageIO.read(Path.of(args[1]).toFile());Path out=Path.of(args[0]);Files.createDirectories(out);
        for(int index=0;index<models.length;index++)for(int[] size:new int[][]{{640,480},{1280,720},{1440,820},{1920,1080},{3840,2160}})for(boolean nativeLane:new boolean[]{false,true}){
            if(nativeLane&&index!=1)continue;
            double visible=-1;for(int second=6;second<=12;second++)if(!joke(frame(models[index],size[0],size[1],second,false,nativeLane)).isEmpty()){visible=second;break;}
            check(visible>=0,"every passive context displays occasional humor");
            SceneContractTest.Frame commands=frame(models[index],size[0],size[1],visible,false,nativeLane);
            for(Object[] command:commands.texts)if(((String)command[0]).equals(joke(commands))){
                double x=(double)command[1],y=(double)command[2],font=(double)command[3],width=commands.canvas().measure((String)command[0],font);
                check(x>=0&&x+width<=size[0]&&y-font>0&&y<size[1],"humor stays inside viewport");
                if(nativeLane)check(y-font>size[1]*.1+228&&y<size[1]*.9-50,"humor respects the original native status and Abort lanes");
            }
            BufferedImage image=new BufferedImage(size[0],size[1],BufferedImage.TYPE_INT_RGB);SceneProbe canvas=new SceneProbe(image,logo);
            LoadingScene.draw(canvas,size[0],size[1],models[index].snapshot(),visible,false,false,4097L<<20,12288L<<20,nativeLane?(size[1]*.1+228)/size[1]:0,nativeLane?(size[1]*.9-50)/size[1]:0);canvas.g.dispose();
            ImageIO.write(image,"png",out.resolve(names[index]+"-"+size[0]+(nativeLane?"-native":"")+".png").toFile());
        }
        for(int index=0;index<models.length;index++){
            BufferedImage[] still=new BufferedImage[2];
            for(int i=0;i<2;i++){still[i]=new BufferedImage(640,480,BufferedImage.TYPE_INT_RGB);SceneProbe c=new SceneProbe(still[i],logo);LoadingScene.draw(c,640,480,models[index].snapshot(),i*90,false,true,4097L<<20,12288L<<20);c.g.dispose();}
            check(!joke(frame(models[index],640,480,0,true,false)).isEmpty(),"reduced motion retains one static phrase");
            check(Arrays.equals(pixels(still[0]),pixels(still[1])),"reduced motion stays pixel-identical throughout quiet waits");
        }
        System.out.println("PASS "+checks+" occasional humor timing/real-scene/raster/reduced/native-lane checks");
    }
}
