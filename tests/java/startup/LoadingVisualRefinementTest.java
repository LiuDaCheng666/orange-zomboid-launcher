package ozl.startup;

import java.lang.reflect.*;
import java.util.*;

/** Visible behavior contracts for the once-only entrance, idle repertoire and readable facts. */
public final class LoadingVisualRefinementTest {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static final class Commands implements InvocationHandler {
        final List<Object[]> text=new ArrayList<>();
        LoadingScene.Canvas canvas(){return (LoadingScene.Canvas)Proxy.newProxyInstance(getClass().getClassLoader(),new Class<?>[]{LoadingScene.Canvas.class},this);}
        public Object invoke(Object proxy,Method method,Object[] args){
            if(method.getName().equals("measure"))return ((String)args[0]).length()*(double)args[1]*.6;
            if(method.getName().equals("text"))text.add(args.clone());return null;
        }
        boolean has(String value){return text.stream().anyMatch(a->((String)a[0]).equals(value));}
    }
    static void motion(){
        check(LogoGrowth.at(0,false).bodyScale()<.35&&LogoGrowth.at(2.8,false).bodyScale()>.95,"entrance grows and unfolds once");
        Set<String> poses=new HashSet<>();boolean quiet=false,tilt=false,leaf=false,hop=false;
        for(int i=60;i<2400;i++){
            LogoGrowth.Frame f=LogoGrowth.at(i*.05,false);
            check(f.bodyScale()>=.94&&f.bodyScale()<=1.06,"long wait must retain full grown orange instead of shrinking back to seed at "+i*.05);
            check(f.leafScale()>=.93&&Math.abs(f.angle())<=Math.PI*2+.03&&Math.abs(f.leafAngle())<.56&&Math.abs(f.lift())<.12,"idle stays recognizable and bounded");
            quiet|=Math.abs(f.angle())<.001&&Math.abs(f.leafAngle())<.001&&Math.abs(f.lift())<.001;
            tilt|=Math.abs(f.angle())>.07;leaf|=Math.abs(f.leafAngle())>.16;hop|=f.lift()<-.035;
            poses.add(String.format(Locale.ROOT,"%.2f %.2f %.2f",f.angle(),f.leafAngle(),f.lift()));
        }
        check(quiet&&tilt&&leaf&&hop&&poses.size()>25,"idle repertoire has quiet beats, roll, leaf wave and a restrained hop");
        Set<Integer> orientations=new HashSet<>();
        for(int i=0;i<50;i++){LogoGrowth.Frame f=LogoGrowth.at(4+i*.1,false);orientations.add(Math.floorMod((int)Math.floor(f.angle()/(Math.PI/2)),4));}
        check(orientations.size()==4,"slow full turn visibly visits all four orientations");
        check(Math.abs(Math.sin(LogoGrowth.at(8.999,false).angle())-Math.sin(LogoGrowth.at(9.001,false).angle()))<.003,"full-turn settle remains visually continuous modulo one turn");
        double leafSwing=0,hopLift=0;for(int i=0;i<300;i++){LogoGrowth.Frame f=LogoGrowth.at(3+i*.1,false);leafSwing=Math.max(leafSwing,Math.abs(f.leafAngle()));hopLift=Math.min(hopLift,f.lift());}
        check(leafSwing>.4&&hopLift<-.10,"leaf wave and soft hop remain clearly distinct from a quiet pose");
        for(double t:new double[]{0,1,9,33,120})check(LogoGrowth.at(t,true).equals(LogoGrowth.at(0,true)),"reduced motion stays static");
    }
    static void humor(){
        Set<String> phrases=new HashSet<>();String previous="",last="";double entered=0,exited=0;
        for(int i=0;i<10000;i++){
            double t=i*.1;String current=LoadingHumor.at(t,1,LoadingModel.Context.BOOT,false).text();
            check(current.equals(LoadingHumor.at(t,1,LoadingModel.Context.BOOT,false).text()),"repainting the same session time keeps the shuffled caption stable");
            if(!current.isEmpty()&&previous.isEmpty()){check(!current.equals(last),"consecutive captions do not repeat");if(exited>0)check(t-exited>=1&&t-exited<=4,"short quiet gap keeps the waiting sequence alive");entered=t;phrases.add(current);last=current;}
            if(current.isEmpty()&&!previous.isEmpty()){check(t-entered>=4.5&&t-entered<=5.2,"caption remains readable for about five seconds");exited=t;}
            previous=current;
        }
        check(phrases.size()>=80&&phrases.size()<=110,"long wait offers an expanded deck of short PZ captions");
        for(String required:new String[]{"狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊"})check(phrases.contains(required),"requested joke appears: "+required);
    }
    static void layout(){
        LoadingModel boot=new LoadingModel();boot.phase("Loading Mods");boot.mod("VisibleActivity");Commands c=new Commands();LoadingScene.draw(c.canvas(),1280,720,boot.snapshot(),11,false,false,4097L<<20,12288L<<20);
        check(c.has("初始化")&&c.has("模组")&&c.has("资源")&&c.has("脚本")&&c.has("界面")&&c.has("就绪"),"top track labels actual observed loading milestones");
        check(c.text.stream().anyMatch(a->((String)a[0]).contains("VisibleActivity")),"observed activity remains visible independently of humor");
        LoadingModel server=new LoadingModel();server.serverBegin("橙子社区服务器","203.0.113.7",16261);server.serverStage("WorkshopUpdate",132,84);server.serverStats("Riverside",84);server.queueInformation(server.snapshot().generation,17,32,1993,7,9,14,35);server.serverClock(server.snapshot().generation,1993,6,8,14,35,48);
        c=new Commands();LoadingScene.draw(c.canvas(),1280,720,server.snapshot(),11,false,false,4097L<<20,12288L<<20);
        for(String value:new String[]{"橙子社区服务器","203.0.113.7:16261","在线玩家","游戏时间","世界天数","模组","创意工坊"})check(c.has(value),"server facts have readable labeled hierarchy: "+value);
    }
    public static void main(String[] args){switch(args[0]){case "motion"->motion();case "humor"->humor();case "layout"->layout();default->throw new IllegalArgumentException();}System.out.println("PASS "+checks+" "+args[0]+" refinement contracts");}
}
