package zombie.gameStates;
public class HookFixture {
 public static void main(String[] args){Object result=new IngameState().update();if(result!=IngameState.TOKEN)throw new AssertionError("update return altered");System.out.println("VANILLA_RETURN_PRESERVED");}
}
class IngameState {
 static final Object TOKEN=new Object();
 public Object update(){System.out.println("VANILLA_UPDATE_PRESERVED");return TOKEN;}
}
