package ozl.startup;
import java.util.Locale;
/** Centered actual transfer above the retained native project list and controls. */
final class WorkshopScene {
 static String size(double bytes){return String.format(Locale.ROOT,"%.2f MiB",bytes/1048576d);}
 static void draw(LoadingScene.Canvas c,int w,int h,LoadingModel.Snapshot state,double time,boolean reduced,long used,long maximum,double workshopTop){
  int accent=0xffad8de8,ink=0xfff0eafa,muted=0xffbcb2ce;double s=LoadingScene.scale(w,h),margin=Math.max(24,42*s),protectedTop=workshopTop>0?workshopTop*h:h*.54;
  c.gradient(0,0,w,h,0xff211a3d,0xff0d1027);double header=LoadingScene.memory(c,w,s,false,used,maximum,ink,muted,accent),stageBottom=LoadingScene.stages(c,w,header,s,state,ink,muted,accent,0xff0d1027);
  boolean compact=h<600;int logRows=compact?1:2;
  double logLine=Math.max(14,16*s),logBaseline=protectedTop-10*s,statsBaseline=logBaseline-(logRows-1)*logLine-Math.max(18,20*s);
  double barHeight=Math.max(20,22*s),barY=statsBaseline-Math.max(16,20*s)-barHeight,titleY=barY-Math.max(16,16*s),heroTop=stageBottom+14*s,heroBottom=titleY-22*s,cy=(heroTop+heroBottom)/2,visualScale=Math.max(.1,Math.min(s,(heroBottom-heroTop)/(164*Math.sqrt(2)+16))),cx=w*.22;
  LoadingScene.ring(c,cx,cy,visualScale,time,reduced,accent);LoadingScene.orange(c,cx,cy,visualScale,LoadingModel.Context.SERVER,time,reduced,true);
  double infoX=w*.425,infoWidth=w*.54,nameY=heroTop+22*s;
  String item=state.downloadTitle.isEmpty()?state.downloadID.isEmpty()?"正在校验已安装项目":state.downloadID:state.downloadTitle;
  if(compact){LoadingScene.text(c,state.name+(state.endpoint.isEmpty()?"":" · "+state.endpoint),infoX,heroTop+11,12,muted,false,infoWidth);LoadingScene.text(c,item,infoX,heroTop+35,14,ink,false,infoWidth);}
  else{LoadingScene.text(c,state.name,infoX,nameY,Math.max(15,22*s),ink,false,infoWidth);LoadingScene.text(c,state.endpoint,infoX,nameY+22*s,Math.max(11,13*s),muted,false,infoWidth);LoadingScene.text(c,item,infoX,nameY+52*s,Math.max(14,20*s),ink,false,infoWidth);LoadingScene.text(c,state.downloadID.isEmpty()?"":"工坊项目 "+state.downloadID,infoX,nameY+72*s,Math.max(10,12*s),muted,false,infoWidth);}
  String heading=state.downloadPhase.equals("prepare")?"正在准备下一个工坊项目":state.downloadPhase.equals("validate")?"正在验证工坊项目":"正在下载工坊模组";LoadingScene.centered(c,heading,w*.5,titleY,Math.max(16,22*s),ink,w*.85);
  boolean known=state.downloadPhase.equals("download")&&state.downloaded>=0&&state.downloadTotal>1;double width=Math.min(1000*s,w*.76),x=(w-width)/2;if(known)LoadingScene.slot(c,x,barY,width,barHeight,state.downloaded/(double)state.downloadTotal,accent,false);else LoadingScene.activity(c,x,barY,width,barHeight,time,reduced,accent,false);
  String bytes=state.downloadPhase.equals("prepare")?"正在准备项目":state.downloadPhase.equals("validate")?"正在校验安装状态":state.downloaded<0?"等待下载进度":size(state.downloaded)+(state.downloadTotal>1?" / "+size(state.downloadTotal):"");LoadingScene.text(c,bytes,x,statsBaseline,Math.max(11,13*s),muted,false,width*.54);
  String progress=known?String.format(Locale.ROOT,"%.0f%%",state.downloaded*100d/state.downloadTotal):"";LoadingScene.text(c,progress,x+width,titleY,Math.max(14,18*s),ink,true,width*.2);String ready="已就绪 "+Math.max(0,state.workshopReady)+" / "+state.workshopProjects+" 工坊项目";LoadingScene.centered(c,ready,w*.5,statsBaseline,Math.max(10,12*s),muted,width*.42);
  if(state.downloadRate>=0&&state.downloadPhase.equals("download"))LoadingScene.text(c,size(state.downloadRate)+"/s",x+width,statsBaseline,Math.max(11,13*s),muted,true,width*.24);
  LoadingScene.logs(c,state,margin,w-margin,logBaseline,logLine,Math.max(10,12*s),w*.43-margin,w*.43-margin,logRows,ink,muted,accent);
 }
}
