package ozl.startup.tests;
import ozl.startup.*;
import java.lang.reflect.*;
import java.util.*;

/** Records the real scene command boundary, independent of raster/GL adapters. */
public final class SceneContractTest {
    static final class Frame implements InvocationHandler {
        double size,angle,leaf,x,y;int lines,background;String text="";final List<double[]> bars=new ArrayList<>();final List<Object[]> texts=new ArrayList<>();final Map<String,Double> textSizes=new HashMap<>();
        LoadingScene.Canvas canvas(){return (LoadingScene.Canvas)Proxy.newProxyInstance(getClass().getClassLoader(),new Class<?>[]{LoadingScene.Canvas.class},this);}
        public Object invoke(Object p,Method method,Object[] a){switch(method.getName()){
            case "measure":return ((String)a[0]).length()*(double)a[1]*.6;
            case "logo":case "growingLogo":x=(double)a[0];y=(double)a[1];size=(double)a[2];angle=a.length>=5?(double)a[4]:0;leaf=a.length>=7?(double)a[5]:1;break;
            case "line":lines++;break;
            case "gradient":if((double)a[0]==0&&(double)a[1]==0)background=(int)a[4];if((double)a[1]<140&&(double)a[3]<=45)bars.add(new double[]{(double)a[0],(double)a[1],(double)a[2],(double)a[3],(int)a[4]});break;
            case "text":text+=" "+a[0];textSizes.put((String)a[0],(double)a[3]);texts.add(a.clone());break;
        }return null;}
    }
    static Frame draw(LoadingModel m,double t,boolean reduced){Frame frame=new Frame();LoadingScene.draw(frame.canvas(),1440,900,m.snapshot(),t,false,reduced,4097L<<20,12288L<<20);return frame;}
    static void check(boolean b,String why){if(!b)throw new AssertionError(why);}
    public static void main(String[] args){
        if(args.length>0&&args[0].equals("palette")){palette();return;}
        if(args.length>0&&args[0].startsWith("queue")){queue(Integer.parseInt(args[0].substring(5)));return;}
        LoadingModel boot=new LoadingModel();Frame first=draw(boot,0,false),settled=draw(boot,2,false),later=draw(boot,9,false);
        check(settled.size==first.size,"approved entrance keeps the orange full-size while fading in");
        for(double time:new double[]{4,7,11,16,30,60,90}){Frame idle=draw(boot,time,false);check(idle.size>=174*.94&&idle.leaf>=.93,"long waits retain a fully grown source mark");}
        check(draw(boot,6,false).angle!=draw(boot,11,false).angle,"idle changes gesture without repeating seed growth");
        check(settled.text.contains("4,097")&&settled.text.contains("MiB"),"heap has measured MiB precision");
        check(settled.bars.size()>=2&&settled.lines>=5,"top uses connected real milestone nodes plus separate measured heap track");
        boolean measured=false;for(double[] a:settled.bars)for(double[] b:settled.bars)if(a!=b&&a[0]+2==b[0]&&a[1]+2==b[1]&&a[3]-4==b[3]&&a[2]>b[2]&&Math.abs(b[2]/(a[2]-4)-4097d/12288)<.00001)measured=true;
        check(measured,"heap fill width uses the actual measured used/max ratio");
        LoadingModel save=new LoadingModel();save.worldBegin(false,"ActualSave","Sandbox","Rosewood");Frame local=draw(save,2,false);
        LoadingModel server=new LoadingModel();server.serverBegin("Friendly server","203.0.113.7",16261);server.serverStage("WorkshopUpdate",132,8);server.serverStats("Riverside",8);server.queueInformation(server.snapshot().generation,17,32,1993,7,9,14,35);server.serverClock(server.snapshot().generation,1993,6,8,14,35,48);Frame remote=draw(server,2,false);
        Frame lightNative=new Frame();LoadingScene.draw(lightNative.canvas(),1280,720,server.snapshot(),4,true,false,0,0,300d/720,598d/720);
        check(((lightNative.background>>16)&255)<64&&((lightNative.background>>8)&255)<64&&(lightNative.background&255)<64,"light preference preserves dark native connection lane for unchanged white native status");
        check(local.lines>=20&&remote.lines>=6&&local.lines!=remote.lines,"save map chunks and server network use distinct scene geometry");
        check(local.text.contains("ActualSave")&&local.text.contains("沙盒")&&local.text.contains("Rosewood"),"scene exposes genuine local identity with common mode translated for display");
        check(remote.text.contains("132 项")&&remote.text.contains("8 个")&&remote.text.contains("创意工坊")&&remote.text.contains("模组"),"server labels project and mod-ID counts separately");
        check(remote.text.contains("203.0.113.7:16261")&&remote.text.contains("在线玩家")&&remote.text.contains("17 / 32")&&remote.text.contains("1993-07-09")&&remote.text.contains("14:35")&&remote.text.contains("第 3 天"),"server scene displays only proven current endpoint/population/game date/clock/world day");
        Frame compact=new Frame();LoadingScene.draw(compact.canvas(),640,480,server.snapshot(),2,false,false,4097L<<20,12288L<<20);
        check(compact.textSizes.entrySet().stream().filter(e->e.getKey().contains("203.0.113.7")||e.getKey().contains("1993-07-09")).allMatch(e->e.getValue()>=12),"central server endpoint/count/clock remain readable at small viewport");
        Frame still1=draw(boot,0,true),still2=draw(boot,50,true);check(still1.size==still2.size&&still1.angle==still2.angle,"reduced motion freezes orange growth and rotation");
        server.issue(new String[]{"模组脚本执行中断","当前对象不存在","ActualModName","RegisterWorld.lua:58","检查模组依赖"});
        for(int i=0;i<8;i++)server.nativeStatus(server.snapshot().generation,"Observed stage "+i);
        for(int[] viewport:new int[][]{{640,480},{1280,720},{1920,1080},{3840,2160}}){int w=viewport[0],h=viewport[1];Frame frame=new Frame();double status=h*.1+228,cancel=h*.9-50;LoadingScene.draw(frame.canvas(),w,h,server.snapshot(),4,false,false,4097L<<20,12288L<<20,status/h,cancel/h);
            check(frame.text.contains("第 3 天")&&frame.text.contains("203.0.113.7:16261"),"known critical fields preserved in native lanes");
            check(frame.y-frame.size/2>status&&frame.y+frame.size/2<cancel,"full grown orange stays between native controls");
            for(Object[] a:frame.texts){String value=(String)a[0];double width=frame.canvas().measure(value,(double)a[3]),xx=(double)a[1]-((boolean)a[5]?width:0),yy=(double)a[2],font=(double)a[3];
                check(!(xx<w/2+50&&xx+width>w/2-50&&yy>cancel&&yy-font<cancel+25),"activity/diagnostics cannot cover native Abort");
                check(!(xx<w/2+150&&xx+width>w/2-150&&yy>status-20&&yy-font<status),"custom information cannot cover native status");
                if(value.contains("ActualModName"))check((boolean)a[5],"mod diagnostics retain right alignment");
            }
        }
        String host="a".repeat(63)+"."+"b".repeat(63)+"."+"c".repeat(63)+"."+"d".repeat(61);server.endpoint(server.snapshot().generation,host,16261);
        for(boolean nativeLane:new boolean[]{false,true}){Frame frame=new Frame();LoadingScene.draw(frame.canvas(),640,480,server.snapshot(),4,false,false,0,0,nativeLane?276d/480:0,nativeLane?382d/480:0);check(frame.text.replace(" ","").contains(host+":16261"),"maximum complete endpoint wraps without ellipsis");}
        queue(-1);queue(0);queue(8);
        palette();
        System.out.println("PASS scene growth/rotation/context/precision contracts");
    }
    static void palette(){LoadingModel boot=new LoadingModel(),save=new LoadingModel(),server=new LoadingModel();save.worldBegin(false,"Save","Sandbox","Rosewood");server.serverBegin("Server");Frame a=draw(boot,4,false),b=draw(save,4,false),c=draw(server,4,false);check(a.background!=b.background&&a.background!=c.background&&b.background!=c.background,"three primary contexts have distinct palettes");check(a.x!=b.x&&a.x!=c.x&&b.lines!=c.lines,"approved boot is centered; save and server use distinct left-side map/network geometry");System.out.println("PASS three primary palette/composition contracts");}
    static void queue(int position){LoadingModel server=new LoadingModel();server.serverBegin("Queue fixture");if(position<0)server.queueWaiting();else server.queue(position);Frame frame=new Frame();LoadingScene.draw(frame.canvas(),640,480,server.snapshot(),4,false,false,0,0,276d/480,382d/480);check(frame.texts.stream().anyMatch(a->((String)a[0]).contains("Esc / 手柄 A 取消排队")&&(double)a[1]>128)&&(position<0||frame.text.contains("排队位置  "+position)),"queue "+position+" retains actual cancel hint with inherited native lease bounds");System.out.println("PASS queue hint at position "+position);}
}
