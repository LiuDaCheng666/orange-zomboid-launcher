package zombie.modding;
import java.util.ArrayList;
public final class ActiveMods {public String source="";private final ArrayList<String> mods=new ArrayList<String>();private static final ActiveMods value=new ActiveMods("default");public ActiveMods(String id){}public static ActiveMods getById(String id){return value;}public ArrayList<String> getMods(){return mods;}}
