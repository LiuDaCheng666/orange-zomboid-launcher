package zombie.core;
public final class Core {
 public static boolean debug=false;
 public static String gameSaveWorld="C:/Users/Private/Saves/ActualSave",gameMode="Sandbox",gameMap="Rosewood";
 public static final Core instance=new Core();
 public int starts,ends,uiStarts,uiEnds;
 public int resets;public Runnable duringReset;
 public static Core getInstance(){return instance;}
 public void StartFrame(){starts++;} public void EndFrame(){ends++;}
 public boolean StartFrameUI(){uiStarts++;return true;} public void EndFrameUI(){uiEnds++;}
 // Installed ResetLua renders once, releases render-state waiting, then reloads
 // synchronously without producing further CPU sprite frames.
 public void ResetLua(boolean reset,String reason)throws java.io.IOException{ResetLua("default",reason);}
 public void ResetLua(String mods,String reason)throws java.io.IOException{
  resets++;zombie.gameStates.MainScreenState.instance.render();
  zombie.core.opengl.RenderThread.setWaitForRenderState(false);
  if(duringReset!=null)duringReset.run();
  zombie.core.opengl.RenderThread.setWaitForRenderState(true);
  zombie.Lua.LuaEventManager.triggerEvent("OnResetLua",reason);
 }
}
