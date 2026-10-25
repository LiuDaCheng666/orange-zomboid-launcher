package ozl.startup.tests;
import ozl.startup.*;
import zombie.*;
import zombie.gameStates.*;
import zombie.network.GameClient;
import zombie.Lua.LuaEventManager;
import java.util.*;
import java.net.*;

public final class ServerInformationTransformerTest {
    static int checks;static void check(boolean b,String why){if(!b)throw new AssertionError(why);checks++;}
    static void connect(int port,String name){GameClient.instance.doConnect("FORBIDDEN-user","FORBIDDEN-password","127.0.0.1","FORBIDDEN-local",Integer.toString(port),"FORBIDDEN-server-password",name,false,9,"FORBIDDEN-google-key");}
    static final class Poison {public String toString(){throw new AssertionError("unrelated queue info read");}}
    public static void main(String[] args)throws Exception{
        try(DatagramSocket blackhole=new DatagramSocket(new InetSocketAddress("127.0.0.1",0))){
            connect(blackhole.getLocalPort(),"Current server");check(StartupRuntime.snapshot().endpoint.equals("127.0.0.1:"+blackhole.getLocalPort()),"canonical exact host2/port4/name6 whitelist");
            check(StartupRuntime.snapshot().online==-1&&StartupRuntime.snapshot().gameClock.isEmpty()&&StartupRuntime.snapshot().maxPlayers==-1,"stale GameTime/options and unreplicated player lists are not observations");
            LoadingQueueState queue=new LoadingQueueState();queue.enter();LoadingQueueState.onConnectionImmediate();check(StartupRuntime.snapshot().online==-1&&StartupRuntime.snapshot().gameDate.isEmpty(),"immediate queue packet supplies no default zero fields");
            HashMap<String,Object> info=new HashMap<>();info.put("countPlayers",(short)17);info.put("maxPlayers",(short)32);info.put("Year",(short)1993);info.put("Month",7);info.put("Day",9);info.put("Hour",(byte)6);info.put("Minutes",(byte)59);info.put("FORBIDDEN",new Poison());
            LoadingQueueState.onPlaceInQueue(2,info);check(StartupRuntime.snapshot().online==17&&StartupRuntime.snapshot().gameDate.equals("1993-07-09")&&StartupRuntime.snapshot().gameClock.equals("06:59"),"known Short/Byte/Integer queue values only, already-one-based date");
            long operation=StartupRuntime.snapshot().generation;LuaEventManager.triggerEvent("OnConnected");ConnectToServerState state=new ConnectToServerState();state.exit();state.exit();
            check(StartupRuntime.snapshot().generation==operation&&StartupRuntime.snapshot().online==17,"repeated real handoffs preserve current server receipt");
            state.loadTime(false);state.loadOptions(false);check(StartupRuntime.snapshot().gameClock.equals("14:35")&&StartupRuntime.snapshot().worldDay==3&&StartupRuntime.snapshot().maxPlayers==32,"normal-return GameTime/options prove current server clock/day/limit");
            GameLoadingState world=new GameLoadingState();world.enter();GameTime.instance.minutes=36;world.render();check(StartupRuntime.snapshot().gameClock.equals("14:36")&&StartupRuntime.snapshot().online==17,"current server game time refreshes through world load after proof");
            connect(blackhole.getLocalPort(),"Second server");boolean thrown=false;try{state.loadTime(true);}catch(java.io.IOException original){thrown=original.getMessage().equals("original clock");}check(thrown&&StartupRuntime.snapshot().gameClock.isEmpty(),"original time-load exception stays original and never blesses old globals");
            world.enter();world.render();check(StartupRuntime.snapshot().gameClock.isEmpty(),"later world cannot read previous GameTime before current proof");
        }
        try(ServerQueryTest.Loopback server=new ServerQueryTest.Loopback((s,p)->ServerQueryTest.send(s,p,ServerQueryTest.info(12,48)))){
            connect(server.port(),"Immediate server");long until=System.nanoTime()+2_000_000_000L;while(StartupRuntime.snapshot().online<0&&System.nanoTime()<until)Thread.sleep(10);
            check(StartupRuntime.snapshot().online==12&&StartupRuntime.snapshot().maxPlayers==48,"ordinary immediate route receives own off-thread loopback query result");
        }
        StartupRuntime.handoff(false);check(StartupRuntime.snapshot().endpoint.isEmpty()&&StartupRuntime.snapshot().online==-1,"real cancellation clears endpoint/count/clock and owned work");
        System.out.println("PASS "+checks+" real-premain server information whitelist/proof/handoff contracts");
    }
}
