package zombie.modding;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
public final class ActiveModsFile {
 public boolean read(String file,ActiveMods value){try{value.source=new String(Files.readAllBytes(Paths.get(file)),StandardCharsets.UTF_8);return true;}catch(java.io.IOException e){throw new IllegalStateException(e);}}
 public boolean write(String file,ActiveMods value){try{Files.write(Paths.get(file),value.source.getBytes(StandardCharsets.UTF_8));return true;}catch(java.io.IOException e){throw new IllegalStateException(e);}}
}
