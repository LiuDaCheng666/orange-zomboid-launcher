package ozl.startup;

/** Decorative waiting captions. This clock never changes observed loading state. */
final class LoadingHumor {
    private static final String[] PHRASES={"狗正在跨越栅栏","正在摧毁模组","正在添加bug","正在鞭策浣熊","正在给僵尸系鞋带","正在说服直升机绕路"};
    record Caption(String text,int opacity) {}
    static Caption at(double seconds,long operation,LoadingModel.Context context,boolean reduced){
        int seed=Math.floorMod(operation*31+context.ordinal()*17,42);
        if(reduced)return new Caption(PHRASES[seed%PHRASES.length],155);
        if(!Double.isFinite(seconds))return new Caption("",0);
        double elapsed=Math.max(0,seconds)-(6+seed%2);
        if(elapsed<0)return new Caption("",0);
        // Seven different quiet intervals form a bounded repeating schedule.
        long block=(long)Math.floor(elapsed/133);elapsed%=133;
        int slot=0;
        while(slot<7){double period=4+12+Math.floorMod(seed+slot*5,7);if(elapsed<period)break;elapsed-=period;slot++;}
        if(elapsed>=4)return new Caption("",0);
        double fade=Math.min(1,Math.min(elapsed/.45,(4-elapsed)/.45));
        int opacity=(int)Math.round(fade*12)*13;
        if(opacity==0)return new Caption("",0);
        return new Caption(PHRASES[Math.floorMod(seed+block*7+slot,PHRASES.length)],opacity);
    }
}
