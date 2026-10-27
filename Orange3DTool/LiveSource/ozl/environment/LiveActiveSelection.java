package ozl.environment;
import java.util.*;
/** Live wrapper retains the manifest boundary while sharing scoped persistence. */
final class LiveActiveSelection extends ActiveSelectionSession {
 LiveActiveSelection(LiveEnvironmentManifest e){super(e.user,e.choices,e.override,(ids,ordinary)->e.normalize(ids,ordinary));}
 @Override synchronized void defaultLoaded(List<String> ids){super.defaultLoaded(ids);}
 @Override synchronized void copied(Object target,Object source,String id,List<String> ids,boolean client){super.copied(target,source,id,ids,client);}
 @Override synchronized void readSource(String file,Object active)throws java.io.IOException{super.readSource(file,active);}
 @Override synchronized String writePath(String file,Object active,String id)throws Exception{return super.writePath(file,active,id);}
}
