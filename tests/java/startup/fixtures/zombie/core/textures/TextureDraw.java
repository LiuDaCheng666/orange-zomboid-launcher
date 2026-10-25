package zombie.core.textures;
public final class TextureDraw {
    public abstract static class GenericDrawer {
        public GenericDrawer(){}
        public abstract void render();
        public void render(TextureDraw ignored){render();}
        public void postRender(){}
    }
}
