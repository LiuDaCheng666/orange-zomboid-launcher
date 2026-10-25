package zombie.core;
import java.util.*;
import zombie.core.textures.TextureDraw;
public final class SpriteRenderer {
    public static final SpriteRenderer instance=new SpriteRenderer();
    public final List<Object> queue=new ArrayList<>();
    public final List<String> rendered=new ArrayList<>();
    public Runnable background,controls;
    public TextureDraw drawGeneric(TextureDraw.GenericDrawer drawer){queue.add(drawer);return new TextureDraw();}
    public void drain(){for(Object command:new ArrayList<>(queue)){
        if(command instanceof TextureDraw.GenericDrawer drawer){rendered.add("owned");drawer.render(new TextureDraw());}
        else {String name=(String)command;rendered.add(name);Runnable action=name.equals("background")?background:controls;if(action!=null)action.run();}
    }}
    public void recycle(){for(Object command:queue)if(command instanceof TextureDraw.GenericDrawer drawer)drawer.postRender();queue.clear();rendered.clear();}
}
