package zombie.network;
public final class ServerOptions {public static final ServerOptions instance=new ServerOptions();public int maximum=999;public static ServerOptions getInstance(){return instance;}public int getMaxPlayers(){return maximum;}}
