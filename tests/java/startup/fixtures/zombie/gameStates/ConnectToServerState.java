package zombie.gameStates;
import java.util.ArrayList;
public final class ConnectToServerState extends GameState {
    private enum State {Start,TestTCP,WorkshopInit,WorkshopQuery,WorkshopConfirm,ServerWorkshopItemScreen,WorkshopUpdate,CheckMods,Finish,Exit}
    private State state=State.Start;
    private final ArrayList<Object> workshopItems=new ArrayList<>(),confirmItems=new ArrayList<>();
    public int calls;public boolean throwUpdate;public Runnable duringUpdate;
    public void enter(){calls++;state=State.Start;}
    public GameStateMachine.StateAction update(){calls++;if(throwUpdate)throw new IllegalStateException("original update");state=State.WorkshopUpdate;if(duringUpdate!=null)duringUpdate.run();return GameStateMachine.StateAction.Remain;}
    public void FromLua(String command){calls++;if(command.equals("bad"))throw new IllegalArgumentException("original command");if(command.equals("install"))state=State.WorkshopUpdate;if(command.equals("disconnect"))state=State.Exit;}
    public enum WorkshopItemState {CheckItemState,SubscribePending,DownloadPending,Ready,Fail}
    public static final class WorkshopItem {public long id;public WorkshopItemState state;public Object details;public WorkshopItem(long id,WorkshopItemState state){this.id=id;this.state=state;}}
    public void items(WorkshopItem... values){workshopItems.clear();java.util.Collections.addAll(workshopItems,values);}
    public void exit(){calls++;}
    public void loadTime(boolean fail)throws java.io.IOException{receiveGameTime(new zombie.core.network.ByteBufferReader(fail));}
    private void receiveGameTime(zombie.core.network.ByteBufferReader reader)throws java.io.IOException{calls++;if(reader.fail)throw new java.io.IOException("original clock");zombie.GameTime t=zombie.GameTime.instance;t.year=1993;t.month=6;t.day=8;t.hour=14;t.minutes=35;t.worldHours=48;}
    public void loadOptions(boolean fail)throws java.io.IOException{receiveServerOptions(new zombie.core.network.ByteBufferReader(fail));}
    private void receiveServerOptions(zombie.core.network.ByteBufferReader reader)throws java.io.IOException{calls++;if(reader.fail)throw new java.io.IOException("original options");zombie.network.ServerOptions.instance.maximum=32;}
}
