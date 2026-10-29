package fixture.extra;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
/** A custom Java component outside the launcher's known quick-install list. */
public final class Main {
    public static void main(String[] args) throws Exception {
        Files.write(Paths.get(System.getProperty("ozl.fixture.executed")), "extra:LOCAL-EXTRA\n".getBytes(StandardCharsets.UTF_8), StandardOpenOption.CREATE, StandardOpenOption.APPEND);
    }
}
