package fixture.server;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardOpenOption;
/** Harmless sentinel: proves the real Buddy main callback executed after approval. */
public final class Main {
    public static void main(String[] args) throws Exception {
        String origin;
        try (java.io.BufferedReader input = new java.io.BufferedReader(new java.io.InputStreamReader(Main.class.getResourceAsStream("/payload-origin.txt"), StandardCharsets.UTF_8))) {
            origin = input.readLine();
        }
        Files.write(Paths.get(System.getProperty("ozl.fixture.executed")),
            ("main:" + origin + "\n").getBytes(StandardCharsets.UTF_8),
            StandardOpenOption.CREATE, StandardOpenOption.APPEND);
    }
}
