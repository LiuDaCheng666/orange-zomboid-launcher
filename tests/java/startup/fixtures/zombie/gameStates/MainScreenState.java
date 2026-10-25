package zombie.gameStates;
public final class MainScreenState extends GameState {
    public static final MainScreenState instance=new MainScreenState();
    public int entered;public boolean failBackground,failRender;
    public void enter(){entered++;}
    public void renderBackground(){zombie.core.SpriteRenderer.instance.queue.add("background");if(failBackground)throw new IllegalArgumentException("original background");}
    public void render(){boolean prior=zombie.ui.UIManager.useUiFbo;zombie.ui.UIManager.useUiFbo=false;try{renderBackground();if(failRender)throw new IllegalStateException("original render");zombie.ui.UIManager.render();}finally{zombie.ui.UIManager.useUiFbo=prior;}}
}
