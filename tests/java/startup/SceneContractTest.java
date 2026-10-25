package ozl.startup.tests;
import ozl.startup.*;
import java.lang.reflect.*;
import java.util.*;

/** Records the real scene command boundary, independent of raster/GL adapters. */
public final class SceneContractTest {
    static final class Frame implements InvocationHandler {
        double size,angle;int lines;String text="";final List<double[]> bars=new ArrayList<>();final Map<String,Double> textSizes=new HashMap<>();
        LoadingScene.Canvas canvas(){return (LoadingScene.Canvas)Proxy.newProxyInstance(getClass().getClassLoader(),new Class<?>[]{LoadingScene.Canvas.class},this);}
        public Object invoke(Object p,Method method,Object[] a){switch(method.getName()){
            case "measure":return ((String)a[0]).length()*(double)a[1]*.6;
            case "logo":size=(double)a[2];angle=a.length>=5?(double)a[4]:0;break;
            case "line":lines++;break;
            case "gradient":if((double)a[1]<140&&(double)a[3]<=6)bars.add(new double[]{(double)a[0],(double)a[1],(double)a[2],(double)a[3],(int)a[4]});break;
            case "text":text+=" "+a[0];textSizes.put((String)a[0],(double)a[3]);break;
        }return null;}
    }
    static Frame draw(LoadingModel m,double t,boolean reduced){Frame frame=new Frame();LoadingScene.draw(frame.canvas(),1440,900,m.snapshot(),t,false,reduced,4097L<<20,12288L<<20);return frame;}
    static void check(boolean b,String why){if(!b)throw new AssertionError(why);}
    public static void main(String[] args){
        LoadingModel boot=new LoadingModel();Frame first=draw(boot,0,false),settled=draw(boot,2,false),later=draw(boot,9,false);
        check(settled.size>first.size*1.8,"orange visibly grows from the first accepted frame");
        check(Math.abs(later.angle-settled.angle)>.12,"orange itself rotates slowly after growth");
        check(settled.text.contains("4,097")&&settled.text.contains("MiB"),"heap has measured MiB precision");
        check(settled.bars.size()>=8,"top retains discrete real stages plus separate heap track and fill");
        boolean measured=false;for(double[] a:settled.bars)for(double[] b:settled.bars)if(a!=b&&a[0]==b[0]&&a[1]==b[1]&&a[3]==b[3]&&a[2]>b[2]&&Math.abs(b[2]/a[2]-4097d/12288)<.00001)measured=true;
        check(measured,"heap fill width uses the actual measured used/max ratio");
        LoadingModel save=new LoadingModel();save.worldBegin(false,"ActualSave","Sandbox","Rosewood");Frame local=draw(save,2,false);
        LoadingModel server=new LoadingModel();server.serverBegin("Friendly server","203.0.113.7",16261);server.serverStage("WorkshopUpdate",132,8);server.serverStats("Riverside",8);server.queueInformation(server.snapshot().generation,17,32,1993,7,9,14,35);server.serverClock(server.snapshot().generation,1993,6,8,14,35,48);Frame remote=draw(server,2,false);
        check(local.lines>=20&&remote.lines>=6&&local.lines!=remote.lines,"save map chunks and server network use distinct scene geometry");
        check(local.text.contains("ActualSave")&&local.text.contains("沙盒")&&local.text.contains("Rosewood"),"scene exposes genuine local identity with common mode translated for display");
        check(remote.text.contains("132 个创意工坊项目")&&remote.text.contains("8 个模组"),"server labels project and mod-ID counts separately");
        check(remote.text.contains("203.0.113.7:16261")&&remote.text.contains("在线 17 / 32")&&remote.text.contains("1993-07-09")&&remote.text.contains("14:35")&&remote.text.contains("世界第 3 天"),"server scene displays only proven current endpoint/population/game date/clock/world day");
        Frame compact=new Frame();LoadingScene.draw(compact.canvas(),640,480,server.snapshot(),2,false,false,4097L<<20,12288L<<20);
        check(compact.textSizes.entrySet().stream().filter(e->e.getKey().contains("203.0.113.7")||e.getKey().contains("1993-07-09")).allMatch(e->e.getValue()>=12),"central server endpoint/count/clock remain readable at small viewport");
        Frame still1=draw(boot,0,true),still2=draw(boot,50,true);check(still1.size==still2.size&&still1.angle==still2.angle,"reduced motion freezes orange growth and rotation");
        System.out.println("PASS scene growth/rotation/context/precision contracts");
    }
}
