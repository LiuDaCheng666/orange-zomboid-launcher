package orange.ads;
public final class ReticleMode {
 private boolean nativeMode,held;
 public ReticleMode(boolean startNative){nativeMode=startNative;}
 public boolean nativeMode(){return nativeMode;}
 public boolean poll(boolean down,boolean allowed){boolean pressed=down&&!held;held=down;if(pressed&&allowed){nativeMode=!nativeMode;return true;}return false;}
}
