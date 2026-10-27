package zombie.gameStates;
import java.util.ArrayList;
public final class ChooseGameInfo {
 public static final class Mod {
  private ArrayList<String> require=new ArrayList<String>();
  public Mod(String directory){}
  public ArrayList<String> getRequire(){return require;}
  public void setRequire(ArrayList<String> value){require=value;}
 }
}
