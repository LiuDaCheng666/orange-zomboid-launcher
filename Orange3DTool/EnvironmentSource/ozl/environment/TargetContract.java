package ozl.environment;

import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.jar.*;
import net.bytebuddy.jar.asm.*;

/** Explicit equivalent APIs only; inspect bytes without initializing game classes. */
final class TargetContract {
    static final String TARGET="zombie/ZomboidFileSystem";
    private static final Map<String,String> METHODS=new LinkedHashMap<String,String>();
    private static final Map<String,Integer> CALLS=new HashMap<String,Integer>();
    static {
        METHODS.put("getAllModFolders","(Ljava/util/List;)V");
        METHODS.put("readDefaultModsTxt","()Lzombie/modding/ActiveMods;");
        METHODS.put("readLoadedDotTxt","()Ljava/util/ArrayList;");
        METHODS.put("saveModsFile","()V");
        CALLS.put("readDefaultModsTxt",1);CALLS.put("readLoadedDotTxt",1);CALLS.put("saveModsFile",2);
    }
    private TargetContract() {}
    static void verifyJar(Path game)throws IOException {
        Path file=SafePaths.canonical(game.resolve("projectzomboid.jar"),true,false);
        try(JarFile jar=new JarFile(file.toFile())){
            JarEntry entry=jar.getJarEntry(TARGET+".class");if(entry==null)throw new IOException("required mod discovery class missing");
            try(InputStream in=jar.getInputStream(entry);ByteArrayOutputStream out=new ByteArrayOutputStream()){
                byte[] buffer=new byte[8192];int n,total=0;while((n=in.read(buffer))!=-1){total+=n;if(total>8*1024*1024)throw new IOException("oversized discovery class");out.write(buffer,0,n);}verify(out.toByteArray());
            }
        }
    }
    static void verify(byte[] bytes){
        final Set<String> seen=new HashSet<String>();final Map<String,Integer> observed=new HashMap<String,Integer>();
        ClassReader reader=new ClassReader(bytes);if(!TARGET.equals(reader.getClassName()))throw new IllegalArgumentException("wrong discovery class");
        reader.accept(new ClassVisitor(Opcodes.ASM9){
            @Override public MethodVisitor visitMethod(int access,final String name,String descriptor,String signature,String[] exceptions){
                if(!METHODS.containsKey(name))return null;
                if(!METHODS.get(name).equals(descriptor)||!seen.add(name)||(access&(Opcodes.ACC_STATIC|Opcodes.ACC_ABSTRACT|Opcodes.ACC_NATIVE))!=0||name.equals("getAllModFolders")&&(access&Opcodes.ACC_PUBLIC)==0)throw new IllegalArgumentException("unsupported mod discovery method signature");
                if(!CALLS.containsKey(name))return null;observed.put(name,0);
                return new MethodVisitor(Opcodes.ASM9){
                    @Override public void visitMethodInsn(int opcode,String owner,String method,String desc,boolean itf){
                        if(owner.equals("zombie/core/Core")&&method.equals("getMyDocumentFolder")){
                            if(opcode!=Opcodes.INVOKESTATIC||!desc.equals("()Ljava/lang/String;"))throw new IllegalArgumentException("unsupported mod settings path call");
                            observed.put(name,observed.get(name)+1);
                        }
                    }
                };
            }
        },ClassReader.SKIP_DEBUG|ClassReader.SKIP_FRAMES);
        if(!seen.equals(METHODS.keySet())||!observed.equals(CALLS))throw new IllegalArgumentException("unsupported mod settings bytecode contract");
    }
}
