import java.lang.reflect.*;
import java.nio.file.*;
import java.util.*;

/** Characterizes the installed Buddy API with an isolated config, never the player's preferences. */
public final class BuddyOrderProbe {
    private static Object call(Class<?> owner, String name, Class<?>[] types, Object receiver, Object... args) throws Exception {
        Method method=owner.getDeclaredMethod(name,types);method.setAccessible(true);return method.invoke(receiver,args);
    }
    public static void main(String[] args) throws Exception {
        Path root=Path.of(args[0]).toAbsolutePath().normalize();
        if(!root.toString().contains("artifacts"))throw new IllegalArgumentException("isolated artifact root required");
        Files.createDirectories(root);System.setProperty("user.home",root.toString());
        Class<?> agent=Class.forName("me.zed_0xff.zombie_buddy.Agent");
        Field arguments=agent.getDeclaredField("arguments");arguments.setAccessible(true);
        @SuppressWarnings("unchecked") Map<String,String> options=(Map<String,String>)arguments.get(null);options.put("config_dir",root.toString());
        Class<?> loader=Class.forName("me.zed_0xff.zombie_buddy.Loader");
        call(loader,"setPolicy",new Class<?>[]{String.class},null,"deny-new");
        Class<?> config=Class.forName("me.zed_0xff.zombie_buddy.Config");
        Constructor<?> constructor=config.getDeclaredConstructor();constructor.setAccessible(true);
        Object enabled=call(config,"withAutoFixModOrder",new Class<?>[]{boolean.class},constructor.newInstance(),true);
        Field state=loader.getDeclaredField("g_config");state.setAccessible(true);state.set(null,enabled);
        List<String> requested=List.of("NeatUI_Framework","ZombieBuddy","Viewpoint");
        List<String> pinned=new ArrayList<>(requested);
        call(loader,"autoFixModOrder",new Class<?>[]{List.class},null,pinned);
        if(!pinned.equals(List.of("ZombieBuddy","NeatUI_Framework","Viewpoint")))throw new AssertionError("installed original pinning assumption changed: "+pinned);
        Path approvals=root.resolve("mod_approvals.json");byte[] sentinel="isolated approval sentinel".getBytes(java.nio.charset.StandardCharsets.UTF_8);Files.write(approvals,sentinel);
        Class<?> api=Class.forName("me.zed_0xff.zombie_buddy.ZombieBuddy");
        call(api,"setAutoFixModOrder",new Class<?>[]{boolean.class},null,false);
        Object disabled=state.get(null);
        if((Boolean)call(config,"auto_fix_mod_order",new Class<?>[0],disabled))throw new AssertionError("original API left the order policy enabled");
        String persisted=Files.readString(root.resolve("config.json"));
        if(!persisted.matches("(?s).*\\\"auto_fix_mod_order\\\"\\s*:\\s*false.*"))throw new AssertionError("preference not persisted by original API");
        if(!Arrays.equals(sentinel,Files.readAllBytes(approvals)))throw new AssertionError("approval data changed");
        System.out.println("PASS: installed Buddy pins the server fixture differently; original API disables and persists only order policy in isolated config");
    }
}
