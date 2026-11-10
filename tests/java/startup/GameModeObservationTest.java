package ozl.startup.tests;

import java.lang.reflect.Method;
import java.nio.file.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;
import ozl.startup.LoadingModel;
import ozl.startup.LoadingScene;
import ozl.startup.StartupRuntime;
import zombie.core.GameMode;

/** Exercises the production observation helper with explicit old/new API fixtures. */
public final class GameModeObservationTest {
    static int checks;
    static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);checks++;}
    public static final class LegacyCore {public static String gameMode="Sandbox";}
    public static final class CurrentCore {
        public GameMode gameMode=new GameMode("Sandbox");
        static final CurrentCore instance=new CurrentCore();static int reads;
        public static CurrentCore getInstance(){reads++;return instance;}
    }
    public static final class MissingCore {}
    public static final class NullCore {
        public GameMode gameMode;
        public static NullCore getInstance(){return null;}
    }
    public static final class UnavailableCore {
        public GameMode gameMode;
        public static UnavailableCore getInstance(){throw new IllegalStateException("not ready");}
    }
    public static final class UnknownCore {
        public Object gameMode=new Object();
        public static UnknownCore getInstance(){throw new AssertionError("unknown field must not be observed");}
    }
    static String observe(Class<?> core)throws Exception {
        Method reader=StartupRuntime.class.getDeclaredMethod("string",Class.class,String.class);
        reader.setAccessible(true);return (String)reader.invoke(null,core,"gameMode");
    }
    static void image(Path output,String mode,Path logo,int width,int height)throws Exception {
        LoadingModel model=new LoadingModel();model.worldBegin(false,"当前存档",mode,"Muldraugh, KY");
        BufferedImage raster=new BufferedImage(width,height,BufferedImage.TYPE_INT_RGB);
        SceneProbe canvas=new SceneProbe(raster,ImageIO.read(logo.toFile()));
        LoadingScene.draw(canvas,width,height,model.snapshot(),1.4,false,false,2048L<<20,8192L<<20);
        canvas.g.dispose();ImageIO.write(raster,"png",output.toFile());
    }
    public static void main(String[] args)throws Exception {
        check(observe(LegacyCore.class).equals("Sandbox"),"42.21 static String mode remains observable without getInstance");
        check(observe(CurrentCore.class).equals("Sandbox"),"42.22 instance GameMode must supply the current Sandbox ID");
        check(CurrentCore.reads==1,"new mode uses the current Core instance once");
        CurrentCore.instance.gameMode=new GameMode("Apocalypse");
        check(observe(CurrentCore.class).equals("Apocalypse"),"later world entry observes its new mode without a cached Sandbox");
        CurrentCore.instance.gameMode=null;
        check(observe(CurrentCore.class).isEmpty(),"unknown current mode stays empty");
        check(observe(MissingCore.class).isEmpty(),"missing mode observation does not throw");
        check(observe(UnavailableCore.class).isEmpty(),"not-ready Core only omits display information");
        check(observe(NullCore.class).isEmpty(),"null Core instance only omits display information");
        CurrentCore.instance.gameMode=new GameMode("Sandbox");CurrentCore.instance.gameMode.failText=true;
        check(observe(CurrentCore.class).isEmpty(),"mode text failure only omits display information");
        check(observe(UnknownCore.class).isEmpty(),"unknown object does not leak an Object identity string or invoke instance API");
        LegacyCore.gameMode=null;check(observe(LegacyCore.class).isEmpty(),"legacy null mode stays empty");
        LegacyCore.gameMode="Sandbox";
        if(args.length==2){
            Path output=Paths.get(args[0]);Files.createDirectories(output);Path logo=Paths.get(args[1]);
            CurrentCore.instance.gameMode=new GameMode("Sandbox");
            for(int[] size:new int[][]{{1280,720},{640,480}}){
                image(output.resolve("legacy-"+size[0]+".png"),observe(LegacyCore.class),logo,size[0],size[1]);
                image(output.resolve("current-"+size[0]+".png"),observe(CurrentCore.class),logo,size[0],size[1]);
            }
        }
        System.out.println("PASS "+checks+" old/new game-mode production observations; fixture API shapes, no game entry point");
    }
}
