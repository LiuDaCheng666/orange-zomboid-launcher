package ozl.startup;

import java.util.*;

/** Production layout shared by the actual game GL canvas and offscreen inspection. */
public final class LoadingScene {
    public interface Canvas {
        void gradient(double x,double y,double w,double h,int top,int bottom);
        void glow(double x,double y,double radius,int center,int edge);
        void arc(double x,double y,double radius,double thickness,double start,double length,int color);
        void line(double x1,double y1,double x2,double y2,double thickness,int color);
        void logo(double x,double y,double size,double alpha,double angle);
        default void growingLogo(double x,double y,double size,double alpha,double angle,double leafScale,double leafAngle){logo(x,y,size,alpha,angle);}
        default void growingLogo(double x,double y,double size,double alpha,double angle,double leafScale,double leafAngle,double stretchX,double stretchY){growingLogo(x,y,size,alpha,angle,leafScale,leafAngle);}
        double measure(String text,double size);
        void text(String value,double x,double y,double size,int color,boolean right);
    }
    /** Match both GL's raster font and scene measurement, including small viewports. */
    public static double font(double size){return Math.max(9,Math.min(64,size));}
    static int alpha(int color,int opacity){return (Math.max(0,Math.min(255,opacity))<<24)|(color&0xffffff);}
    static String fit(Canvas c,String text,double size,double width){
        text=LoadingModel.safe(text,512);size=font(size);if(c.measure(text,size)<=width)return text;
        int end=text.length();while(end>0&&c.measure(text.substring(0,end)+"…",size)>width)end--;
        return end==0?"":text.substring(0,end)+"…";
    }
    static void text(Canvas c,String value,double x,double y,double size,int color,boolean right,double width){if(!value.isEmpty())c.text(fit(c,value,size,width),x,y,font(size),color,right);}
    static double entrance(double time){double t=Math.max(0,Math.min(1,time/1.1)),u=t-1;return 1+2.4*u*u*u+1.4*u*u;}
    public static void draw(Canvas c,int w,int h,LoadingModel.Snapshot state,double seconds,boolean light,boolean reduced,long used,long maximum) {
        draw(c,w,h,state,seconds,light,reduced,used,maximum,0,0);
    }
    /** Native widget bounds are normalized immutable observations from the accepted CPU frame. */
    public static void draw(Canvas c,int w,int h,LoadingModel.Snapshot state,double seconds,boolean light,boolean reduced,long used,long maximum,double statusBottom,double cancelTop) {
        if(state.downloadPage){WorkshopScene.draw(c,w,h,state,seconds,reduced);return;}
        boolean nativeControls=state.context==LoadingModel.Context.SERVER&&statusBottom>0&&cancelTop>statusBottom;
        if(nativeControls)light=false; // Original native status is white and remains authoritative.
        double sceneScale=Math.max(.6,Math.min(2.5,Math.min(w/1440d,h/900d))),s=nativeControls?Math.min(1.3,sceneScale):sceneScale,margin=Math.max(22,48*s);
        int accent=switch(state.context){case BOOT, NONE -> 0xffffbd76;case SERVER -> 0xff67d8f0;case SAVE -> 0xff73dfa8;},ink=light?0xff29343e:0xffe9edf2,muted=light?0xff52616a:0xffa4b5c1;
        int topColor=switch(state.context){case BOOT, NONE -> light?0xfff6eee3:0xff2b201b;case SERVER -> light?0xffe7f2f8:0xff0c263d;case SAVE -> light?0xffe7f3e9:0xff122f27;};
        int bottomColor=switch(state.context){case BOOT, NONE -> light?0xffe8ddd1:0xff130f10;case SERVER -> light?0xffd0e4ee:0xff07121f;case SAVE -> light?0xffd3e5d6:0xff071812;};
        c.gradient(0,0,w,h,topColor,bottomColor);
        double track=w-2*margin,top=Math.max(28,42*s),titleSize=font(15*s),small=font(12*s);
        text(c,state.title,margin,top,titleSize,ink,false,track*.52);
        String count=state.context==LoadingModel.Context.BOOT?(state.processedScripts>0?"已读取 "+state.processedScripts+" 个脚本":state.processedMods>0?"已处理 "+state.processedMods+" 个模组":state.scannedResources>0?"已索引 "+state.scannedResources+" 项资源":""):"";
        if(maximum>0){
            double ratio=Math.max(0,Math.min(1,used/(double)maximum)),heapWidth=Math.min(track*.25,160*s),heapY=top+9*s;
            String heap=String.format(Locale.ROOT,"Java 内存  %,d / %,d MiB",Math.max(0,used)>>20,maximum>>20);
            text(c,heap,w-margin,top,font(10*s),muted,true,track*.46);
            c.gradient(w-margin-heapWidth,heapY,heapWidth,Math.max(1,2*s),alpha(muted,40),alpha(muted,40));
            c.gradient(w-margin-heapWidth,heapY,heapWidth*ratio,Math.max(1,2*s),alpha(accent,145),alpha(accent,145));
        }
        String[] milestones=switch(state.context){case SAVE->new String[]{"准备","世界","就绪"};case SERVER->new String[]{"连接","创意工坊","模组","同步","世界","就绪"};default->new String[]{"初始化","模组","资源","脚本","界面","就绪"};};
        int stages=milestones.length,active=Math.max(0,Math.min(stages-1,state.stageIndex));
        double gap=8*s,cell=(track-gap*(stages-1))/stages,stageY=top+29*s;
        double node=Math.max(4,6*s);
        for(int i=0;i<stages-1;i++){
            double x=margin+i*(cell+gap)+cell/2;
            c.line(x+node+4*s,stageY,x+cell+gap-node-4*s,stageY,Math.max(1,s),alpha(muted,40));
        }
        for(int i=0;i<stages;i++){
            double x=margin+i*(cell+gap)+cell/2;int color=i<active?alpha(accent,160):i==active?accent:alpha(muted,75);
            c.arc(x,stageY,i<active?node*.5:i==active?node+1.5*s:node,i<active?node:Math.max(1,1.5*s),0,Math.PI*2,color);
            if(i<active){c.line(x-node*.45,stageY,x-node*.08,stageY+node*.35,Math.max(1,s),bottomColor);c.line(x-node*.08,stageY+node*.35,x+node*.48,stageY-node*.35,Math.max(1,s),bottomColor);}
            else if(i==active)centered(c,Integer.toString(i+1),x,stageY+3*s,font(9*s),accent,node*2);
            centered(c,milestones[i],x,stageY+20*s,font(i==active?11*s:10*s),i==active?ink:muted,cell);
        }
        String observed=state.detail.isEmpty()?count:state.detail;
        if(!observed.isEmpty()&&!nativeControls)text(c,observed,margin,stageY+42*s,small,muted,false,track*.75);
        double visualScale=s,cx=w*.5,cy=h*(state.context==LoadingModel.Context.SERVER?.40:state.context==LoadingModel.Context.SAVE?.37:.42),t=reduced?2:Math.max(0,seconds);
        LogoGrowth.Frame growth=LogoGrowth.at(seconds,reduced);
        if(state.context==LoadingModel.Context.SAVE){cx=w*.29;cy=h*.40;}
        else if(state.context==LoadingModel.Context.SERVER&&!nativeControls){cx=w*.25;cy=h*.40;}
        if(nativeControls){double upper=statusBottom*h+24*s,lower=cancelTop*h-24*s;cy=(upper+lower)/2;visualScale=Math.min(sceneScale,Math.max(.1,(lower-upper)/260));}
        c.glow(cx,cy,Math.min(310*visualScale,w*.46),alpha(accent,light?35:24),alpha(accent,0));
        if(state.context==LoadingModel.Context.SAVE)map(c,cx,cy,s,t,reduced,accent,light);
        else if(state.context==LoadingModel.Context.SERVER)network(c,cx,cy,visualScale,t,reduced,accent,light);
        else {
            c.arc(cx,cy,107*s,1.1*s,0,Math.PI*2,light?0x38757e71:0x343e5261);
            c.arc(cx,cy,107*s,1.7*s,Math.PI*.18,Math.PI*.34,alpha(accent,125));
            c.arc(cx,cy,107*s,1.7*s,Math.PI*1.18,Math.PI*.34,alpha(accent,65));
        }
        double logoBase=(state.context==LoadingModel.Context.BOOT?174:state.context==LoadingModel.Context.SAVE?130:150)*visualScale;
        double logoSize=logoBase*growth.bodyScale();
        c.growingLogo(cx,cy+logoBase*growth.lift(),logoSize,1,growth.angle(),growth.leafScale(),growth.leafAngle(),growth.stretchX(),growth.stretchY());
        double labelY=state.context==LoadingModel.Context.BOOT?cy+157*s:h*.31;
        if(state.context==LoadingModel.Context.SERVER){
            double infoX=nativeControls?margin:w*.46,infoY=nativeControls?top+98*s:h*.28;
            double infoWidth=nativeControls?track:w*.49;
            serverFacts(c,state,infoX,infoY,infoWidth,s,ink,muted,accent,nativeControls);
            if("排队".equals(state.stage))centered(c,state.detail,w*.5,nativeControls?Math.min(cancelTop*h-12*s,cy+120*visualScale):h*.69,small,ink,w*.78);
        }else{
            String identity=state.context==LoadingModel.Context.BOOT?"O Z L":state.name;
            double infoCx=state.context==LoadingModel.Context.BOOT?w*.5:w*.68,infoWidth=state.context==LoadingModel.Context.BOOT?w*.84:w*.49;
            centered(c,identity,infoCx,labelY,font(21*s),ink,infoWidth);
            String mode=switch(state.mode){case "Sandbox" -> "沙盒";case "Survival" -> "生存";default -> state.mode;};
            String metadata=String.join("  ·  ",java.util.stream.Stream.of(mode,state.map).filter(v->!v.isEmpty()).toList());
            centered(c,metadata,infoCx,labelY+25*s,small,muted,infoWidth);
            if(state.context==LoadingModel.Context.SAVE)centered(c,state.detail,infoCx,labelY+56*s,small,muted,infoWidth);
        }
        LoadingHumor.Caption caption=LoadingHumor.at(seconds,state.generation,state.context,reduced);
        double humorY=nativeControls?Math.min(cy+150*visualScale,cancelTop*h-15*s):state.context==LoadingModel.Context.BOOT?labelY+28*s:cy+153*s;
        centered(c,caption.text(),cx,humorY,Math.max(10,12*s),alpha(muted,caption.opacity()),Math.min(w*.52,300*s));
        double line=Math.max(16,23*s),bottom=h-Math.max(23,45*s),body=font(13*s),leftWidth=w*.46-margin,rightWidth=w*.42-margin;
        if(nativeControls){leftWidth=Math.min(leftWidth,w*.5-75-margin);rightWidth=Math.min(rightWidth,w*.5-75-margin);}
        int rows=Math.min(5,state.activity.length);
        for(int i=0;i<rows;i++)text(c,state.activity[state.activity.length-rows+i],margin,bottom-(rows-1-i)*line,body,i==rows-1?ink:muted,false,leftWidth);
        if(state.issue.length==5){double right=w-margin,y=bottom-4*line;text(c,state.issue[0],right,y,body,accent,true,rightWidth);
            String[] values={state.issue[2].isEmpty()?"":"模组  ·  "+state.issue[2],state.issue[1],state.issue[3],state.issue[4]};
            for(int i=0;i<values.length;i++)text(c,values[i],right,y+(i+1)*line,body,i==0?ink:muted,true,rightWidth);
        }
    }
    private static void serverFacts(Canvas c,LoadingModel.Snapshot state,double x,double y,double width,double s,int ink,int muted,int accent,boolean compact){
        double small=Math.max(12,font(12*s)),label=font(10*s),line=Math.max(16,(compact?18:22)*s);
        text(c,state.name,x,y,font(22*s),ink,false,width);
        int endpointLines=leftWrapped(c,state.endpoint,x,y+line,small,muted,width,line);
        double base=y+(endpointLines+1)*line+16*s,col=width/3;
        String population=state.online>=0?(state.maxPlayers>0?state.online+" / "+state.maxPlayers:Integer.toString(state.online)):state.maxPlayers>0?"— / "+state.maxPlayers:"—";
        String clock=state.gameClock.isEmpty()?"—":state.gameClock,day=state.worldDay>0?"第 "+state.worldDay+" 天":"—";
        String[] labels={"在线玩家","游戏时间","世界天数"},values={population,clock,day};
        for(int i=0;i<3;i++){
            text(c,labels[i],x+i*col,base,label,muted,false,col-8*s);
            text(c,values[i],x+i*col,base+line,font(21*s),i==0?accent:ink,false,col-8*s);
        }
        double second=base+2*line+12*s;
        String[] lowerLabels={"地图","模组","创意工坊"},lowerValues={state.map.isEmpty()?"—":state.map,state.requestedMods>=0?state.requestedMods+" 个":"—",state.workshopProjects>=0?state.workshopProjects+" 项":"—"};
        for(int i=0;i<3;i++){
            text(c,lowerLabels[i],x+i*col,second,label,muted,false,col-8*s);
            text(c,lowerValues[i],x+i*col,second+line,small,ink,false,col-8*s);
        }
        if(!state.gameDate.isEmpty())text(c,state.gameDate,x+col,base+line+Math.max(14,17*s),Math.max(12,font(10*s)),muted,false,col-8*s);

    }
    private static int leftWrapped(Canvas c,String value,double x,double y,double size,int color,double width,double line){
        if(value.isEmpty())return 0;int rows=0;
        while(!value.isEmpty()){int end=value.length();while(end>1&&c.measure(value.substring(0,end),font(size))>width)end--;c.text(value.substring(0,end),x,y+rows*line,font(size),color,false);value=value.substring(end);rows++;}return rows;
    }
    private static void centered(Canvas c,String text,double x,double y,double size,int color,double width){if(text.isEmpty())return;String v=fit(c,text,size,width);c.text(v,x-c.measure(v,font(size))/2,y,font(size),color,false);}
    private static int wrapped(Canvas c,String text,double x,double y,double size,int color,double width,double line){
        if(text.isEmpty())return 0;int rows=0;
        while(!text.isEmpty()){int end=text.length();while(end>1&&c.measure(text.substring(0,end),font(size))>width)end--;String part=text.substring(0,end);c.text(part,x-c.measure(part,font(size))/2,y+rows*line,font(size),color,false);text=text.substring(end);rows++;}return rows;
    }
    private static void map(Canvas c,double cx,double cy,double s,double time,boolean reduced,int accent,boolean light){
        double gap=7*s,cell=32*s;
        for(int y=-2;y<=2;y++)for(int x=-3;x<=3;x++){
            int distance=Math.abs(x)+Math.abs(y);double appear=reduced?1:Math.max(0,Math.min(1,(time-distance*.065)*2.2));
            double xx=cx+x*(cell+gap)-cell/2,yy=cy+y*(cell+gap)-cell/2+(1-appear)*18*s;
            int opacity=(int)((distance<2?15:34)*appear);c.gradient(xx,yy,cell,cell,alpha(accent,opacity),alpha(accent,opacity/2));
            int stroke=alpha(accent,(int)((distance<2?45:95)*appear));
            c.line(xx,yy,xx+cell,yy,1*s,stroke);c.line(xx,yy,xx,yy+cell,1*s,stroke);
            if(!reduced){double p=((time*.17+x*.09+y*.07)%1+1)%1;c.gradient(xx+2*s,yy+cell-3*s,Math.max(0,(cell-4*s)*p),1*s,alpha(accent,(int)(55*appear)),alpha(accent,(int)(55*appear)));}
        }
    }
    private static void network(Canvas c,double cx,double cy,double s,double time,boolean reduced,int accent,boolean light){
        double[][] points={{-160,-44},{-99,-101},{21,-121},{142,-62},{158,58},{49,114},{-89,95}};
        for(int i=0;i<points.length;i++){
            double x=cx+points[i][0]*s,y=cy+points[i][1]*s,dx=x-cx,dy=y-cy;
            c.line(cx,cy,x,y,1*s,alpha(accent,60));c.arc(x,y,5*s,2*s,0,Math.PI*2,alpha(accent,180));

        }
        c.arc(cx,cy,48*s,1*s,0,Math.PI*2,alpha(accent,85));
    }
}
