package zombie.core;
public final class Core {
 public static boolean debug=false;
 public static String gameSaveWorld="C:/Users/Private/Saves/ActualSave",gameMode="Sandbox",gameMap="Rosewood";
 public static final Core instance=new Core();
 public int starts,ends,uiStarts,uiEnds;
 public static Core getInstance(){return instance;}
 public void StartFrame(){starts++;} public void EndFrame(){ends++;}
 public boolean StartFrameUI(){uiStarts++;return true;} public void EndFrameUI(){uiEnds++;}
}
