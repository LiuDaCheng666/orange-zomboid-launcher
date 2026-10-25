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
        boolean nativeControls=state.context==LoadingModel.Context.SERVER&&statusBottom>0&&cancelTop>statusBottom;
        if(nativeControls)light=false; // Original native status is white and remains authoritative.
        double sceneScale=Math.max(.6,Math.min(2.5,Math.min(w/1440d,h/900d))),s=nativeControls?Math.min(1.3,sceneScale):sceneScale,margin=Math.max(22,48*s);
        int accent=switch(state.context){case BOOT, NONE -> 0xffffbd76;case SERVER -> 0xff67d8f0;case SAVE -> 0xff73dfa8;},ink=light?0xff29343e:0xffe9edf2,muted=light?0xff52616a:0xffa4b5c1;
        int topColor=switch(state.context){case BOOT, NONE -> light?0xfff6eee3:0xff2b201b;case SERVER -> light?0xffe7f2f8:0xff0c263d;case SAVE -> light?0xffe7f3e9:0xff122f27;};
        int bottomColor=switch(state.context){case BOOT, NONE -> light?0xffe8ddd1:0xff130f10;case SERVER -> light?0xffd0e4ee:0xff07121f;case SAVE -> light?0xffd3e5d6:0xff071812;};
        c.gradient(0,0,w,h,topColor,bottomColor);
        double track=w-2*margin,top=Math.max(28,50*s),titleSize=font(15*s),small=font(12*s);
        text(c,state.title,margin,top,titleSize,ink,false,track*.58);
        String count=state.context==LoadingModel.Context.BOOT?(state.processedScripts>0?"已读取 "+state.processedScripts+" 个脚本":state.processedMods>0?"已处理 "+state.processedMods+" 个模组":state.scannedResources>0?"已索引 "+state.scannedResources+" 项资源":""):"";
        text(c,count,w-margin,top,small,muted,true,track*.4);
        int stages=state.context==LoadingModel.Context.SAVE?3:6;
        double gap=5*s,cell=(track-gap*(stages-1))/stages,stageY=top+16*s;
        for(int i=0;i<stages;i++){
            int color=i<state.stageIndex?alpha(accent,165):i==state.stageIndex?accent:(light?0xffc7cfcb:0xff293744);
            c.gradient(margin+i*(cell+gap),stageY,cell,Math.max(2,4*s),color,color);
            if(i==state.stageIndex&&!reduced){double p=(seconds*.35)%1;c.gradient(margin+i*(cell+gap)+p*Math.max(0,cell-22*s),stageY-s,22*s,Math.max(3,6*s),0x80ffe8cb,0x80ffe8cb);}
        }
        if(maximum>0){
            double ratio=Math.max(0,Math.min(1,used/(double)maximum)),heapY=top+35*s;
            c.gradient(margin,heapY,track,Math.max(1,2*s),light?0xffd1d8d1:0xff24323c,light?0xffd1d8d1:0xff24323c);
            c.gradient(margin,heapY,track*ratio,Math.max(1,2*s),alpha(accent,155),alpha(accent,155));
            String heap=String.format(Locale.ROOT,"Java 内存  %,d / %,d MiB",Math.max(0,used)>>20,maximum>>20);text(c,heap,w-margin,top+55*s,small,muted,true,track);
        }
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
            double orbit=reduced?-.5:(t*.22)%(Math.PI*2);
            for(int i=0;i<3;i++)c.arc(cx,cy,107*s,2.4*s,orbit+i*Math.PI*2/3,.72,alpha(accent,190-i*45));
            c.arc(cx,cy,124*s,1*s,-orbit*.6+.3,1.1,alpha(accent,65));
        }
        double logoSize=(state.context==LoadingModel.Context.BOOT?174:state.context==LoadingModel.Context.SAVE?130:150)*visualScale*growth.bodyScale();
        c.growingLogo(cx,cy,logoSize,1,growth.angle(),growth.leafScale(),growth.leafAngle());
        String identity=state.context==LoadingModel.Context.BOOT?"O Z L":state.name;
        double infoCx=state.context==LoadingModel.Context.BOOT||nativeControls?w*.5:w*.68,infoWidth=state.context==LoadingModel.Context.BOOT||nativeControls?w*.84:w*.49;
        double labelY=nativeControls?top+88*s:state.context==LoadingModel.Context.BOOT?cy+157*s:h*.31;
        if(!identity.isEmpty())centered(c,identity,infoCx,labelY,font(21*s),ink,infoWidth);
        String mode=switch(state.mode){case "Sandbox" -> "沙盒";case "Survival" -> "生存";default -> state.mode;};
        String metadata=String.join("  ·  ",java.util.stream.Stream.of(mode,state.map,state.context==LoadingModel.Context.SERVER&&state.requestedMods>=0?state.requestedMods+" 个模组":"").filter(v->!v.isEmpty()).toList());
        if(state.context==LoadingModel.Context.SERVER&&state.workshopProjects>=0)metadata=(metadata.isEmpty()?"":metadata+"  ·  ")+state.workshopProjects+" 个创意工坊项目";
        double infoSize=state.context==LoadingModel.Context.SERVER?Math.max(12,small):small,infoLine=Math.max(18,23*s),infoY=labelY+(state.context==LoadingModel.Context.SERVER?Math.max(20,25*s):25*s);
        centered(c,metadata,infoCx,infoY,infoSize,muted,infoWidth);
        double detailY=state.context==LoadingModel.Context.SERVER?infoY+3*infoLine:labelY+48*s;
        if(state.context==LoadingModel.Context.SERVER){
            String online=state.online>=0?(state.maxPlayers>0?"在线 "+state.online+" / "+state.maxPlayers:"在线 "+state.online):state.maxPlayers>0?"上限 "+state.maxPlayers+" 人":"";
            int endpointLines=wrapped(c,state.endpoint,infoCx,infoY+infoLine,infoSize,ink,infoWidth,infoLine);
            String game=state.gameDate.isEmpty()&&state.gameClock.isEmpty()?"":"游戏 "+state.gameDate+(state.gameDate.isEmpty()||state.gameClock.isEmpty()?"":"  ")+state.gameClock;
            if(state.worldDay>0)game+=(game.isEmpty()?"":"  ·  ")+"世界第 "+state.worldDay+" 天";
            centered(c,online,infoCx,infoY+(endpointLines+1)*infoLine,infoSize,muted,infoWidth);
            int gameLines=wrapped(c,game,infoCx,infoY+(endpointLines+2)*infoLine,infoSize,muted,infoWidth,infoLine);
            detailY=infoY+(endpointLines+2+gameLines)*infoLine;
        }
        String detail=state.context==LoadingModel.Context.BOOT?state.stage:state.detail;
        if(state.downloaded>=0&&state.downloadTotal>0)detail=String.format(Locale.ROOT,"创意工坊下载  %.2f / %.2f MiB",state.downloaded/1048576d,state.downloadTotal/1048576d);
        // The original connectLabel occupies its own lane; do not duplicate it above the native text.
        if(!nativeControls||"排队".equals(state.stage))centered(c,detail,infoCx,detailY,infoSize,muted,infoWidth);
        double line=Math.max(16,23*s),bottom=h-Math.max(23,45*s),body=font(13*s),leftWidth=w*.46-margin,rightWidth=w*.42-margin;
        if(nativeControls){leftWidth=Math.min(leftWidth,w*.5-75-margin);rightWidth=Math.min(rightWidth,w*.5-75-margin);}
        int rows=Math.min(5,state.activity.length);
        for(int i=0;i<rows;i++)text(c,state.activity[state.activity.length-rows+i],margin,bottom-(rows-1-i)*line,body,i==rows-1?ink:muted,false,leftWidth);
        if(state.issue.length==5){double right=w-margin,y=bottom-4*line;text(c,state.issue[0],right,y,body,accent,true,rightWidth);
            String[] values={state.issue[2].isEmpty()?"":"模组  ·  "+state.issue[2],state.issue[1],state.issue[3],state.issue[4]};
            for(int i=0;i<values.length;i++)text(c,values[i],right,y+(i+1)*line,body,i==0?ink:muted,true,rightWidth);
        }
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
            if(!reduced){double p=((time*.2+i*.14)%1+1)%1;c.glow(cx+dx*p,cy+dy*p,4*s,alpha(accent,200),alpha(accent,0));}
        }
        c.arc(cx,cy,48*s,1*s,0,Math.PI*2,alpha(accent,85));
    }
}
