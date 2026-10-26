package ozl.environment;

import java.io.*;
import java.nio.*;
import java.nio.charset.*;
import java.nio.file.*;
import java.security.*;
import java.util.*;

/** Hash-pinned launch contract, independent of the optional loading presentation. */
public final class EnvironmentManifest {
    public final String id;
    public final Path game,user,home,mods,bridge,snapshot;
    public final List<Path> roots;
    private EnvironmentManifest(String id,Path game,Path user,Path home,Path mods,Path bridge,List<Path> roots){
        this.id=id;this.game=game;this.user=user;this.home=home;this.mods=mods;this.bridge=bridge;this.snapshot=game.getParent();this.roots=Collections.unmodifiableList(new ArrayList<Path>(roots));
    }
    private static IllegalArgumentException invalid(String reason){return new IllegalArgumentException(reason);}
    private static byte[] expectedHash(String text){
        if(text==null||!text.matches("[a-fA-F0-9]{64}"))throw invalid("missing or invalid manifest SHA-256");
        byte[] value=new byte[32];for(int i=0;i<value.length;i++)value[i]=(byte)Integer.parseInt(text.substring(i*2,i*2+2),16);return value;
    }
    private static Path path(String encoded)throws IOException {
        try{
            byte[] bytes=Base64.getDecoder().decode(encoded);
            if(!Base64.getEncoder().encodeToString(bytes).equals(encoded)||bytes.length>32768)throw invalid("invalid encoded path");
            String value=StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes)).toString();
            if(value.length()==0||value.matches(".*[\\p{Cntrl}].*"))throw invalid("invalid path text");
            return Paths.get(value);
        }catch(IllegalArgumentException e){throw invalid("invalid encoded path");}
    }
    public static EnvironmentManifest read(Path file,String sha256,Path cwd,Path actualHome)throws IOException {
        byte[] expected=expectedHash(sha256),bytes=SafePaths.bytes(file,4*1024*1024);
        try{if(!MessageDigest.isEqual(expected,MessageDigest.getInstance("SHA-256").digest(bytes)))throw invalid("manifest SHA-256 mismatch");}catch(NoSuchAlgorithmException impossible){throw new IllegalStateException(impossible);}
        for(byte b:bytes)if((b&255)>127||(b<32&&b!='\n'&&b!='\r'))throw invalid("manifest must be plain ASCII properties");
        Map<String,String> values=new HashMap<String,String>();
        Set<String> allowed=new HashSet<String>(Arrays.asList("version","id","game","user","home","mods","roots","bridge"));
        for(String line:new String(bytes,StandardCharsets.US_ASCII).split("\\r?\\n",-1)){
            if(line.length()==0||line.startsWith("#")||line.startsWith("!"))continue;
            int equals=line.indexOf('=');if(equals<1)throw invalid("invalid manifest property");String key=line.substring(0,equals);
            if(!allowed.contains(key)||values.put(key,line.substring(equals+1))!=null)throw invalid("unknown or duplicate manifest property");
        }
        if(!values.keySet().equals(allowed)||!"OZLENV1".equals(values.get("version"))||!values.get("id").matches("[a-f0-9]{32}"))throw invalid("invalid manifest identity or version");
        Path game=SafePaths.canonical(path(values.get("game")),true,true),user=SafePaths.canonical(path(values.get("user")),true,true),home=SafePaths.canonical(path(values.get("home")),true,true),mods=SafePaths.canonical(path(values.get("mods")),true,true),bridge=path(values.get("bridge"));
        Path snapshot=game.getParent();if(snapshot==null||snapshot.getParent()==null)throw invalid("invalid snapshot root");SafePaths.canonical(snapshot,true,true);
        if(!SafePaths.canonical(cwd,true,true).equals(game))throw invalid("working directory differs from frozen game");
        if(!SafePaths.canonical(actualHome,true,true).equals(home)||!home.startsWith(snapshot)||home.equals(snapshot)||home.startsWith(game))throw invalid("user.home differs from snapshot runtime home");
        if(!mods.equals(snapshot.resolve("runtime/mods")))throw invalid("mods differs from snapshot runtime mod settings");
        if(!bridge.equals(user.resolve("mods/OrangeLauncherSession")))throw invalid("bridge differs from owned temporary path");SafePaths.canonical(bridge,false,true);
        List<Path> roots=new ArrayList<Path>();Set<Path> unique=new HashSet<Path>();String encodedRoots=values.get("roots");
        if(encodedRoots.length()>0){String[] entries=encodedRoots.split(",",-1);if(entries.length>4096)throw invalid("too many frozen mod roots");
            for(String entry:entries){Path root=SafePaths.canonical(path(entry),true,true);if(!root.startsWith(snapshot)||root.equals(snapshot)||root.equals(game)||root.startsWith(home)||!unique.add(root))throw invalid("invalid, duplicate or unfrozen mod root");SafePaths.tree(root);roots.add(root);}
        }
        EnvironmentManifest manifest=new EnvironmentManifest(values.get("id"),game,user,home,mods,bridge,roots);manifest.bridgePresent();return manifest;
    }
    /** The sole permitted live mod root is the launcher's journal-owned bridge. */
    public boolean bridgePresent()throws IOException {
        SafePaths.canonical(bridge,false,true);if(!Files.exists(bridge,LinkOption.NOFOLLOW_LINKS))return false;SafePaths.tree(bridge);
        boolean found=false;
        for(String name:new String[]{"mod.info","common/mod.info","42/mod.info"}){
            Path info=bridge.resolve(name);if(!Files.exists(info,LinkOption.NOFOLLOW_LINKS))continue;
            String content=new String(SafePaths.bytes(info,65536),StandardCharsets.UTF_8);int identities=0;
            for(String line:content.split("\\r?\\n")){line=line.trim();if(line.startsWith("id=")){if(!"OrangeLauncherSession".equals(line.substring(3).trim()))throw invalid("foreign bridge mod identity");identities++;}}
            if(identities!=1)throw invalid("missing or duplicate bridge mod identity");found=true;
        }
        if(!found)throw invalid("missing bridge mod identity");return true;
    }
    public List<String> discoveryRoots()throws IOException {
        List<String> result=new ArrayList<String>();for(Path root:roots){SafePaths.canonical(root,true,true);result.add(root.toString());}if(bridgePresent())result.add(bridge.toString());return result;
    }
}
