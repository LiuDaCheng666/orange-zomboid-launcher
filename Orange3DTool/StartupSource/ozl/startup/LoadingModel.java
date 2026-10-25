package ozl.startup;

import java.util.*;
import java.util.regex.Pattern;

/** Synchronized observations. Animation time never creates progress or log events. */
public final class LoadingModel {
    private boolean boot=true, world, waiting, disabled;
    private String title="正在启动游戏", detail="", stage="准备";
    private final ArrayDeque<String> activity=new ArrayDeque<>();
    private final Set<String> mods=new HashSet<>(),scripts=new HashSet<>();
    private String[] issue=new String[0];
    private int stageIndex;
    private long began=System.nanoTime();
    private static final Pattern CREDENTIAL=Pattern.compile("(?i)(password|access_token|token|secret)\\s*[:=]\\s*[^\\s,;]+" );
    public static String safe(String value,int limit) {
        String clean=CREDENTIAL.matcher(value==null?"":value).replaceAll("$1=•••").replaceAll("[\\r\\n\\t\\p{Cntrl}]"," ").trim();
        return clean.length()>limit?clean.substring(0,limit-1)+"…":clean;
    }
    private void append(String value) {
        value=safe(value,180);
        if(value.isEmpty()||value.equals(activity.peekLast()))return;
        activity.addLast(value);while(activity.size()>5)activity.removeFirst();
    }
    public synchronized void phase(String text) {
        if(disabled||(!boot&&!world))return;
        String value=safe(text,160),lower=value.toLowerCase(Locale.ROOT);
        if(lower.contains("waiting for java mods approval")){waiting=true;return;}
        String next=null;int index=stageIndex;
        if(lower.equals("loading mods")){next="模组";index=1;}
        else if(lower.contains("texture pack")||lower.contains("models and animations")||lower.contains("clothing")){next="资源";index=2;}
        else if(lower.contains("loading scripts")){next="脚本";index=3;}
        else if(lower.contains("loading lua")||lower.contains("reloading lua")||lower.contains("载入lua")||lower.contains("加载lua")){next="界面";index=4;}
        else if(lower.equals("game booting")){next="就绪";index=5;}
        else if(lower.contains("translation")){next="语言";index=2;}
        if(next!=null){waiting=false;stage=next;stageIndex=index;title="正在加载"+next;detail="";append(title);}
    }
    public synchronized void mod(String id) {
        if(disabled||(!boot&&!world))return;
        String name=safe(id,120);if(name.isEmpty())return;
        if(mods.size()<16384)mods.add(name);
        detail=name;append("模组  ·  "+name);
    }
    public synchronized void script(String path) {
        if(disabled||waiting||(!boot&&!world))return;
        String normalized=(path==null?"":path).replace('\\','/');
        if(!normalized.toLowerCase(Locale.ROOT).endsWith(".lua"))return;
        String name=normalized.substring(normalized.lastIndexOf('/')+1),owner="";
        int at=normalized.indexOf("/mods/");
        if(at>=0){String tail=normalized.substring(at+6);int slash=tail.indexOf('/');if(slash>0)owner=tail.substring(0,slash);}
        if(scripts.size()<65536)scripts.add(normalized);
        detail=safe((owner.isEmpty()?"":owner+" / ")+name,120);
        append("脚本  ·  "+detail);
    }
    public synchronized void menuReady(){boot=false;world=false;waiting=false;activity.clear();issue=new String[0];}
    public synchronized void worldBegin(){if(disabled)return;boot=false;world=true;waiting=false;stage="世界";stageIndex=1;title="正在加载世界";detail="";mods.clear();scripts.clear();activity.clear();issue=new String[0];began=System.nanoTime();append(title);}
    public synchronized void worldEnd(){world=false;waiting=false;issue=new String[0];}
    public synchronized void waitingForInput(){waiting=true;}
    public synchronized void worldStatus(String value){if(world&&!waiting&&!disabled&&!value.isBlank()){detail=safe(value,140);append(detail);}}
    public synchronized void issue(String[] value){if(value!=null&&value.length==5&&!value[0].isBlank())issue=value.clone();}
    public synchronized void disable(){disabled=true;}
    public synchronized Snapshot snapshot(){return new Snapshot((boot||world)&&!waiting&&!disabled,world,title,detail,stage,stageIndex,mods.size(),scripts.size(),activity.toArray(String[]::new),issue.clone(),(System.nanoTime()-began)/1e9);}
    public static final class Snapshot {
        public final boolean active,world;
        public final String title,detail,stage;
        public final int stageIndex,processedMods,processedScripts;
        public final String[] activity,issue;
        public final double elapsed;
        Snapshot(boolean active,boolean world,String title,String detail,String stage,int index,int mods,int scripts,String[] activity,String[] issue,double elapsed){this.active=active;this.world=world;this.title=title;this.detail=detail;this.stage=stage;stageIndex=index;processedMods=mods;processedScripts=scripts;this.activity=activity;this.issue=issue;this.elapsed=elapsed;}
    }
}
