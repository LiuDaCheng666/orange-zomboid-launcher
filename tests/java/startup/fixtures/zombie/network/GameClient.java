package zombie.network;
import java.util.ArrayList;
public final class GameClient {
    public static boolean client;
    public static String serverName="Stale static server",gameMap="Riverside";
    public static String ip="Old ip";public static int port=123;
    public static final GameClient instance=new GameClient();
    public final ArrayList<String> serverMods=new ArrayList<>();
    public int calls,sync;
    public boolean failConnect,failCoop;
    public int loginQueueDoneRequests;public boolean failLoginQueueDone;
    public enum RequestState {Start,Loading,Complete}
    public void doConnect(String a,String b,String c,String d,String e,String f,String friendly,boolean h,int i){doConnect(a,b,c,d,e,f,friendly,h,i,"credential");}
    public void doConnect(String a,String b,String c,String d,String e,String f,String friendly,boolean h,int i,String j){calls++;if(failConnect)throw new IllegalStateException("original connect");ip=c;port=Integer.parseInt(e);serverName=friendly;client=true;}
    public void doConnectCoop(String privateIdentifier){calls++;if(failCoop)throw new IllegalStateException("original coop");ip="127.0.0.1";port=0;client=true;}
    public void disconnect(boolean force){calls++;client=false;}
    public void doDisconnect(String privateReason){calls++;client=false;}
    public void setRequest(RequestState state){sync++;}
    public void GameLoadingRequestData(){sync++;}
    public void sendLoginQueueDone(long elapsed){loginQueueDoneRequests++;if(failLoginQueueDone)throw new IllegalStateException("original acknowledgement");}
}
