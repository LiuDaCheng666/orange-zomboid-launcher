package ozl.startup;

import java.util.*;

/** One layout shared by the actual GL canvas and offscreen visual checks. */
public final class LoadingScene {
    public interface Canvas {
        void gradient(double x,double y,double w,double h,int top,int bottom);
        void glow(double x,double y,double radius,int center,int edge);
        void arc(double x,double y,double radius,double thickness,double start,double length,int color);
        void logo(double x,double y,double size,double alpha);
        double measure(String text,double size);
        void text(String value,double x,double y,double size,int color,boolean right);
    }
    static int alpha(int color,int opacity){return (opacity<<24)|(color&0xffffff);}
    static String fit(Canvas c,String text,double size,double width){
        text=LoadingModel.safe(text,512);if(c.measure(text,size)<=width)return text;
        int end=text.length();while(end>0&&c.measure(text.substring(0,end)+"…",size)>width)end--;
        return end==0?"":text.substring(0,end)+"…";
    }
    public static void draw(Canvas c,int w,int h,LoadingModel.Snapshot state,double seconds,boolean light,boolean reduced,long used,long maximum) {
        double scale=Math.max(.65,Math.min(2.5,Math.min(w/1440d,h/900d)));
        int accent=0xffffbf78,text=light?0xff29343e:0xffe9edf2,muted=light?0xff66737c:0xff8f9da9;
        c.gradient(0,0,w,h,light?0xffeff1ec:0xff151e27,light?0xffdfe5df:0xff0a1017);
        double cx=w*.5,cy=h*.43;
        c.glow(cx,cy,310*scale,light?0x23e4ac62:0x18efa95b,0x00efa95b);
        double track=Math.min(w-96*scale,960*scale),x=(w-track)*.5,top=50*scale;
        c.text(state.title,x,top,15*scale,text,false);
        String count=state.processedScripts>0?"已读取 "+state.processedScripts+" 个脚本":state.processedMods>0?"已处理 "+state.processedMods+" 个模组":"";
        c.text(count,x+track,top,13*scale,muted,true);
        double gap=5*scale,cell=(track-gap*5)/6;
        for(int i=0;i<6;i++){
            int color=i<state.stageIndex?alpha(accent,185):i==state.stageIndex?accent:(light?0xffc7cfcb:0xff293744);
            double xx=x+i*(cell+gap);
            c.gradient(xx,top+16*scale,cell,4*scale,color,color);
            if(i==state.stageIndex&&!reduced){double p=(seconds*.35)%1;c.gradient(xx+p*Math.max(0,cell-22*scale),top+15*scale,22*scale,6*scale,0x80ffe8cb,0x80ffe8cb);}
        }
        double ratio=maximum>0?Math.max(0,Math.min(1,used/(double)maximum)):0;
        c.gradient(x,top+35*scale,track,2*scale,light?0xffd1d8d1:0xff24323c,light?0xffd1d8d1:0xff24323c);
        c.gradient(x,top+35*scale,track*ratio,2*scale,alpha(accent,155),alpha(accent,155));
        String heap=maximum>0?String.format(Locale.ROOT,"Java 内存  %.1f / %.1f GiB",used/1073741824d,maximum/1073741824d):"";
        c.text(heap,x+track,top+55*scale,12*scale,muted,true);
        double turn=reduced?-.5:seconds*.34;
        c.arc(cx,cy,107*scale,1.1*scale,0,Math.PI*2,light?0x38757e71:0x343e5261);
        for(int i=0;i<3;i++)c.arc(cx,cy,107*scale,2.4*scale,turn+i*Math.PI*2/3,.72,alpha(accent,190-i*45));
        c.arc(cx,cy,124*scale,1*scale,-turn*.6+.3,1.1,alpha(accent,65));
        double breath=reduced?1:1+Math.sin(seconds*1.65)*.016;
        c.logo(cx,cy,112*scale*breath,1);
        c.text("O Z L",cx-c.measure("O Z L",21*scale)/2,cy+164*scale,21*scale,text,false);
        String detail=fit(c,state.detail,13*scale,Math.min(660*scale,w*.65));
        if(!detail.isEmpty())c.text(detail,cx-c.measure(detail,13*scale)/2,cy+193*scale,13*scale,muted,false);
        double font=13*scale,line=23*scale,left=48*scale,bottom=h-45*scale;
        String[] activity=state.activity;int rows=Math.min(5,activity.length);
        for(int i=0;i<rows;i++){
            String value=fit(c,activity[activity.length-rows+i],font,w*.42-48*scale);
            c.text(value,left,bottom-(rows-1-i)*line,font,i==rows-1?text:muted,false);
        }
        if(state.issue.length==5){
            double right=w-48*scale,width=Math.min(460*scale,w*.36),y=bottom-4*line;
            c.text(fit(c,state.issue[0],font,width),right,y,font,accent,true);
            String[] values={state.issue[2].isEmpty()?"":("模组  ·  "+state.issue[2]),state.issue[1],state.issue[3],state.issue[4]};
            for(int i=0;i<values.length;i++)if(!values[i].isEmpty())c.text(fit(c,values[i],font,width),right,y+(i+1)*line,font,i==0?text:muted,true);
        }
    }
}
