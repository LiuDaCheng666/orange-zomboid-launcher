package zombie;
public final class ZomboidFileSystem {
    public int count;
    public void loadMod(String id){if("throws".equals(id))throw new IllegalArgumentException("original failure");count++;}
    public int indexed,scanned;
    public void init(){indexed++;}
    public void searchFolders(java.io.File file){scanned++;}
}
