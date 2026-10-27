package ozl.startup;

import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.io.*;
import java.util.*;

/** A short-lived launcher receipt, never a source of executable code. */
public final class Session {
    public final String nonce, theme;
    public final boolean reducedMotion;
    public final Path folder, issues;
    private final Path user;
    private Session(String nonce,String theme,boolean reducedMotion,Path folder,Path issues,Path user) {
        this.nonce=nonce;this.theme=theme;this.reducedMotion=reducedMotion;this.folder=folder;this.issues=issues;this.user=user;
    }
    private static String decode(Properties p,String key) {
        return new String(Base64.getDecoder().decode(p.getProperty(key,"")),StandardCharsets.UTF_8);
    }
    private static boolean canonical(Path path)throws IOException {
        if(!path.isAbsolute()||!path.normalize().equals(path))return false;
        for(Path part=path;part!=null;part=part.getParent()){
            if(Files.isSymbolicLink(part))return false;
            if(Files.exists(part,LinkOption.NOFOLLOW_LINKS)&&!part.toRealPath().equals(part))return false;
        }
        return true;
    }
    public static Session read(Path agent,Path workingDirectory,long nowSeconds) {
        try {
            Path fullAgent=agent.toAbsolutePath().normalize(), game=workingDirectory.toAbsolutePath().normalize();
            if(!fullAgent.toRealPath().equals(fullAgent)||!game.toRealPath().equals(game))return null;
            String ownedContext=System.getProperty("ozl.startup.context","");
            boolean owned=!ownedContext.isEmpty();
            Path context=owned?Path.of(ownedContext):null;
            Path folder=owned?fullAgent.getParent():game.resolve("OrangeLauncherRuntime/startup");
            if(owned&&(!canonical(context)||context.startsWith(game)||context.equals(game)||!folder.getFileName().toString().equals("startup")||!folder.getParent().getFileName().toString().matches("live-[a-f0-9]{12}")||!folder.getParent().getParent().equals(context.resolve("runtime"))))return null;
            if(!folder.resolve("OrangeStartup.jar").equals(fullAgent))return null;
            Path file=folder.resolve("session.properties");
            if(!file.toRealPath().equals(file)||Files.size(file)>8192)return null;
            Properties p=new Properties();
            try(Reader reader=Files.newBufferedReader(file,StandardCharsets.UTF_8)){p.load(reader);}
            String nonce=p.getProperty("session",""),theme=p.getProperty("theme","");
            long created=Long.parseLong(p.getProperty("created","")),expires=Long.parseLong(p.getProperty("expires",""));
            if(!(owned?"OZLOWN1":"OZL1").equals(p.getProperty("version"))||!nonce.matches("[a-f0-9]{32}"))return null;
            if(created>nowSeconds+5||expires<=nowSeconds||expires<=created||expires-created>600)return null;
            if(!"dark".equals(theme)&&!"light".equals(theme))return null;
            String reduced=p.getProperty("reducedMotion","");
            if(!"true".equals(reduced)&&!"false".equals(reduced))return null;
            Path recordedGame=Path.of(decode(p,"game"));
            Path user=Path.of(decode(p,"user"));
            if(owned&&(!Path.of(decode(p,"context")).equals(context)||context.startsWith(user)))return null;
            Path issues=Path.of(decode(p,"issues"));
            if(!canonical(recordedGame)||!recordedGame.equals(game)||!canonical(user)||!canonical(issues))return null;
            if(!issues.equals(folder.resolve("issues.txt")))return null;
            if(Files.exists(issues)&&!issues.toRealPath().equals(issues))return null;
            return new Session(nonce,theme,Boolean.parseBoolean(reduced),folder,issues,user);
        } catch(Exception invalid){return null;}
    }
    /** Intent only. The existing Lua bridge alone consumes and executes the request.
     * Stop at the endpoint header; usernames and passwords are never inspected. */
    public boolean pendingDirectServer(long nowSeconds){
        try{
            Path request=user.resolve("Lua/OrangeLauncherSession.request.txt");
            if(!canonical(request)||!Files.isRegularFile(request)||Files.size(request)>16384)return false;
            String[] header=new String[8];
            try(Reader reader=Files.newBufferedReader(request,StandardCharsets.UTF_8)){
                for(int i=0;i<header.length;i++){
                    StringBuilder line=new StringBuilder();int ch;
                    while((ch=reader.read())!=-1&&ch!='\n'){if(line.length()>=1024)return false;line.append((char)ch);}
                    if(ch==-1)return false;header[i]=line.toString().replaceFirst("\r$","");
                }
            }
            if(!"ORANGE1".equals(header[0])||!nonce.equals(header[1])||!"server".equals(header[3]))return false;
            long timestamp=Long.parseLong(header[2]);int port=Integer.parseInt(header[7]);
            return timestamp<=nowSeconds&&nowSeconds-timestamp<=600&&port>=1&&port<=65535&&!header[6].isBlank()&&header[6].length()<=256&&!header[6].matches(".*[\\p{Cntrl}].*");
        }catch(Exception unavailable){return false;}
    }
    public String[] readIssue(long nowMillis) {return readIssue(nowMillis,0);}
    public String[] readIssue(long nowMillis,long newerThan) {
        try {
            if(!Files.isRegularFile(issues)||Files.size(issues)>16384||!issues.toRealPath().equals(issues))return new String[0];
            byte[] bytes;try(InputStream input=Files.newInputStream(issues)){bytes=input.readNBytes(16385);}
            if(bytes.length>16384)return new String[0];
            List<String> lines=new String(bytes,StandardCharsets.UTF_8).lines().toList();
            if(lines.size()!=8||!"OZL1".equals(lines.get(0))||!nonce.equals(lines.get(1)))return new String[0];
            long timestamp=Long.parseLong(lines.get(2));
            if(timestamp<=newerThan||timestamp>nowMillis+5000||nowMillis-timestamp>30000)return new String[0];
            String[] value=new String[5];
            for(int i=0;i<5;i++){
                value[i]=new String(Base64.getDecoder().decode(lines.get(i+3)),StandardCharsets.UTF_8);
                if(value[i].length()>2048)return new String[0];
                value[i]=LoadingModel.safe(value[i],512);
            }
            return value;
        } catch(Exception invalid){return new String[0];}
    }
}
