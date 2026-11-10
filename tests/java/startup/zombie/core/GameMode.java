package zombie.core;

/** Test API shape only; never packaged in the launcher component. */
public final class GameMode {
    private final String id;
    public boolean failText;
    public GameMode(String id){this.id=id;}
    @Override public String toString(){if(failText)throw new IllegalStateException("mode not ready");return id;}
}
