package ozl.startup;

import java.awt.geom.AffineTransform;
import java.awt.image.BufferedImage;

/** Pure animation and image partition shared by GL and raster. Never reports progress. */
public final class LogoGrowth {
    public record Frame(double bodyScale,double leafScale,double angle,double leafAngle,double lift,double stretchX,double stretchY) {}
    private static double unit(double value){return Math.max(0,Math.min(1,value));}
    private static double smooth(double value){double t=unit(value);return t*t*(3-2*t);}
    private static double spring(double value){double t=unit(value)-1;return 1+2.4*t*t*t+1.4*t*t;}
    public static Frame at(double seconds,boolean reduced){
        if(reduced)return new Frame(1,1,0,0,0,1,1);
        double t=Double.isFinite(seconds)?Math.max(0,seconds):0;
        if(t<3){
            double growth=spring((t-.2)/1.9),leaf=spring((t-.7)/1.8);
            return new Frame(.24+.76*growth,.05+.95*leaf,-.12*(1-unit(growth)),
                -.95*(1-unit(leaf)),.12*(1-unit(growth)),1,1);
        }
        // A quiet beat separates four deliberate gestures. The fruit never becomes a seed again.
        double cycle=(t-3)%30,angle=0,leafAngle=0,lift=0,sx=1,sy=1;
        if(cycle>=1.0&&cycle<6.0){
            double u=(cycle-1)/5,envelope=Math.pow(Math.sin(Math.PI*u),2);
            angle=Math.PI*2*smooth(u);lift=-.007*envelope;
        }else if(cycle>=6&&cycle<6.6){
            double u=(cycle-6)/.6;angle=.025*Math.sin(2*Math.PI*u)*(1-u);
        }else if(cycle>=8&&cycle<12){
            double u=(cycle-8)/4,envelope=Math.pow(Math.sin(Math.PI*u),2);
            leafAngle=.55*Math.sin(4*Math.PI*u)*envelope;angle=-.018*envelope;
        }else if(cycle>=15&&cycle<18.2){
            double u=(cycle-15)/3.2;
            if(u<.24){double squash=Math.pow(Math.sin(Math.PI*u/.24),2);sx+=.06*squash;sy-=.07*squash;lift=.016*squash;}
            else if(u<.76){double hop=Math.sin(Math.PI*(u-.24)/.52);lift=-.105*hop;angle=.045*Math.sin(2*Math.PI*(u-.24)/.52);sx-=.025*hop;sy+=.035*hop;leafAngle=-.16*hop;}
            else{double settle=Math.sin(Math.PI*(u-.76)/.24);sx+=.025*settle;sy-=.028*settle;lift=.007*settle;}
        }else if(cycle>=21&&cycle<26){
            double u=(cycle-21)/5,envelope=Math.pow(Math.sin(Math.PI*u),2);
            angle=-.08*envelope;leafAngle=.14*envelope;lift=-.009*envelope;
        }
        return new Frame(1,1,angle,leafAngle,lift,sx,sy);
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
