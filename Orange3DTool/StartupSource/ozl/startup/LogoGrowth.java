package ozl.startup;

import java.awt.geom.AffineTransform;
import java.awt.image.BufferedImage;

/** Pure animation and image partition shared by GL and raster. Never reports progress. */
public final class LogoGrowth {
    public record Frame(double bodyScale,double leafScale,double angle,double leafAngle,double lift,double stretchX,double stretchY) {}
    private static double unit(double value){return Math.max(0,Math.min(1,value));}
    private static double smooth(double value){double t=unit(value);return t*t*(3-2*t);}
    private static double spring(double value){double t=unit(value)-1;return 1+2.4*t*t*t+1.4*t*t;}
    public static Frame at(double seconds,boolean reduced){return at(seconds,reduced,LoadingModel.Context.BOOT,false);}
    public static Frame at(double seconds,boolean reduced,LoadingModel.Context context,boolean download){
        if(reduced)return new Frame(1,1,0,0,0,1,1);
        double t=Double.isFinite(seconds)?Math.max(0,seconds):0;
        double cycle=t%18,angle=cycle<6.6?Math.PI*2*smooth((cycle-1)/4.8):0,leafAngle=0,lift=0;
        if(download){double p=(t%6)/6;leafAngle=.15*Math.sin(Math.PI*p*2)*Math.pow(Math.sin(Math.PI*p),2);}
        else if(context==LoadingModel.Context.SERVER){int direction=new int[]{-1,-1,0,1,1,0,-1}[(int)(t/2.8)%7];double p=(t%2.8)/2.8,wave=Math.pow(Math.sin(Math.PI*p),2);angle+=direction*.065*wave;leafAngle=-direction*.11*wave;lift=-1.5/164*wave;}
        else if(context==LoadingModel.Context.SAVE){double p=t%7,dy=0;if(p<.35)dy=6*(1-Math.cos(Math.PI*p/.35))/2;else if(p<1.05){double v=(p-.35)/.7;dy=6*(1-v)-3*Math.sin(Math.PI*v);}else if(p<1.6)dy=1.5*Math.sin((p-1.05)/.55*Math.PI);lift=dy/164;leafAngle=-.02*dy;}
        else if(cycle>=7&&cycle<8.5){double p=(cycle-7)/1.5;lift=-9d/174*Math.sin(Math.PI*p);leafAngle=-.22*Math.sin(Math.PI*p);angle+=.025*Math.sin(p*Math.PI*2);}
        else if(cycle>=10&&cycle<13){double wave=Math.pow(Math.sin(Math.PI*(cycle-10)/3),2);angle+=.06*wave;leafAngle=-.12*wave;}
        return new Frame(1,1,angle,leafAngle,lift,1,1);
    }
    public record Parts(BufferedImage body,BufferedImage leaf) {}
    public static Parts split(BufferedImage source){
        int w=source.getWidth(),h=source.getHeight();
        BufferedImage body=new BufferedImage(w,h,BufferedImage.TYPE_INT_ARGB),leaf=new BufferedImage(w,h,BufferedImage.TYPE_INT_ARGB);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            int p=source.getRGB(x,y),r=(p>>16)&255,g=(p>>8)&255,b=p&255;
            boolean isLeaf=x>w*.65&&y<h*.27&&g>r&&g>b;
            (isLeaf?leaf:body).setRGB(x,y,p);
        }
        return new Parts(body,leaf);
    }
    /** Maps the unit image square to pixels, hinging the leaf at its actual base. */
    public static AffineTransform transform(double x,double y,double size,double angle,double leafScale,double leafAngle,boolean leaf){
        return transform(x,y,size,angle,leafScale,leafAngle,leaf,1,1);
    }
    public static AffineTransform transform(double x,double y,double size,double angle,double leafScale,double leafAngle,boolean leaf,double stretchX,double stretchY){
        AffineTransform t=new AffineTransform();t.translate(x,y);t.rotate(angle);t.scale(size*stretchX,size*stretchY);t.translate(-.5,-.5);
        if(leaf){t.translate(.70,.24);t.rotate(leafAngle);t.scale(leafScale,leafScale);t.translate(-.70,-.24);}
        return t;
    }
}
