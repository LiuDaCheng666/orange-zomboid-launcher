package ozl.startup;

/** Accessed only after an actual accepted draw target. Operation time stays separate. */
public final class AnimationClock {
    private long epoch=Long.MIN_VALUE,origin;
    private String scene="";
    public double seconds(long operation,String context,long now){
        if(epoch!=operation||!scene.equals(context)){epoch=operation;scene=context;origin=now;}
        return Math.max(0,(now-origin)/1_000_000_000d);
    }
}
