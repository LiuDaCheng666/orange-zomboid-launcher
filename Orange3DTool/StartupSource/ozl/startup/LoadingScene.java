package ozl.startup;
import java.util.*;

/** Actual game and offscreen renderers share this approved original-orange layout. */
public final class LoadingScene {
 public interface Canvas {
  void gradient(double x,double y,double w,double h,int top,int bottom);
  void glow(double x,double y,double radius,int center,int edge);
  void arc(double x,double y,double radius,double thickness,double start,double length,int color);
  void line(double x1,double y1,double x2,double y2,double thickness,int color);
  void logo(double x,double y,double size,double alpha,double angle);
  default void growingLogo(double x,double y,double size,double alpha,double angle,double leafScale,double leafAngle){logo(x,y,size,alpha,angle);}
  default void growingLogo(double x,double y,double size,double alpha,double angle,double leafScale,double leafAngle,double sx,double sy){growingLogo(x,y,size,alpha,angle,leafScale,leafAngle);}
  double measure(String text,double size);
  void text(String value,double x,double y,double size,int color,boolean right);
 }
 public static double font(double size){return Math.max(9,Math.min(64,size));}
 static double scale(int w,int h){return Math.max(.6,Math.min(2.5,Math.min(w/1280d,h/720d)));}
 static int alpha(int color,int opacity){return (Math.max(0,Math.min(255,opacity))<<24)|(color&0xffffff);}
 /** Return a code-point boundary using logarithmic font measurements. */
 static int prefix(Canvas c,String value,double size,double width,String suffix){
  int low=0,high=value.codePointCount(0,value.length());
  while(low<high){int middle=(low+high+1)>>>1,end=value.offsetByCodePoints(0,middle);if(c.measure(value.substring(0,end)+suffix,size)<=width)low=middle;else high=middle-1;}
  return value.offsetByCodePoints(0,low);
 }
 static String fit(Canvas c,String value,double size,double width){
  value=LoadingModel.safe(value,512);size=font(size);if(value.isEmpty()||width<=0)return "";
  if(c.measure(value,size)<=width)return value;if(c.measure("…",size)>width)return "";
  int end=prefix(c,value,size,width,"…");return end==0?"":value.substring(0,end)+"…";
 }
 static void text(Canvas c,String value,double x,double y,double size,int color,boolean right,double width){if(!value.isEmpty())c.text(fit(c,value,size,width),x,y,font(size),color,right);}
 static void centered(Canvas c,String value,double x,double y,double size,int color,double width){if(value.isEmpty())return;String v=fit(c,value,size,width);c.text(v,x-c.measure(v,font(size))/2,y,font(size),color,false);}
 /** A rectangular track with actual fill inset two pixels; no enclosing panel. */
 static void slot(Canvas c,double x,double y,double width,double height,double ratio,int accent,boolean light){int track=light?0x2052616a:0x22a4b5c1;c.gradient(x,y,width,height,track,track);double inset=Math.min(2,height/4),fill=Math.max(0,width-2*inset)*Math.max(0,Math.min(1,ratio));if(fill>0)c.gradient(x+inset,y+inset,fill,height-2*inset,accent,accent);}
 static double memory(Canvas c,int w,double s,boolean light,long used,long maximum,int ink,int muted,int accent){
  if(maximum<=0)return Math.max(34,48*s);double width=Math.min(900*s,w*.60),x=(w-width)/2,y=Math.max(18,24*s),height=Math.max(14,18*s);slot(c,x,y,width,height,Math.max(0,used)/(double)maximum,accent,light);
  String heap=String.format(Locale.ROOT,"%,d / %,d MiB",Math.max(0,used)>>20,maximum>>20);double label=Math.max(14,14*s),value=Math.max(20,20*s),gap=12*s,baseline=y+height+Math.max(24,28*s),lw=c.measure("Java 内存",label),vw=c.measure(heap,value),start=(w-lw-gap-vw)/2;c.text("Java 内存",start,baseline,label,muted,false);c.text(heap,start+lw+gap,baseline,value,ink,false);return baseline;
 }
 static double stages(Canvas c,int w,double top,double s,LoadingModel.Snapshot state,int ink,int muted,int accent,int background){
  String[] names=switch(state.context){case SAVE->new String[]{"准备","世界","就绪"};case SERVER->new String[]{"连接","创意工坊","模组","同步","世界","就绪"};default->new String[]{"初始化","模组","资源","脚本","界面","就绪"};};
  double margin=Math.max(22,48*s),cell=(w-2*margin)/names.length,y=top+Math.max(20,27*s),node=Math.max(4,6*s);int active=Math.max(0,Math.min(names.length-1,state.stageIndex));
  double labelGap=Math.max(18,20*s);
  for(int i=0;i<names.length;i++){double x=margin+cell*(i+.5);if(i<names.length-1)c.line(x+node+6*s,y,x+cell-node-6*s,y,Math.max(1,s),alpha(muted,40));int color=i<active?alpha(accent,165):i==active?accent:alpha(muted,75);c.arc(x,y,i<active?node*.5:node,i<active?node:Math.max(1,1.2*s),0,Math.PI*2,color);if(i<active){c.line(x-node*.45,y,x-node*.08,y+node*.35,Math.max(1,s),background);c.line(x-node*.08,y+node*.35,x+node*.48,y-node*.35,Math.max(1,s),background);}else if(i==active)centered(c,Integer.toString(i+1),x,y+3*s,font(9*s),accent,node*2);centered(c,names[i],x,y+labelGap,Math.max(10,11*s),i==active?ink:muted,cell-4*s);}return y+labelGap;
 }
 public static void draw(Canvas c,int w,int h,LoadingModel.Snapshot state,double seconds,boolean light,boolean reduced,long used,long maximum){draw(c,w,h,state,seconds,light,reduced,used,maximum,0,0,0);}
 public static void draw(Canvas c,int w,int h,LoadingModel.Snapshot state,double seconds,boolean light,boolean reduced,long used,long maximum,double statusBottom,double cancelTop){draw(c,w,h,state,seconds,light,reduced,used,maximum,statusBottom,cancelTop,0);}
 public static void draw(Canvas c,int w,int h,LoadingModel.Snapshot state,double seconds,boolean light,boolean reduced,long used,long maximum,double statusBottom,double cancelTop,double workshopTop){
  if(state.downloadPage){WorkshopScene.draw(c,w,h,state,seconds,reduced,used,maximum,workshopTop);return;}
  boolean nativeControls=state.context==LoadingModel.Context.SERVER&&statusBottom>0&&cancelTop>statusBottom;if(nativeControls)light=false;double s=scale(w,h),margin=Math.max(22,42*s);
  int accent=switch(state.context){case BOOT,NONE->0xffffbd76;case SERVER->0xff67d8f0;case SAVE->0xff73dfa8;},ink=light?0xff29343e:0xffe9edf2,muted=light?0xff52616a:0xffa4b5c1;
  int top=switch(state.context){case BOOT,NONE->light?0xfff6eee3:0xff2b201b;case SERVER->light?0xffe7f2f8:0xff0c263d;case SAVE->light?0xffe7f3e9:0xff122f27;},bottom=switch(state.context){case BOOT,NONE->light?0xffe8ddd1:0xff130f10;case SERVER->light?0xffd0e4ee:0xff07121f;case SAVE->light?0xffd3e5d6:0xff071812;};c.gradient(0,0,w,h,top,bottom);
  double header=memory(c,w,s,light,used,maximum,ink,muted,accent),stageBottom=stages(c,w,header,s,state,ink,muted,accent,bottom);
  String context=switch(state.context){case BOOT,NONE->"启动游戏";case SERVER->"连接服务器";case SAVE->"载入单人存档";};text(c,context,margin,Math.max(32,40*s),Math.max(11,12*s),muted,false,Math.max(0,w*.2-margin-12*s));
  double cx=state.context==LoadingModel.Context.BOOT?w*.5:w*.22,cy=h*.37,visualScale=s,titleY=h*.64;boolean wideNative=false;
  if(nativeControls){double upper=statusBottom*h+16*s,lower=cancelTop*h-16*s,heroHeight=Math.max(12,lower-upper-70*s);wideNative=heroHeight>=240*s;cx=wideNative?w*.22:w*.5;cy=upper+heroHeight/2;visualScale=Math.min(s,heroHeight/(174*Math.sqrt(2)+30));titleY=lower-48*s;}
  c.glow(cx,cy,Math.min(280*visualScale,w*.4),alpha(accent,light?30:22),alpha(accent,0));if(state.context==LoadingModel.Context.SAVE)map(c,cx,cy,visualScale,seconds,reduced,accent);else if(state.context==LoadingModel.Context.SERVER)network(c,cx,cy,visualScale,seconds,reduced,accent);else ring(c,cx,cy,visualScale,seconds,reduced,accent);orange(c,cx,cy,visualScale,state.context,seconds,reduced,false);
  if(state.context==LoadingModel.Context.SERVER){double infoX=nativeControls&&!wideNative?margin:w*.425,infoY=nativeControls&&!wideNative?stageBottom+Math.max(28,25*s):cy-80*s;serverFacts(c,state,infoX,infoY,nativeControls&&!wideNative?w-2*margin:w*.54,s,ink,muted,accent,nativeControls&&!wideNative);}
  else if(state.context==LoadingModel.Context.BOOT)centered(c,"O Z L",cx,cy+118*s,22*s,ink,w*.8);
  else{double x=w*.425,y=cy-46*s,width=w*.54;text(c,state.name,x,y,24*s,ink,false,width);String mode=switch(state.mode){case "Sandbox"->"沙盒";case "Survival"->"生存";default->state.mode;};String meta=String.join(" · ",java.util.stream.Stream.of(mode,state.map).filter(v->!v.isEmpty()).toList());text(c,meta,x,y+27*s,Math.max(12,14*s),muted,false,width);text(c,state.detail,x,y+60*s,Math.max(12,14*s),muted,false,width);}
  LoadingHumor.Caption caption=LoadingHumor.at(seconds,state.generation,state.context,reduced);double humorY=state.context==LoadingModel.Context.BOOT?cy+146*s:cy+134*visualScale;if(nativeControls)humorY=Math.min(humorY,titleY-27*s);centered(c,caption.text(),cx,humorY,Math.max(10,13*s),alpha(muted,caption.opacity()),Math.min(w*.44,340*s));
  centered(c,state.title,w*.5,titleY,Math.max(15,22*s),ink,w*.86);double width=Math.min(900*s,w*.60),y=titleY+15*s,height=Math.max(7,10*s);activity(c,(w-width)/2,y,width,height,seconds,reduced,accent,light);
  String count=state.processedScripts>0?"已读取 "+state.processedScripts+" 个脚本":state.processedMods>0?"已处理 "+state.processedMods+" 个模组":state.scannedResources>0?"已索引 "+state.scannedResources+" 项资源":"";centered(c,state.detail.isEmpty()?count:state.detail,w*.5,y+height+22*s,Math.max(11,14*s),muted,w*.8);
  double line=Math.max(16,22*s),baseline=h-Math.max(24,32*s),leftWidth=w*.46-margin,rightWidth=w*.42-margin;if(nativeControls){leftWidth=Math.min(leftWidth,w*.5-75-margin);rightWidth=Math.min(rightWidth,w*.5-75-margin);}logs(c,state,margin,w-margin,baseline,line,Math.max(11,14*s),leftWidth,rightWidth,5,ink,muted,light?ink:accent);
 }
 static void activity(Canvas c,double x,double y,double width,double height,double seconds,boolean reduced,int accent,boolean light){slot(c,x,y,width,height,0,accent,light);double inset=Math.min(2,height/4),segment=(width-2*inset)*.25,p=reduced?.5:(Math.sin(seconds*.75)+1)/2;c.gradient(x+inset+p*(width-2*inset-segment),y+inset,segment,Math.max(1,height-2*inset),accent,accent);}
 static void logs(Canvas c,LoadingModel.Snapshot state,double left,double right,double baseline,double line,double size,double leftWidth,double rightWidth,int count,int ink,int muted,int accent){int rows=Math.min(count,state.activity.length);for(int i=0;i<rows;i++)text(c,state.activity[state.activity.length-rows+i],left,baseline-(rows-1-i)*line,size,i==rows-1?ink:muted,false,leftWidth);if(state.issue.length==5){String[] values={state.issue[0],state.issue[2].isEmpty()?"":"模组 · "+state.issue[2],state.issue[1],state.issue[3],state.issue[4]};int limit=Math.min(count,values.length);for(int i=0;i<limit;i++)text(c,values[i],right,baseline-(limit-1-i)*line,size,i==0?accent:i==1?ink:muted,true,rightWidth);}}
 static void orange(Canvas c,double cx,double cy,double s,LoadingModel.Context context,double seconds,boolean reduced,boolean download){LogoGrowth.Frame pose=LogoGrowth.at(seconds,reduced,context,download);double size=(context==LoadingModel.Context.BOOT?174:164)*s,opacity=reduced?1:Math.max(0,Math.min(1,seconds/.65));c.growingLogo(cx,cy+size*pose.lift(),size,opacity,pose.angle(),pose.leafScale(),pose.leafAngle(),pose.stretchX(),pose.stretchY());}
 static void ring(Canvas c,double cx,double cy,double s,double time,boolean reduced,int accent){double start=reduced?0:time*.22;c.arc(cx,cy,97*s,Math.max(.6,s),0,Math.PI*2,alpha(accent,35));c.arc(cx,cy,97*s,1.5*s,start,Math.PI*.34,alpha(accent,155));c.arc(cx,cy,97*s,1.5*s,start+Math.PI,Math.PI*.25,alpha(accent,75));}
 static void map(Canvas c,double cx,double cy,double s,double time,boolean reduced,int accent){double gap=6*s,cell=28*s;for(int y=-2;y<=2;y++)for(int x=-3;x<=3;x++){double wave=reduced?.3:Math.pow(Math.max(0,Math.sin((time*.23-Math.hypot(x,y)*.12)*Math.PI*2)),2),xx=cx+x*(cell+gap)-cell/2,yy=cy+y*(cell+gap)-cell/2;int fill=alpha(accent,(int)(18+wave*46));c.gradient(xx,yy,cell,cell,fill,fill);c.line(xx,yy,xx+cell,yy,Math.max(.6,s),alpha(accent,55));c.line(xx,yy,xx,yy+cell,Math.max(.6,s),alpha(accent,55));}}
 static void network(Canvas c,double cx,double cy,double s,double time,boolean reduced,int accent){double[][] points={{-145,-40},{-90,-89},{15,-100},{133,-51},{144,50},{44,93},{-80,84}};int active=(int)(Math.max(0,time)/2.8)%points.length;double p=reduced?.5:1-(time%2.8)/2.8;for(int i=0;i<points.length;i++){double x=cx+points[i][0]*s,y=cy+points[i][1]*s;c.line(cx,cy,x,y,Math.max(.6,s),alpha(accent,60));c.arc(x,y,4*s,Math.max(.6,s),0,Math.PI*2,alpha(accent,155));if(i==active&&!reduced)c.arc(cx+(x-cx)*p,cy+(y-cy)*p,1.3*s,2.6*s,0,Math.PI*2,accent);}}
 private static void serverFacts(Canvas c,LoadingModel.Snapshot state,double x,double y,double width,double s,int ink,int muted,int accent,boolean compact){
  double line=Math.max(16,(compact?16:22)*s),name=Math.max(16,(compact?20:25)*s),small=Math.max(12,14*s),label=Math.max(10,12*s);
  double nameGap=Math.max(line,name*1.3);text(c,state.name,x,y,name,ink,false,width);int endpointLines=leftWrapped(c,state.endpoint,x,y+nameGap,small,muted,width,line);
  double base=y+nameGap+endpointLines*line+(compact?4:15)*s,col=width/3;
  String population=state.online>=0?(state.maxPlayers>0?state.online+" / "+state.maxPlayers:Integer.toString(state.online)):state.maxPlayers>0?"— / "+state.maxPlayers:"—";
  String[] labels={"在线玩家","游戏时间","世界天数"},values={population,state.gameClock.isEmpty()?"—":state.gameClock,state.worldDay>0?"第 "+state.worldDay+" 天":"—"};
  double compactRow=Math.max(18,20*s);
  for(int i=0;i<3;i++){if(compact)text(c,labels[i]+" · "+values[i],x+i*col,base,small,ink,false,col-8*s);else{text(c,labels[i],x+i*col,base,label,muted,false,col-8*s);text(c,values[i],x+i*col,base+line,Math.max(14,20*s),i==0?accent:ink,false,col-8*s);}}
  if(!state.gameDate.isEmpty())text(c,state.gameDate,x+col,compact?base+compactRow:base+line+Math.max(15,16*s),Math.max(12,11*s),muted,false,col-8*s);
  double second=compact?base+(state.gameDate.isEmpty()?1:2)*compactRow:base+2*line+(state.gameDate.isEmpty()?12:23)*s;
  String[] lowerLabels={"地图","模组","创意工坊"},lowerValues={state.map.isEmpty()?"—":state.map,state.requestedMods>=0?state.requestedMods+" 个":"—",state.workshopProjects>=0?state.workshopProjects+" 项":"—"};
  for(int i=0;i<3;i++){if(compact)text(c,lowerLabels[i]+" · "+lowerValues[i],x+i*col,second,small,ink,false,col-8*s);else{text(c,lowerLabels[i],x+i*col,second,label,muted,false,col-8*s);text(c,lowerValues[i],x+i*col,second+line,small,ink,false,col-8*s);}}
 }
 private static int leftWrapped(Canvas c,String value,double x,double y,double size,int color,double width,double line){
  if(value.isEmpty())return 0;int rows=0;
  while(!value.isEmpty()){int end=c.measure(value,font(size))<=width?value.length():prefix(c,value,font(size),width,"");if(end==0)break;c.text(value.substring(0,end),x,y+rows*line,font(size),color,false);value=value.substring(end);rows++;}return rows;
 }
}
