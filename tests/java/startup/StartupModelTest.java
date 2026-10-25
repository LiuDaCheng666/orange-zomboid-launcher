package ozl.startup.tests;

import java.lang.reflect.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

public final class StartupModelTest {
    private static int checks;
    private static Class<?> modelClass;
    static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
        checks++;
    }
    static Object call(Object object, String method, Class<?>[] types, Object... args) throws Exception {
        return object.getClass().getMethod(method, types).invoke(object, args);
    }
    static Object field(Object object, String name) throws Exception { return object.getClass().getField(name).get(object); }
    static Object snapshot(Object model) throws Exception { return call(model, "snapshot", new Class<?>[0]); }
    static void event(Object model, String method, String value) throws Exception { call(model, method, new Class<?>[]{String.class}, value); }
    static String b64(String value) { return Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8)); }
    public static void main(String[] args) throws Exception {
        try { modelClass = Class.forName("ozl.startup.LoadingModel"); }
        catch (ClassNotFoundException missing) { throw new AssertionError("Game-side loading component is not implemented", missing); }
        Object model = modelClass.getConstructor().newInstance();
        check((boolean)field(snapshot(model), "active"), "a fresh accepted launch starts loading");
        event(model, "phase", "Loading Mods");
        event(model, "mod", "ExampleNewMod");
        event(model, "mod", "OtherAuthorMod");
        Object current = snapshot(model);
        check((int)field(current, "processedMods") == 2, "distinct real mod events update the count");
        check(Arrays.toString((String[])field(current, "activity")).contains("OtherAuthorMod"), "latest real mod appears in activity");
        event(model, "mod", "OtherAuthorMod");
        check((int)field(snapshot(model), "processedMods") == 2, "duplicate mod notification does not inflate progress");
        event(model, "script", "C:/Users/Alice/Zomboid/mods/UnseenMod/42/media/lua/client/Fix.lua");
        String activity = Arrays.toString((String[])field(snapshot(model), "activity"));
        check(activity.contains("UnseenMod") && activity.contains("Fix.lua") && !activity.contains("Alice"), "script activity locates an unseen mod without exposing user paths");
        for (int i=0; i<2000; i++) event(model, "script", "media/lua/shared/Script"+i+".lua");
        check(((String[])field(snapshot(model), "activity")).length <= 5, "dense real logs remain bounded on screen");
        call(model, "menuReady", new Class<?>[0]);
        check(!(boolean)field(snapshot(model), "active"), "real menu transition removes the loading presentation");
        event(model, "phase", "Loading Lua");
        check(!(boolean)field(snapshot(model), "active"), "late boot log cannot cover the menu again");
        call(model, "worldBegin", new Class<?>[0]);
        check((boolean)field(snapshot(model), "active"), "a real world-load transition starts its own presentation");
        call(model, "waitingForInput", new Class<?>[0]);
        check(!(boolean)field(snapshot(model), "active"), "required input is never covered");
        call(model, "worldEnd", new Class<?>[0]);
        check(!(boolean)field(snapshot(model), "active"), "world completion releases the game");
        Path root=Files.createTempDirectory(Path.of(System.getProperty("user.dir")),"ozl-startup-session-");
        try {
            Path folder=Files.createDirectories(root.resolve("OrangeLauncherRuntime/startup"));
            Path agent=Files.write(folder.resolve("OrangeStartup.jar"),new byte[]{1});
            Path session=folder.resolve("session.properties");
            long now=System.currentTimeMillis()/1000;
            String nonce="1234567890abcdef1234567890abcdef";
            String text="version=OZL1\nsession="+nonce+"\ncreated="+(now-1)+"\nexpires="+(now+599)+"\ngame="+b64(root.toString())+"\nuser="+b64(root.resolve("user").toString())+"\nissues="+b64(folder.resolve("issues.txt").toString())+"\ntheme=dark\nreducedMotion=false\n";
            Files.writeString(session,text);
            Class<?> sessionClass=Class.forName("ozl.startup.Session");
            Method read=sessionClass.getMethod("read",Path.class,Path.class,long.class);
            Object accepted=read.invoke(null,agent,root,now);
            check(accepted!=null,"fresh exact-game session activates the component");
            String issue="OZL1\n"+nonce+"\n"+(now*1000)+"\n"+b64("脚本执行中断")+"\n"+b64("模组调用了不存在的对象")+"\n"+b64("UnseenAuthorMod")+"\n"+b64("Register.lua:58")+"\n"+b64("检查依赖与版本")+"\n";
            Path exchange=folder.resolve("issues.txt");Files.writeString(exchange,issue);
            String[] decoded=(String[])call(accepted,"readIssue",new Class<?>[]{long.class},now*1000);
            check(decoded.length==5&&decoded[2].equals("UnseenAuthorMod"),"current-session Chinese analysis retains arbitrary mod attribution");
            Object issueModel=modelClass.getConstructor().newInstance();
            call(issueModel,"issue",new Class<?>[]{String[].class},(Object)decoded);
            check(((String[])field(snapshot(issueModel),"issue")).length==5,"accepted analysis appears in the model");
            Files.writeString(exchange,"OZL1\n"+nonce+"\n"+(now*1000)+"\n\n\n\n\n\n");
            String[] cleared=(String[])call(accepted,"readIssue",new Class<?>[]{long.class},now*1000);
            check(cleared.length==5&&cleared[0].isEmpty(),"valid empty-primary packet decodes as an explicit clear");
            call(issueModel,"issue",new Class<?>[]{String[].class},(Object)cleared);
            check(((String[])field(snapshot(issueModel),"issue")).length==0,"explicit empty-primary packet clears earlier actionable analysis");
            call(issueModel,"issue",new Class<?>[]{String[].class},(Object)decoded);
            Files.writeString(exchange,issue);
            call(issueModel,"issue",new Class<?>[]{String[].class},(Object)(String[])call(accepted,"readIssue",new Class<?>[]{long.class},now*1000+31000));
            check(((String[])field(snapshot(issueModel),"issue")).length==0,"expired exchange clears earlier actionable analysis");
            call(issueModel,"issue",new Class<?>[]{String[].class},(Object)decoded);
            Files.delete(exchange);
            call(issueModel,"issue",new Class<?>[]{String[].class},(Object)(String[])call(accepted,"readIssue",new Class<?>[]{long.class},now*1000));
            check(((String[])field(snapshot(issueModel),"issue")).length==0,"absent exchange clears earlier actionable analysis");
            Files.writeString(exchange,issue);
            check(((String[])call(accepted,"readIssue",new Class<?>[]{long.class,long.class},now*1000,now*1000)).length==0,"a later world load never replays earlier boot analysis");
            Files.writeString(exchange,issue.replace(nonce,"abcdef1234567890abcdef1234567890"));
            check(((String[])call(accepted,"readIssue",new Class<?>[]{long.class},now*1000)).length==0,"another launch cannot inject analysis into this game");
            Files.writeString(exchange,issue);
            check(((String[])call(accepted,"readIssue",new Class<?>[]{long.class},now*1000+31000)).length==0,"stale analysis is ignored");
            check(read.invoke(null,agent,root,now+601)==null,"expired sessions never activate a later game launch");
            check(read.invoke(null,agent,root.resolve("other-game"),now)==null,"a session cannot activate another game directory");
            Files.writeString(session,text.replace(b64(folder.resolve("issues.txt").toString()),b64(root.resolve("outside.txt").toString())));
            check(read.invoke(null,agent,root,now)==null,"analysis exchange cannot escape the owned component folder");
            Files.writeString(session,text.replace("expires="+(now+599),"expires="+(now+6000)));
            check(read.invoke(null,agent,root,now)==null,"unbounded session lifetime is rejected");
            Files.writeString(session,text.replace(nonce,"foreign-nonce"));
            check(read.invoke(null,agent,root,now)==null,"malformed session identity stays inactive");
            String relative=Path.of(System.getProperty("user.dir")).toAbsolutePath().relativize(root).toString();
            Files.writeString(session,text.replace(b64(root.toString()),b64(relative)));
            check(read.invoke(null,agent,root,now)==null,"relative recorded game path is rejected instead of inheriting cwd");
            Files.writeString(session,text.replace(b64(root.resolve("user").toString()),b64(root.resolve("unused/../user").toString())));
            check(read.invoke(null,agent,root,now)==null,"noncanonical user paths cannot activate the component");
        } finally {
            try(var paths=Files.walk(root)){for(Path p:paths.sorted(Comparator.reverseOrder()).toList())Files.delete(p);}
        }
        System.out.println("PASS "+checks+" startup model/session behavior checks");
    }
}
