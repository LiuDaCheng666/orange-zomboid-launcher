package ozl.startup;

import java.util.*;
import java.util.regex.Pattern;
import java.io.File;

/** Synchronized observations. Animation time never creates progress or log events. */
public final class LoadingModel {
    public enum Context { BOOT, SAVE, SERVER, NONE }
    public enum Route { FULL, BEHIND_UI, NONE }
    private boolean boot=true, world, waiting, disabled,nativeInteraction;
    private Context context=Context.BOOT;
    private Route route=Route.FULL;
    private long epoch=1,scannedResources,lastResourceLabel;
    private String name="",mode="",map="";
    private String lastWorldStatus="",lastConversion="";
    private int workshopProjects=-1,requestedMods=-1,queuePosition=-1;
    private long downloaded=-1,downloadTotal=-1;
    private String downloadPhase="download";
    private boolean downloadPage;private String downloadID="",downloadTitle="";private int workshopReady=-1;private double downloadRate=-1;
    private long generation=1;
    private boolean serverOperation;
    private ServerInfo server=new ServerInfo();
    private static final class ServerInfo {
        String name="",host="",map="",clock="",date="";int port,online=-1,maximum=-1,mods=-1,projects=-1,populationRank,maxRank,mapRank,timeRank;long day=-1;boolean clockObserved;
        String endpoint(){return host.isEmpty()||port<1?"":(host.contains(":")?"["+host+"]":host)+":"+port;}
    }
    private void clearServer(){generation++;serverOperation=false;server=new ServerInfo();}
    private boolean currentServer(long operation){return serverOperation&&operation==generation;}
    public synchronized void endpoint(long operation,String host,int port){if(!currentServer(operation)||host==null||host.length()>253||port<1||port>65535)return;String clean=safe(host,254);if(!clean.matches("[a-zA-Z0-9._:%\\[\\]-]+"))return;server.host=clean.startsWith("[")&&clean.endsWith("]")?clean.substring(1,clean.length()-1):clean;server.port=port;}
    public synchronized void publicInfo(long operation,String publicName,String publicMap,int players,int maximum){
        if(!currentServer(operation))return;if(server.name.isEmpty()){server.name=safe(publicName,96);if(context==Context.SERVER)name=server.name;}
        if(server.mapRank<1&&publicMap!=null&&!publicMap.isBlank()){server.map=safe(publicMap,120);server.mapRank=1;if(context==Context.SERVER)map=server.map;}
        population(players,maximum,1);
    }
    private void population(int players,int maximum,int rank){if(players>=0&&maximum>0&&players<=maximum&&maximum<=65535){if(rank>=server.populationRank){server.online=players;server.populationRank=rank;}if(rank>=server.maxRank){server.maximum=maximum;server.maxRank=rank;}}}
    public synchronized void maximum(long operation,int maximum){if(currentServer(operation)&&maximum>0&&maximum<=65535&&server.maxRank<=4){server.maximum=maximum;server.maxRank=4;}}
    private void clock(int year,int month,int day,int hour,int minute,int rank){
        if(rank<server.timeRank||hour<0||hour>23||minute<0||minute>59||year<1||year>9999)return;
        try{java.time.LocalDate date=java.time.LocalDate.of(year,month,day);server.date=date.toString();server.clock=String.format(Locale.ROOT,"%02d:%02d",hour,minute);server.timeRank=rank;}catch(java.time.DateTimeException invalid){}
    }
    public synchronized void queueInformation(long operation,int players,int maximum,int year,int month,int day,int hour,int minute){if(!currentServer(operation))return;population(players,maximum,3);clock(year,month,day,hour,minute,1);}
    public synchronized void serverClock(long operation,int year,int month,int day,int hour,int minute,double worldHours){if(!currentServer(operation))return;clock(year,month+1,day+1,hour,minute,2);server.clockObserved=true;if(Double.isFinite(worldHours)&&worldHours>=0&&worldHours<24d*1_000_000_000L)server.day=(long)Math.floor(worldHours/24)+1;}
    private String title="正在启动游戏", detail="", stage="准备";
    private final ArrayDeque<String> activity=new ArrayDeque<>();
    private final Set<String> mods=new HashSet<>(),scripts=new HashSet<>();
    private final LinkedHashMap<String,Integer> translatedPhases=new LinkedHashMap<>();
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
    public synchronized void early(String operation){
        if(!boot||disabled||waiting)return;
        String label=switch(operation){case "mainThreadInit" -> "正在初始化游戏";case "index" -> "正在索引游戏资源";case "fonts" -> "正在准备字体";case "init" -> "正在准备平台与资源";default -> "";};
        if(label.isEmpty())return;title=label;stage="初始化";stageIndex=0;detail="";append(label);
    }
    /** File.getName is lexical. No stat, canonicalization, or path set on the hot scan. */
    public synchronized void resource(File file,long now){
        if(!boot||disabled||waiting)return;
        if(scannedResources<Long.MAX_VALUE)scannedResources++;
        if(file==null||(lastResourceLabel!=0&&now-lastResourceLabel<250_000_000L))return;
        lastResourceLabel=now;detail=safe(file.getName(),100);if(!detail.isEmpty())append("资源  ·  "+detail);
    }
    /** Only actual game loading keys may associate a resolved label with a phase.
     * Dynamic texture-pack names stay exact, with bounded label count and length. */
    public synchronized void translation(String key,String label){
        int index=switch(key==null?"":key){
            case "UI_Loading_Scripts" -> 3;
            case "UI_Loading_Clothing","UI_Loading_ModelsAnimations","UI_Loading_Texturepack" -> 2;
            case "UI_Loading_OnGameBoot" -> 5;
            case "UI_Loading_Lua" -> 4;
            default -> -1;
        };
        if(index<0||label==null||label.isBlank()||label.length()>2048)return;
        translatedPhases.remove(label);translatedPhases.put(label,index);
        while(translatedPhases.size()>64)translatedPhases.remove(translatedPhases.keySet().iterator().next());
    }
    public synchronized void phase(String text) {
        if(disabled||(!boot&&!world))return;
        String value=safe(text,160),lower=value.toLowerCase(Locale.ROOT);
        if(lower.contains("waiting for java mods approval")){waiting=true;return;}
        String next=null;int index=stageIndex;
        Integer translated=translatedPhases.get(text);
        if(translated!=null){index=translated;next=switch(index){case 2 -> "资源";case 3 -> "脚本";case 4 -> "界面";case 5 -> "就绪";default -> null;};}
        else if(lower.equals("loading mods")){next="模组";index=1;}
        else if(lower.contains("texture pack")||lower.contains("models and animations")||lower.contains("clothing")){next="资源";index=2;}
        else if(lower.contains("loading scripts")){next="脚本";index=3;}
        else if(lower.contains("loading lua")||lower.contains("reloading lua")||lower.contains("载入lua")||lower.contains("加载lua")){next="界面";index=4;}
        else if(lower.equals("game booting")){next="就绪";index=5;}
        else if(lower.contains("translation")){next="语言";index=2;}
        if(next!=null){waiting=false;stage=next;if(boot)stageIndex=index;title="正在加载"+next;detail="";append(title);}
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
    private void reset(Context next,Route target){lastNativeStatus="";downloadPage=false;downloadID=downloadTitle="";workshopReady=-1;downloadRate=-1;epoch++;boot=next==Context.BOOT;world=false;waiting=false;nativeInteraction=false;context=next;route=target;name="";mode="";map="";detail="";lastWorldStatus=lastConversion="";stageIndex=0;scannedResources=0;lastResourceLabel=0;workshopProjects=requestedMods=queuePosition=-1;downloaded=downloadTotal=-1;mods.clear();scripts.clear();activity.clear();issue=new String[0];began=System.nanoTime();}
    public synchronized void nativeInteraction(boolean value){if(serverOperation&&context==Context.SERVER&&route==Route.BEHIND_UI&&nativeInteraction!=value){nativeInteraction=value;epoch++;}}
    public synchronized void menuReady(){reset(Context.NONE,Route.NONE);clearServer();}
    public synchronized void serverBegin(String friendly){serverBegin(friendly,"",0);}
    public synchronized void serverBegin(String friendly,String host,int port){if(disabled)return;clearServer();serverOperation=true;reset(Context.SERVER,Route.BEHIND_UI);name=safe(friendly,96);server.name=name;endpoint(generation,host,port);title="正在连接服务器";stage="连接";append(title);}
    public synchronized void serverResume(){if(disabled)return;if(context==Context.SERVER&&route==Route.BEHIND_UI)return;if(!serverOperation){clearServer();serverOperation=true;}reset(Context.SERVER,Route.BEHIND_UI);name=server.name;map=server.map;requestedMods=server.mods;workshopProjects=server.projects;title="正在连接服务器";stage="连接";append(title);}
    private String lastNativeStatus="";
    public synchronized void nativeStatus(long operation,String label){if(operation!=generation||disabled||nativeInteraction||context!=Context.SERVER||route!=Route.BEHIND_UI||label.isEmpty()||label.equals(lastNativeStatus))return;lastNativeStatus=label;detail=label;append(label);}
    public synchronized void serverStage(String actual,int projects,int modsCount){
        if(disabled||context!=Context.SERVER||route!=Route.BEHIND_UI)return;
        String label=switch(actual==null?"":actual){case "Start","TestTCP" -> "正在连接服务器";case "WorkshopInit","WorkshopQuery" -> "正在检查创意工坊项目";case "WorkshopConfirm","ServerWorkshopItemScreen" -> "创意工坊项目确认";case "WorkshopUpdate" -> "正在同步创意工坊项目";case "CheckMods" -> "正在检查服务器模组";case "Finish" -> "正在接收连接信息";case "Loading" -> "正在同步服务器数据";default -> "";};
        if(projects>=0){workshopProjects=projects;server.projects=projects;}if(modsCount>=0){requestedMods=modsCount;server.mods=modsCount;}
        if(!label.isEmpty()&&!label.equals(title)){title=label;stage="同步";stageIndex=switch(actual){case "Start","TestTCP" -> 0;case "WorkshopInit","WorkshopQuery","WorkshopConfirm","ServerWorkshopItemScreen","WorkshopUpdate" -> 1;case "CheckMods" -> 2;case "Finish","Loading" -> 3;default -> stageIndex;};detail="";append(label);}
    }
    public synchronized void serverStats(String actualMap,int modsCount){if(context!=Context.SERVER||disabled)return;if(actualMap!=null&&!actualMap.isBlank()){map=safe(actualMap,120);server.map=map;server.mapRank=2;}if(modsCount>=0){requestedMods=modsCount;server.mods=modsCount;}}
    public synchronized void workshop(int projects,long bytes,long total){if(context!=Context.SERVER||route!=Route.BEHIND_UI||disabled)return;if(projects>=0){workshopProjects=projects;server.projects=projects;}if(bytes>=0&&total>0&&bytes<=total){downloaded=bytes;downloadTotal=total;}}
    public synchronized void downloadPhase(String phase){downloadPhase=phase;}
    public synchronized void download(boolean active,String id,String title,int ready,long bytes,long total,double rate){if(disabled)return;if(downloadPage!=active){downloadPage=active;epoch++;}downloadID=id;downloadTitle=title;workshopReady=ready;downloaded=bytes;downloadTotal=total;downloadRate=rate;if(active)nativeInteraction=false;}
    public synchronized void queueWaiting(){if(context!=Context.SERVER||disabled)return;title="正在排队连接";stage="排队";detail="Esc / 手柄 A 取消排队";}
    public synchronized void queue(int position){if(context!=Context.SERVER||disabled||position<0)return;route=Route.BEHIND_UI;queuePosition=position;title="正在排队连接";stage="排队";detail="排队位置  "+position+"  ·  Esc / 手柄 A 取消排队";append(detail);}
    /** Retain only an explicitly owned friendly identity for the next real world entry. */
    public synchronized void handoff(boolean keepServer){reset(Context.NONE,Route.NONE);if(!keepServer)clearServer();}
    public synchronized void worldBegin(){worldBegin(false,"","","");}
    public synchronized void worldBegin(boolean server,String save,String actualMode,String actualMap){
        if(disabled)return;String friendly=server?this.server.name:"";if(!server)clearServer();else if(!serverOperation){clearServer();serverOperation=true;}
        int knownMods=server?this.server.mods:-1;reset(server?Context.SERVER:Context.SAVE,Route.FULL);world=true;
        name=server?friendly:basename(save);mode=server?"":safe(actualMode,80);map=safe(actualMap,120);requestedMods=knownMods;
        if(server){workshopProjects=this.server.projects;if(map.isEmpty())map=this.server.map;else{this.server.map=map;this.server.mapRank=2;}}
        stage="世界";stageIndex=server?4:1;title=server?"正在加载服务器世界":"正在载入存档";append(title);
    }
    private static String basename(String path){String value=path==null?"":path.replace('\\','/');int at=value.lastIndexOf('/');return safe(value.substring(at+1),96);}
    public synchronized void conversion(boolean converting,int count,int total){if(world&&!waiting&&converting&&count>=0&&total>0&&count<=total){detail="正在转换世界  "+count+" / "+total;if(!detail.equals(lastConversion)){lastConversion=detail;append(detail);}}}
    public synchronized void worldEnd(){handoff(false);}
    public synchronized void waitingForInput(){handoff(false);}
    public synchronized void worldStatus(String value){if(world&&!waiting&&!disabled&&value!=null&&!value.isBlank()){detail=safe(value,140);if(!detail.equals(lastWorldStatus)){lastWorldStatus=detail;append(detail);}}}
    public synchronized void issue(String[] value){
        if(value==null||value.length==0||(value.length==5&&value[0].isBlank()))issue=new String[0];
        else if(value.length==5)issue=value.clone();
    }
    public synchronized void disable(){disabled=true;}
    public synchronized Snapshot snapshot(){return new Snapshot(route!=Route.NONE&&!waiting&&!disabled&&!nativeInteraction,world,title,detail,stage,stageIndex,mods.size(),scripts.size(),activity.toArray(String[]::new),issue.clone(),(System.nanoTime()-began)/1e9,context,route,epoch,scannedResources,name,mode,map,workshopProjects,requestedMods,queuePosition,downloaded,downloadTotal,generation,server,downloadPage,downloadID,downloadTitle,workshopReady,downloadRate,downloadPhase);}
    public static final class Snapshot {
        public final boolean active,world;
        public final boolean downloadPage;public final String downloadID,downloadTitle,downloadPhase;public final int workshopReady;public final double downloadRate;
        public final String title,detail,stage;
        public final int stageIndex,processedMods,processedScripts;
        public final String[] activity,issue;
        public final double elapsed;
        public final Context context;public final Route route;public final long epoch,scannedResources,downloaded,downloadTotal;
        public final String name,mode,map;public final int workshopProjects,requestedMods,queuePosition;
        public final long generation,worldDay;public final String endpoint,gameClock,gameDate;public final int online,maxPlayers;public final boolean clockObserved;
        Snapshot(boolean active,boolean world,String title,String detail,String stage,int index,int mods,int scripts,String[] activity,String[] issue,double elapsed,Context context,Route route,long epoch,long scanned,String name,String mode,String map,int projects,int requested,int queue,long downloaded,long total,long generation,ServerInfo server,boolean downloadPage,String downloadID,String downloadTitle,int workshopReady,double downloadRate,String downloadPhase){this.downloadPhase=downloadPhase;this.downloadPage=downloadPage;this.downloadID=downloadID;this.downloadTitle=downloadTitle;this.workshopReady=workshopReady;this.downloadRate=downloadRate;this.active=active;this.world=world;this.title=title;this.detail=detail;this.stage=stage;stageIndex=index;processedMods=mods;processedScripts=scripts;this.activity=activity;this.issue=issue;this.elapsed=elapsed;this.context=context;this.route=route;this.epoch=epoch;scannedResources=scanned;this.name=name;this.mode=mode;this.map=map;workshopProjects=projects;requestedMods=requested;queuePosition=queue;this.downloaded=downloaded;downloadTotal=total;this.generation=generation;endpoint=server.endpoint();online=server.online;maxPlayers=server.maximum;gameClock=server.clock;gameDate=server.date;worldDay=server.day;clockObserved=server.clockObserved;}
    }
}
