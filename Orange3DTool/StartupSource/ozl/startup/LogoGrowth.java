package ozl.startup;

import java.awt.geom.AffineTransform;
import java.awt.image.BufferedImage;

/** Pure animation and image partition shared by GL and raster. Never reports progress. */
public final class LogoGrowth {
    public record Frame(double bodyScale,double leafScale,double angle,double leafAngle,double lift) {}
    private static double unit(double value){return Math.max(0,Math.min(1,value));}
    private static double smooth(double value){double t=unit(value);return t*t*(3-2*t);}
    private static double spring(double value){double t=unit(value)-1;return 1+2.4*t*t*t+1.4*t*t;}
    public static Frame at(double seconds,boolean reduced){
        if(reduced)return new Frame(1,1,0,0,0);
        double t=Double.isFinite(seconds)?Math.max(0,seconds)%8.4:0;
        double growth=spring((t-.35)/2.25),leaf=spring((t-1.0)/2.0),fold=smooth((t-6.4)/2.0);
        double idle=smooth((t-2.6)/.6)*(1-smooth((t-6.0)/.4));
        double breath=Math.sin((t-2.6)*Math.PI*2/2.2)*idle;
        double body=(.24+.76*growth+.022*breath)*(1-fold)+.24*fold;
        double unfolded=(.05+.95*leaf)*(1-fold)+.05*fold;
        double grown=unit(growth)*(1-fold);
        return new Frame(body,unfolded,-.12*(1-grown)+.025*breath,
            -.95*(1-unit(leaf)*(1-fold))+.075*breath,
            .12*(1-grown)-.018*breath);
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
        AffineTransform t=new AffineTransform();t.translate(x,y);t.rotate(angle);t.scale(size,size);t.translate(-.5,-.5);
        if(leaf){t.translate(.70,.24);t.rotate(leafAngle);t.scale(leafScale,leafScale);t.translate(-.70,-.24);}
        return t;
    }
}
