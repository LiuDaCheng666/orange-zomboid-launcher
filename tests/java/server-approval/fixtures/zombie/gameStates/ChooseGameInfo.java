package zombie.gameStates;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.Map;

/** Only the game metadata boundary is simulated; Buddy approval/loading is the pinned release JAR. */
public final class ChooseGameInfo {
    private static final Map<String, Mod> mods = new LinkedHashMap<String, Mod>();
    public static Mod getAvailableModDetails(String id) { return mods.get(id); }
    public static void register(String id, String directory) { mods.put(id, new Mod(directory)); }
    public static final class Mod {
        private ArrayList<String> require = new ArrayList<String>();
        private final String directory;
        public Mod(String directory) { this.directory = directory; }
        public String getDir() { return directory; }
        public String getVersionDir() { return directory; }
        public String getCommonDir() { return directory; }
        public ArrayList<String> getRequire() { return require; }
        public void setRequire(ArrayList<String> value) { require = value; }
    }
}
