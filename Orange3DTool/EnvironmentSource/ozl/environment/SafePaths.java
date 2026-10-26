package ozl.environment;

import java.io.*;
import java.nio.file.*;
import java.nio.file.attribute.BasicFileAttributes;

/** Filesystem checks shared by manifest binding and mandatory discovery guards. */
final class SafePaths {
    private SafePaths() {}
    static Path canonical(Path path,boolean required,boolean directory)throws IOException {
        if(path==null||!path.isAbsolute()||!path.normalize().equals(path))throw new IOException("noncanonical path");
        for(Path current=path;current!=null;current=current.getParent()){
            if(Files.exists(current,LinkOption.NOFOLLOW_LINKS)){
                BasicFileAttributes attrs=Files.readAttributes(current,BasicFileAttributes.class,LinkOption.NOFOLLOW_LINKS);
                if(attrs.isSymbolicLink()||attrs.isOther()||!current.toRealPath().equals(current))throw new IOException("linked path");
            }
        }
        if(required&&!(directory?Files.isDirectory(path,LinkOption.NOFOLLOW_LINKS):Files.isRegularFile(path,LinkOption.NOFOLLOW_LINKS)))throw new IOException("missing required path");
        return path;
    }
    static void tree(Path root)throws IOException {
        canonical(root,true,true);
        Files.walkFileTree(root,new SimpleFileVisitor<Path>(){
            private void verify(Path path,BasicFileAttributes attrs)throws IOException{
                if(attrs.isSymbolicLink()||attrs.isOther()||!path.toRealPath().equals(path))throw new IOException("linked mod content");
            }
            @Override public FileVisitResult preVisitDirectory(Path dir,BasicFileAttributes attrs)throws IOException{verify(dir,attrs);return FileVisitResult.CONTINUE;}
            @Override public FileVisitResult visitFile(Path file,BasicFileAttributes attrs)throws IOException{verify(file,attrs);return FileVisitResult.CONTINUE;}
        });
    }
    static byte[] bytes(Path file,int limit)throws IOException {
        canonical(file,true,false);if(Files.size(file)>limit)throw new IOException("oversized required file");
        try(InputStream in=Files.newInputStream(file);ByteArrayOutputStream out=new ByteArrayOutputStream()){
            byte[] block=new byte[8192];int n,total=0;
            while((n=in.read(block))!=-1){total+=n;if(total>limit)throw new IOException("oversized required file");out.write(block,0,n);}return out.toByteArray();
        }
    }
}
