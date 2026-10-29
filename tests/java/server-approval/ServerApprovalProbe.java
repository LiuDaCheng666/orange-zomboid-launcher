package ozl.environment.tests;

import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import me.zed_0xff.zombie_buddy.Loader;
import zombie.gameStates.ChooseGameInfo;

/** Real Buddy agent, policy, ConsoleModApprovalFrontend, hash decisions, append, and main.
 * Game metadata alone is a fixture. No frontend or allow/deny function is replaced.
 */
public final class ServerApprovalProbe {
    static void check(boolean ok, String message) { if (!ok) throw new AssertionError(message); }
    public static void main(String[] args) throws Exception {
        Path root = Paths.get(args[0]).toAbsolutePath();
        check(root.toString().contains("artifacts"), "isolated artifact directory required");
        try { Class.forName("fixture.server.Main", false, ClassLoader.getSystemClassLoader()); throw new AssertionError("local Java was loaded before server selection"); }
        catch (ClassNotFoundException expected) {}
        Path late = root.resolve("late-server/ServerJava");
        check(!Files.exists(late), "server source must not exist before agent premain");
        if (args.length > 3 && args[3].startsWith("same-id")) {
            Path local = root.resolve("context/local-pin/ServerJava");
            System.setProperty("ozl.fixture.discovery", local.toString());
            ArrayList<String> menuRoots = new ArrayList<String>();
            new zombie.ZomboidFileSystem().getAllModFolders(menuRoots);
            check(menuRoots.isEmpty(), "pre-connection discovery must not cache a local same-ID source: " + menuRoots);
            System.out.println("[PROBE] no-local-same-id-cached-before-connection");
        }
        Files.createDirectories(late);
        Files.copy(Paths.get(args[1]), late.resolve("payload.jar"));
        Files.write(late.resolve("mod.info"), ("id=ServerJava\nname=Late server Java\njavaJarFile=payload.jar\njavaPkgName=fixture.server\n").getBytes(StandardCharsets.UTF_8));
        ChooseGameInfo.register("ServerJava", late.toString());
        System.out.println("[PROBE] late-server-source-created-after-premain");
        boolean force = args.length > 3 && "same-id-force".equals(args[3]);
        if (args.length > 3 && ("client".equals(args[3]) || args[3].startsWith("same-id"))) {
            zombie.network.GameClient.client = true;
            zombie.network.GameClient.instance.serverMods.add("ServerJava");
            zombie.ZomboidFileSystem filesystem = new zombie.ZomboidFileSystem();
            if (args[3].startsWith("same-id")) {
                Path local = root.resolve("context/local-pin/ServerJava");
                check(Files.isRegularFile(local.resolve("payload.jar")), "distinct local JAR existed at premain");
                System.setProperty("ozl.fixture.discovery", local + "|" + late);
                ArrayList<String> roots = new ArrayList<String>();
                filesystem.getAllModFolders(roots);
                List<String> expectedRoots = force ? Arrays.asList(late.toString(), root.resolve("context/local-pin/ClientExtra").toString()) : Arrays.asList(late.toString());
                check(roots.equals(expectedRoots), "production Discovery.filter must retain the server's same-ID source and only forced extra source: " + roots);
                ChooseGameInfo.register("ServerJava", roots.get(0));
                if (force) ChooseGameInfo.register("ClientExtra", roots.get(1));
                System.out.println("[PROBE] actual-discovery-selected-server-over-local-pin");
            }
            filesystem.loadMods("currentGame");
            check(filesystem.loaded.equals(force ? Arrays.asList("ServerJava", "ClientExtra") : Arrays.asList("ServerJava")), "server order and all explicit local extras passed correctly through both production transformers: " + filesystem.loaded);
            System.out.println("[PROBE] production-client-loadMods-boundary");
        } else Loader.loadMods(new ArrayList<String>(Arrays.asList("ServerJava")));
        boolean expected = Boolean.parseBoolean(args[2]);
        boolean executed = Files.exists(Paths.get(System.getProperty("ozl.fixture.executed")));
        check(executed == expected, "real payload execution=" + executed + ", expected=" + expected);
        boolean visible;
        try { Class.forName("fixture.server.Main", false, ClassLoader.getSystemClassLoader()); visible = true; }
        catch (ClassNotFoundException denied) { visible = false; }
        check(visible == expected, "denied payload must never enter the system classpath");
        if (executed) {
            String marker = new String(Files.readAllBytes(Paths.get(System.getProperty("ozl.fixture.executed"))), StandardCharsets.UTF_8);
            check(!marker.contains("PREMAIN"), "late MAIN approval must not pretend premain ran");
            check(marker.contains("SERVER-") && !marker.contains("main:LOCAL"), "same-ID local JAR must not execute: " + marker);
            if (force) check(marker.equals("main:SERVER-v1\nextra:LOCAL-EXTRA\n"), "the custom forced Java must execute once after the server mod: " + marker);
        }
        System.out.println("PASS real Buddy late-server approval; executed=" + executed + "; classpath=" + visible);
    }
}
