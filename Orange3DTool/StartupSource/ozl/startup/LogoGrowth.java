package ozl.startup;

import java.awt.geom.AffineTransform;
import java.awt.image.BufferedImage;

/** Pure animation and image partition shared by GL and raster. Never reports progress. */
public final class LogoGrowth {
    public record Frame(double bodyScale,double leafScale,double angle,double leafAngle) {}
    public static Frame at(double seconds,boolean reduced){
        if(reduced)return new Frame(1,1,0,0);
        double t=Math.max(0,seconds),entrance=Math.min(1,t/1.4);
        entrance=entrance*entrance*(3-2*entrance);
        double phase=2*Math.PI*t/8.4;
        double grow=(1-Math.cos(phase))/2,unfold=(1-Math.cos(phase-.7))/2;
        return new Frame((.32+.68*entrance)*(.80+.20*grow),.48+.52*unfold,
            .035*Math.sin(phase),-.20*(1-unfold));
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
