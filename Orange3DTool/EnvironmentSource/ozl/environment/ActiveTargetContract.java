package ozl.environment;
import java.io.*;import java.nio.file.*;import java.util.*;import java.util.jar.*;import net.bytebuddy.jar.asm.*;
/** Verified local/client phase seams, separate from the frozen discovery contract. */
final class ActiveTargetContract {
 static final String FILE="zombie/modding/ActiveModsFile";
 static boolean target(String name){return TargetContract.target(name)||FILE.equals(name);}
 static void verifyJar(Path game)throws IOException{TargetContract.verifyJar(game);try(JarFile jar=new JarFile(game.resolve("projectzomboid.jar").toFile())){for(String name:new String[]{TargetContract.TARGET,TargetContract.ACTIVE,FILE}){JarEntry entry=jar.getJarEntry(name+".class");if(entry==null)throw new IOException("missing active selection contract");try(InputStream in=jar.getInputStream(entry);ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(out.size()+n>8388608)throw new IOException("oversized runtime class");out.write(b,0,n);}verify(name,out.toByteArray());}}}}
 static void verify(final String name,byte[] bytes){if(TargetContract.target(name))TargetContract.verify(name,bytes);final Set<String> methods=new HashSet<String>(),fields=new HashSet<String>();final List<String> phase=new ArrayList<String>();
  new ClassReader(bytes).accept(new ClassVisitor(Opcodes.ASM9){
   @Override public FieldVisitor visitField(int access,String field,String descriptor,String signature,Object value){if(name.equals(TargetContract.ACTIVE)&&((field.equals("id")&&descriptor.equals("Ljava/lang/String;"))||(field.equals("mods")&&descriptor.equals("Ljava/util/ArrayList;")))&&(access&(Opcodes.ACC_STATIC|Opcodes.ACC_FINAL))==Opcodes.ACC_FINAL)fields.add(field);return null;}
   @Override public MethodVisitor visitMethod(int access,String method,String descriptor,String signature,String[] exceptions){
    if((access&(Opcodes.ACC_STATIC|Opcodes.ACC_ABSTRACT|Opcodes.ACC_NATIVE))==0&&(access&Opcodes.ACC_PUBLIC)!=0)methods.add(method+descriptor);
    if(!name.equals(TargetContract.TARGET)||!method.equals("loadMods")||!descriptor.equals("(Ljava/lang/String;)V"))return null;
    return new MethodVisitor(Opcodes.ASM9){
     @Override public void visitFieldInsn(int op,String owner,String field,String desc){if(owner.equals("zombie/network/GameClient")&&field.equals("client")&&op==Opcodes.GETSTATIC&&desc.equals("Z"))phase.add("client");if(owner.equals("zombie/network/GameClient")&&field.equals("serverMods")&&op==Opcodes.GETFIELD&&desc.equals("Ljava/util/ArrayList;"))phase.add("server");}
     @Override public void visitMethodInsn(int op,String owner,String method,String desc,boolean itf){if(owner.equals(TargetContract.TARGET)&&method.equals("loadMods")&&desc.equals("(Ljava/util/List;)V"))phase.add("load");if(owner.equals(TargetContract.ACTIVE)&&method.equals("setLoadedMods"))phase.add("loaded");if(owner.equals(TargetContract.ACTIVE)&&method.equals("getMods"))phase.add("mods");}
    };
   }
  },ClassReader.SKIP_DEBUG|ClassReader.SKIP_FRAMES);
  if(name.equals(TargetContract.TARGET)&&(!methods.contains("loadMods(Ljava/lang/String;)V")||!methods.contains("loadMods(Ljava/util/List;)V")||!phase.equals(Arrays.asList("client","server","load","loaded","mods","load","loaded","mods","load"))))throw new IllegalArgumentException("unsupported local/client loading phases");
  if(name.equals(TargetContract.ACTIVE)&&(!fields.containsAll(Arrays.asList("id","mods"))||!methods.contains("copyFrom(Lzombie/modding/ActiveMods;)V")))throw new IllegalArgumentException("unsupported active selection copy");
  if(name.equals(FILE)&&(!methods.contains("read(Ljava/lang/String;Lzombie/modding/ActiveMods;)Z")||!methods.contains("write(Ljava/lang/String;Lzombie/modding/ActiveMods;)Z")))throw new IllegalArgumentException("unsupported save mod metadata I/O");
 }
}
