package fixture.server;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardOpenOption;
public final class PreMain {
    public static void premain(String[] args) throws Exception {
        Files.write(Paths.get(System.getProperty("ozl.fixture.executed")), "PREMAIN\n".getBytes(StandardCharsets.UTF_8),
            StandardOpenOption.CREATE, StandardOpenOption.APPEND);
    }
}
