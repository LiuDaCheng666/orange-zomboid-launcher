package ozl.startup.tests;
import ozl.startup.*;
import java.lang.reflect.*;

public final class ServerInformationTest {
    static int checks;
    static void check(boolean v,String why){if(!v)throw new AssertionError(why);checks++;}
    static Object field(Object o,String n)throws Exception{try{return o.getClass().getField(n).get(o);}catch(NoSuchFieldException e){throw new AssertionError("Missing server receipt field: "+n);}}
    static void call(Object m,String n,Class<?>[] t,Object...a)throws Exception{try{m.getClass().getMethod(n,t).invoke(m,a);}catch(NoSuchMethodException e){throw new AssertionError("Missing server receipt observation: "+n);}}
    static long generation(LoadingModel m)throws Exception{return (long)field(m.snapshot(),"generation");}
    public static void main(String[] args)throws Exception{
        LoadingModel m=new LoadingModel();generation(m);
        call(m,"serverBegin",new Class<?>[]{String.class,String.class,int.class},"Current server","203.0.113.7",16261);long operation=generation(m);
        check(field(m.snapshot(),"endpoint").equals("203.0.113.7:16261")&&(int)field(m.snapshot(),"online")==-1&&field(m.snapshot(),"gameClock").equals(""),"new current target shows authorized endpoint, hides unknown population and game time");
        call(m,"queueInformation",new Class<?>[]{long.class,int.class,int.class,int.class,int.class,int.class,int.class,int.class},operation,17,32,1993,7,9,6,59);
        check((int)field(m.snapshot(),"online")==17&&field(m.snapshot(),"gameDate").equals("1993-07-09")&&field(m.snapshot(),"gameClock").equals("06:59"),"queue date is already one-based and real population is retained");
        call(m,"publicInfo",new Class<?>[]{long.class,String.class,String.class,int.class,int.class},operation,"Public name","Muldraugh",1,4);
        check((int)field(m.snapshot(),"online")==17&&(int)field(m.snapshot(),"maxPlayers")==32&&m.snapshot().name.equals("Current server"),"late lower-authority public result cannot replace queue counts or friendly name");
        call(m,"serverClock",new Class<?>[]{long.class,int.class,int.class,int.class,int.class,int.class,double.class},operation,1993,0,0,7,0,24d);
        check(field(m.snapshot(),"gameDate").equals("1993-01-01")&&(long)field(m.snapshot(),"worldDay")==2,"proven GameTime uses zero-based month/day and world-age day at07:00 boundary");
        m.handoff(true);long rendered=m.snapshot().epoch;m.handoff(true);m.handoff(true);
        check(generation(m)==operation&&(int)field(m.snapshot(),"online")==17&&field(m.snapshot(),"endpoint").equals("203.0.113.7:16261"),"repeated deliberate auth/character handoffs retain same-operation receipt");
        m.worldBegin(true,"FORBIDDEN-cache","","Riverside");
        check(generation(m)==operation&&m.snapshot().epoch>rendered&&field(m.snapshot(),"gameClock").equals("07:00"),"server world changes render epoch without losing current receipt");
        call(m,"serverBegin",new Class<?>[]{String.class,String.class,int.class},"Other","198.51.100.9",16262);long next=generation(m);
        check(next>operation&&(int)field(m.snapshot(),"online")==-1&&field(m.snapshot(),"gameDate").equals(""),"new server clears previous count/date/clock proof");
        call(m,"publicInfo",new Class<?>[]{long.class,String.class,String.class,int.class,int.class},operation,"Old","OldMap",23,64);
        check((int)field(m.snapshot(),"online")==-1&&m.snapshot().map.isEmpty(),"late previous-operation UDP reply cannot populate a new server");
        m.serverStage("WorkshopUpdate",132,8);m.serverStats("CurrentMap",8);m.handoff(true);m.handoff(true);m.worldBegin(true,"FORBIDDEN-cache","","CurrentMap");
        check(m.snapshot().requestedMods==8&&m.snapshot().workshopProjects==132,"known current-operation mod/project counts survive deliberate handoffs and world entry");
        call(m,"publicInfo",new Class<?>[]{long.class,String.class,String.class,int.class,int.class},next,"Public","WrongMap",5,32);
        check(m.snapshot().map.equals("CurrentMap"),"late public map cannot replace current actual world map");
        m.worldBegin(false,"Local","Sandbox","Rosewood");check(field(m.snapshot(),"endpoint").equals("")&&(int)field(m.snapshot(),"online")==-1,"local save clears all server receipts");
        System.out.println("PASS "+checks+" server-information receipt/authority/generation contracts");
    }
}
