package ozl.startup.tests;

import ozl.startup.LoadingModel;
import java.io.File;
import java.lang.reflect.*;
import java.util.Arrays;

/** Consumer-visible operation contracts; no actual game classes or filesystem scan. */
public final class RefinementModelTest {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static Object field(Object value,String name)throws Exception{
        try{return value.getClass().getField(name).get(value);}catch(NoSuchFieldException missing){throw new AssertionError("Missing loading observation: "+name);}
    }
    static Object call(Object target,String name,Class<?>[] types,Object...values)throws Exception{
        try{return target.getClass().getMethod(name,types).invoke(target,values);}catch(NoSuchMethodException missing){throw new AssertionError("Missing loading operation: "+name);}
    }
    static void text(LoadingModel m,String name,String value)throws Exception{call(m,name,new Class<?>[]{String.class},value);}
    static String all(LoadingModel m){return m.snapshot().title+" "+m.snapshot().detail+" "+Arrays.toString(m.snapshot().activity);}
    public static void main(String[] args)throws Exception{
        LoadingModel m=new LoadingModel();
        check(field(m.snapshot(),"context").toString().equals("BOOT")&&field(m.snapshot(),"route").toString().equals("FULL"),"fresh boot owns full presentation");
        text(m,"early","mainThreadInit");
        check(all(m).contains("初始化"),"actual early boundary supplies activity before mods");
        call(m,"resource",new Class<?>[]{File.class,long.class},new File("C:/Users/Private/media/lua/First.lua"),1_000_000_000L);
        for(int i=0;i<10000;i++)call(m,"resource",new Class<?>[]{File.class,long.class},new File("C:/Users/Private/media/lua/Secret"+i+".lua"),1_100_000_000L);
        check((long)field(m.snapshot(),"scannedResources")==10001,"scan callbacks count genuine work without deduplicating paths");
        check(all(m).contains("First.lua")&&!all(m).contains("Private")&&!all(m).contains("Secret9999"),"resource labels are basename-only and throttled");
        call(m,"resource",new Class<?>[]{File.class,long.class},new File("D:/media/textures/Next.png"),1_300_000_000L);
        check(all(m).contains("Next.png")&&m.snapshot().activity.length<=5,"next accepted label stays bounded");
        long boot=(long)field(m.snapshot(),"epoch");m.menuReady();text(m,"early","fonts");
        check(!m.snapshot().active,"late boot observations cannot reopen menu");
        call(m,"worldBegin",new Class<?>[]{boolean.class,String.class,String.class,String.class},false,"C:/Users/Private/Saves/My World","Sandbox","Muldraugh, KY");
        check(field(m.snapshot(),"context").toString().equals("SAVE")&&field(m.snapshot(),"name").equals("My World")&&field(m.snapshot(),"mode").equals("Sandbox"),"local scene shows only real save basename and mode");
        check((long)field(m.snapshot(),"epoch")>boot&&(long)field(m.snapshot(),"scannedResources")==0,"world starts fresh operation counters");
        m.phase("Loading Scripts");check(m.snapshot().stageIndex==1,"world subphase cannot fill the boot-stage track or claim world completion");
        call(m,"conversion",new Class<?>[]{boolean.class,int.class,int.class},true,7,19);
        check(all(m).contains("7 / 19"),"real conversion counts display independently of completion");
        m.worldStatus("正在读取世界");call(m,"conversion",new Class<?>[]{boolean.class,int.class,int.class},true,7,19);
        String[] observed=m.snapshot().activity;
        for(int i=0;i<20;i++){m.worldStatus("正在读取世界");call(m,"conversion",new Class<?>[]{boolean.class,int.class,int.class},true,7,19);}
        check(Arrays.equals(observed,m.snapshot().activity),"unchanged world/conversion values never manufacture alternating repeated log rows");
        m.issue(new String[]{"old","","","",""});text(m,"serverBegin","Friendly Server");
        check(field(m.snapshot(),"context").toString().equals("SERVER")&&field(m.snapshot(),"route").toString().equals("BEHIND_UI")&&m.snapshot().issue.length==0,"server begins fresh behind original interactive UI");
        check(!m.snapshot().world&&field(m.snapshot(),"name").equals("Friendly Server"),"connection identity belongs to its operation");
        call(m,"serverStage",new Class<?>[]{String.class,int.class,int.class},"WorkshopUpdate",132,8);
        check((int)field(m.snapshot(),"workshopProjects")==132&&(int)field(m.snapshot(),"requestedMods")==8,"workshop projects and requested mod IDs remain different real counts");
        call(m,"queue",new Class<?>[]{int.class},3);check(all(m).contains("3"),"queue shows real position");
        call(m,"worldBegin",new Class<?>[]{boolean.class,String.class,String.class,String.class},true,"192.0.2.1_encrypted-user","","Riverside");
        check(field(m.snapshot(),"context").toString().equals("SERVER")&&field(m.snapshot(),"name").equals("Friendly Server")&&field(m.snapshot(),"route").toString().equals("FULL"),"server world retains friendly identity without multiplayer cache path");
        check(!all(m).contains("encrypted-user"),"multiplayer save identity never leaks");
        text(m,"serverBegin","");check(field(m.snapshot(),"name").equals("")&&(int)field(m.snapshot(),"workshopProjects")==-1,"coop/new connection clears prior server name and counts");
        long before=(long)field(m.snapshot(),"epoch");call(m,"handoff",new Class<?>[]{boolean.class},true);
        check(!m.snapshot().active&&(long)field(m.snapshot(),"epoch")>before,"handoff invalidates all queued presentation tickets");
        call(m,"worldBegin",new Class<?>[]{boolean.class,String.class,String.class,String.class},false,"LaterSave","Survivor","Rosewood");
        check(field(m.snapshot(),"name").equals("LaterSave")&&(int)field(m.snapshot(),"requestedMods")==-1&&m.snapshot().issue.length==0,"later local operation inherits no server state");
        Class<?> clockType;
        try{clockType=Class.forName("ozl.startup.AnimationClock");}catch(ClassNotFoundException missing){throw new AssertionError("First-visible animation clock missing");}
        Object clock=clockType.getConstructor().newInstance();
        check((double)call(clock,"seconds",new Class<?>[]{long.class,String.class,long.class},1L,"BOOT",99_000_000_000L)==0,"entrance starts on accepted first draw, independent of premain elapsed");
        check((double)call(clock,"seconds",new Class<?>[]{long.class,String.class,long.class},1L,"BOOT",100_000_000_000L)==1,"decorative time advances only from accepted origin");
        check((double)call(clock,"seconds",new Class<?>[]{long.class,String.class,long.class},2L,"SAVE",199_000_000_000L)==0,"new operation resets decorative origin");
        System.out.println("PASS "+checks+" loading refinement model contracts");
    }
}
