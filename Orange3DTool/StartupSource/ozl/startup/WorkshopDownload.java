package ozl.startup;
import java.lang.reflect.*;
import java.util.*;

/** One native connection's cached workshop observations; never polls Steam. */
final class WorkshopDownload {
 final Object owner;final long generation;final LinkedHashSet<String> ids=new LinkedHashSet<>();
 final Map<String,String> titles=new HashMap<>();boolean installed,started,valid=true;String item="";long bytes=-1,total=-1,sampleBytes=-1,sampleTotal=-1,sampleAt,lastAt;double rate=-1;int ready=-1;
 WorkshopDownload(Object owner,long generation,Object required){this.owner=owner;this.generation=generation;
  if(owner==null||!(required instanceof ArrayList<?> list)||list.isEmpty()||list.size()>16384){valid=false;return;}
  for(Object id:list)if(!(id instanceof String s)||!s.matches("[0-9]{1,20}")||!ids.add(s)){valid=false;break;}
 }
 static Object read(Object value,String name)throws Exception{Field f=value.getClass().getDeclaredField(name);if(!f.canAccess(value))f.setAccessible(true);return f.get(value);}
 boolean state(String expected){try{return read(owner,"state") instanceof Enum<?> e&&e.name().equals(expected);}catch(Exception unavailable){return false;}}
 void details(Object value){if(!valid||!(value instanceof ArrayList<?> list)||list.size()>ids.size())return;try{HashSet<String> found=new HashSet<>();for(Object d:list){Object id=d.getClass().getMethod("getIDString").invoke(d),title=d.getClass().getMethod("getTitle").invoke(d);if(!(id instanceof String s)||!ids.contains(s)||!found.add(s)||!(title instanceof String t)||t.length()>2048){valid=false;return;}titles.put(s,LoadingModel.safe(t,160));}}catch(Exception unavailable){valid=false;}}
 void progress(Object id,Object received,Object denominator,long now){if(!valid||!installed||!state("WorkshopUpdate")||!(id instanceof String s)||!ids.contains(s)||!(received instanceof Long b)||!(denominator instanceof Long t)||b<0||t<1||(t>1&&b>t))return;
  // Only the owner's first unfinished item can publish the current transfer.
  observe();if(!s.equals(current()))return;
  if(!s.equals(item)||b<bytes||t!=total||lastAt!=0&&now-lastAt>2_000_000_000L){rate=-1;sampleAt=0;}item=s;bytes=b;total=t;lastAt=now;
  if(sampleAt==0){sampleAt=now;sampleBytes=b;sampleTotal=t;rate=-1;}
  else if(now>sampleAt&&now-sampleAt>=500_000_000L){double measured=(b-sampleBytes)*1e9/(now-sampleAt);rate=Double.isFinite(measured)&&measured>=0&&sampleTotal==t?measured:-1;sampleAt=now;sampleBytes=b;sampleTotal=t;}
 }
 private String observedCurrent="",observedState="";private Object observedItems;private int observedSize;private boolean observedValid;
 /** One bounded observation at original update/progress or CPU frame acceptance, never per draw. */
 void observe(){ready=-1;observedValid=false;observedCurrent=observedState="";observedItems=null;
  try{Object raw=read(owner,"workshopItems");if(!(raw instanceof ArrayList<?> list)||list.size()!=ids.size())return;HashSet<String> found=new HashSet<>();int count=0;
   for(Object entry:list){Object id=read(entry,"id"),state=read(entry,"state");if(!(id instanceof Long n)||!(state instanceof Enum<?> e))return;String name=e.name(),s=Long.toUnsignedString(n);if(!ids.contains(s)||!found.add(s)||!(name.equals("Ready")||name.equals("CheckItemState")||name.equals("SubscribePending")||name.equals("DownloadPending")))return;if(name.equals("Ready"))count++;else if(observedCurrent.isEmpty()){observedCurrent=s;observedState=name;}}
   ready=count;observedItems=list;observedSize=list.size();observedValid=true;
  }catch(Exception unavailable){}
 }
 String current(){return observedValid?observedCurrent:"";}
 boolean present(){try{return valid&&installed&&state("WorkshopUpdate")&&observedValid&&read(owner,"workshopItems")==observedItems&&((ArrayList<?>)observedItems).size()==observedSize&&(started||observedState.equals("DownloadPending"));}catch(Exception unavailable){return false;}}
 boolean transfer(){return valid&&installed&&observedValid&&state("WorkshopUpdate")&&observedState.equals("DownloadPending");}
 void publish(LoadingModel model,long now){observe();boolean transferring=transfer();if(transferring)started=true;else{rate=-1;sampleAt=lastAt=0;}boolean active=present();if(!active)model.nativeInteraction(true);String current=current();if(!current.equals(item)){item=current;bytes=total=-1;rate=-1;sampleAt=lastAt=0;}model.downloadPhase(transferring?"download":current.isEmpty()?"validate":"prepare");model.download(active,item,titles.getOrDefault(item,""),ready,bytes,total,lastAt!=0&&now>=lastAt&&now-lastAt<=2_000_000_000L?rate:-1);}
}
