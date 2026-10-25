package ozl.startup;
import java.util.Locale;
/** One readable current-item transfer above the original interactive project list. */
final class WorkshopScene {
 static String size(double bytes){return String.format(Locale.ROOT,"%.2f MiB",bytes/1048576d);}
 static void pill(LoadingScene.Canvas c,double x,double y,double width,double height,int color){if(width<=0)return;double r=Math.min(height/2,width/2);c.gradient(x+r,y,Math.max(0,width-2*r),height,color,color);c.arc(x+r,y+height/2,r/2,r,0,Math.PI*2,color);c.arc(x+width-r,y+height/2,r/2,r,0,Math.PI*2,color);}
 static void draw(LoadingScene.Canvas c,int w,int h,LoadingModel.Snapshot s,double time,boolean reduced){
  c.gradient(0,0,w,h,0xff211a3d,0xff0d1027);double scale=Math.max(.75,Math.min(1.4,Math.min(w/1440d,h/900d))),left=Math.max(28,w*.12),right=w-left,top=h*.08,meta=Math.max(14,14*scale),title=Math.max(22,26*scale),itemFont=Math.max(19,22*scale);
  c.glow(w*.8,h*.23,170*scale,0x299c74ff,0x009c74ff);
  String heading=s.downloadPhase.equals("prepare")?"正在准备下一个工坊项目":s.downloadPhase.equals("validate")?"正在验证工坊项目":"正在下载工坊模组";
  LoadingScene.text(c,heading,left,top,title,0xfff0eafa,false,right-left-65*scale);
  LoadingScene.text(c,s.name+(s.endpoint.isEmpty()?"":"  ·  "+s.endpoint),left,top+31,meta,0xffbcb2ce,false,(right-left)*.56);
  LoadingScene.text(c,"已就绪 "+Math.max(0,s.workshopReady)+" / "+s.workshopProjects+" 工坊项目",right,top+31,meta,0xffd3c6f0,true,(right-left)*.42);
  LoadingScene.text(c,s.downloadTitle.isEmpty()?s.downloadID.isEmpty()?"正在校验已安装项目":s.downloadID:s.downloadTitle,left,top+99,itemFont,0xfffaf7ff,false,right-left-80*scale);
  LoadingScene.text(c,s.downloadID.isEmpty()?"":"工坊项目 "+s.downloadID,left,top+127,meta,0xffb9aecb,false,(right-left)*.7);
  boolean known=s.downloadPhase.equals("download")&&s.downloaded>=0&&s.downloadTotal>1;
  if(known)LoadingScene.text(c,String.format(Locale.ROOT,"%.0f%%",s.downloaded*100d/s.downloadTotal),right,top+127,meta,0xffe5d8ff,true,(right-left)*.25);
  double y=top+149,track=right-left,bar=Math.max(20,20*scale);pill(c,left,y,track,bar,0xff413554);
  if(known)pill(c,left,y,track*Math.max(0,Math.min(1,s.downloaded/(double)s.downloadTotal)),bar,0xffad8de8);
  else if(!reduced){double segment=track*.18,p=(time*.24)%1;pill(c,left+p*(track-segment),y,segment,bar,0xff9676cf);}
  String bytes=s.downloadPhase.equals("prepare")?"正在准备项目":s.downloadPhase.equals("validate")?"正在校验安装状态":s.downloaded<0?"等待下载进度":size(s.downloaded)+(s.downloadTotal>1?" / "+size(s.downloadTotal):"");LoadingScene.text(c,bytes,left,y+bar+25,meta,0xffd8cee9,false,track*.68);
  if(s.downloadRate>=0&&s.downloadPhase.equals("download"))LoadingScene.text(c,size(s.downloadRate)+"/s",right,y+bar+25,meta,0xffc3acee,true,track*.3);
  LogoGrowth.Frame growth=LogoGrowth.at(time,reduced);c.growingLogo(right-25*scale,top+83,54*scale*growth.bodyScale(),1,growth.angle(),growth.leafScale(),growth.leafAngle());
 }
}
