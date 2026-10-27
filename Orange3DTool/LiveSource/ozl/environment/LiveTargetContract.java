package ozl.environment;
import java.io.*;import java.nio.file.*;
final class LiveTargetContract {
 static boolean target(String name){return ActiveTargetContract.target(name);}
 static void verifyJar(Path game)throws IOException{ActiveTargetContract.verifyJar(game);}
 static void verify(String name,byte[] bytes){ActiveTargetContract.verify(name,bytes);}
}
