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
    static boolean gate(Path jar) throws Exception {
        return (Boolean) Class.forName("ozl.environment.LiveEnvironmentAgent").getMethod("denied", Path.class, String.class).invoke(null, jar, "fixture.server");
    }
    static void diagnosticScopes(Path jar) throws Exception {
        zombie.ZomboidFileSystem filesystem = new zombie.ZomboidFileSystem();
        java.io.PrintStream original = System.out;
        java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
        java.io.PrintStream capture = new java.io.PrintStream(bytes, true, "UTF-8");
        try {
            System.setOut(capture);
            zombie.network.GameClient.client = false;
            filesystem.getAllModFolders(new ArrayList<String>());
            filesystem.loadMods("diagnostic-nested");
            check(gate(jar), "nested scopes restore the outer local gate");
            try { filesystem.loadMods("diagnostic-exception"); throw new AssertionError("expected diagnostic exception"); }
            catch (IllegalStateException expected) {}
            check(gate(jar), "exception restores the local gate");
            filesystem.getAllModFolders(new ArrayList<String>());
            zombie.network.GameClient.client = true;
            zombie.network.GameClient.instance.serverMods.add("DiagnosticLua");
            filesystem.loadMods("diagnostic-single");
            check(gate(jar), "completed diagnostic scope grants no persistent Java permission");
            zombie.network.GameClient.client = false;
            zombie.network.GameClient.instance.serverMods.clear();
            filesystem.loadMods("diagnostic-empty");
        } finally { System.setOut(original); capture.close(); }
        String output = new String(bytes.toByteArray(), StandardCharsets.UTF_8);
        original.print(output);
        ArrayList<String> summaries = new ArrayList<String>();
        for (String line : output.split("\\r?\\n")) if (line.startsWith("[OZL-LIVE] load-summary")) summaries.add(line);
        check(summaries.size() == 5, "exactly one summary per String load, including nested/exception; outside discovery has none: " + summaries);
        int[] expectedCalls = {1, 2, 1, 1, 0};
        boolean[] expectedClient = {true, false, false, true, false};
        java.util.regex.Pattern pattern = java.util.regex.Pattern.compile("\\[OZL-LIVE\\] load-summary totalMs=(\\d+) discoveryCalls=(\\d+) discoveryMs=(\\d+) client=(true|false)");
        for (int i = 0; i < summaries.size(); i++) {
            java.util.regex.Matcher match = pattern.matcher(summaries.get(i));
            check(match.matches(), "only numeric timing/count and client flag are logged, no path/ID: " + summaries.get(i));
            long total = Long.parseLong(match.group(1)), discovery = Long.parseLong(match.group(3));
            check(Integer.parseInt(match.group(2)) == expectedCalls[i], "nested/exception/outside counters are isolated: " + summaries);
            check(Boolean.parseBoolean(match.group(4)) == expectedClient[i], "each scope captures its actual native client branch");
            check(total >= discovery && discovery >= 0, "elapsed filter time fits the total scope duration");
            if (i == 3) check(discovery > 0, "known20ms input iterator inside the actual filter must be included in elapsed accounting");
            if (i == 4) check(discovery == 0, "empty later scope inherits no discovery time");
        }
        System.out.println("PASS real Live load summary:5 scopes, nested/exception/outside isolation, elapsed accounting and no payload identifiers");
    }
    public static void main(String[] args) throws Exception {
        Path root = Paths.get(args[0]).toAbsolutePath();
        check(root.toString().contains("artifacts"), "isolated artifact directory required");
        try { Class.forName("fixture.server.Main", false, ClassLoader.getSystemClassLoader()); throw new AssertionError("local Java was loaded before server selection"); }
        catch (ClassNotFoundException expected) {}
        Path late = root.resolve("late-server/ServerJava");
        check(!Files.exists(late), "server source must not exist before agent premain");
        boolean manual = args.length > 3 && args[3].startsWith("manual-");
        if (args.length > 3 && args[3].contains("same-id")) {
            Path local = root.resolve("context/local-pin/ServerJava");
            System.setProperty("ozl.fixture.discovery", local.toString());
            ArrayList<String> menuRoots = new ArrayList<String>();
            new zombie.ZomboidFileSystem().getAllModFolders(menuRoots);
            check(menuRoots.equals(manual ? Arrays.asList(local.toString(), root.resolve("context/local-pin/ClientExtra").toString()) : Collections.emptyList()), "pre-connection discovery follows the actual launch selection: " + menuRoots);
            System.out.println("[PROBE] no-local-same-id-cached-before-connection");
        }
        Files.createDirectories(late);
        Files.copy(Paths.get(args[1]), late.resolve("payload.jar"));
        Files.write(late.resolve("mod.info"), ("id=ServerJava\nname=Late server Java\njavaJarFile=payload.jar\njavaPkgName=fixture.server\n").getBytes(StandardCharsets.UTF_8));
        ChooseGameInfo.register("ServerJava", late.toString());
        System.out.println("[PROBE] late-server-source-created-after-premain");
        if (args.length > 3 && "diagnostics".equals(args[3])) { diagnosticScopes(late.resolve("payload.jar")); return; }
        boolean force = args.length > 3 && "same-id-force".equals(args[3]);
        if (args.length > 3 && (manual || "client".equals(args[3]) || args[3].startsWith("same-id"))) {
            zombie.network.GameClient.client = true;
            zombie.network.GameClient.instance.serverMods.add("ServerJava");
            zombie.ZomboidFileSystem filesystem = new zombie.ZomboidFileSystem();
            if (manual) {
                check(gate(late.resolve("payload.jar")), "outside native server loadMods the normal-menu gate still denies new Java even when client=true");
                if ("manual-retry".equals(args[3])) {
                    try { filesystem.loadMods("throwBeforeServer"); throw new AssertionError("fixture must throw before Java loading"); }
                    catch (IllegalStateException expectedFailure) {}
                    check(gate(late.resolve("payload.jar")), "failed server load releases the scoped gate before retry");
                    check(!Files.exists(Paths.get(System.getProperty("ozl.fixture.executed"))), "failed load did not execute payload");
                }
            }
            if (args[3].contains("same-id")) {
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
            if (manual) {
                check(gate(late.resolve("payload.jar")), "successful server load releases the scoped gate");
                zombie.network.GameClient.client = false;
                check(gate(late.resolve("payload.jar")), "returning to menu preserves single-player Java denial");
                zombie.network.GameClient.instance.serverMods.clear();
                if (args[3].contains("same-id")) {
                    ArrayList<String> restored = new ArrayList<String>();
                    filesystem.getAllModFolders(restored);
                    check(restored.equals(Arrays.asList(root.resolve("context/local-pin/ServerJava").toString(), root.resolve("context/local-pin/ClientExtra").toString())), "returning to menu restores local source selection");
                }
            }
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
