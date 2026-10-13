package orange.ads;
public final class ReticleModeTest {
 public static void main(String[] args)throws Exception{
  Class<?> type=Class.forName("orange.ads.ReticleMode");Object m=type.getConstructor(boolean.class).newInstance(true);var poll=type.getMethod("poll",boolean.class,boolean.class);var nativeMode=type.getMethod("nativeMode");
  check((boolean)nativeMode.invoke(m),"default native mode");check((boolean)poll.invoke(m,true,true),"press switches once");check(!(boolean)nativeMode.invoke(m),"extension after press");check(!(boolean)poll.invoke(m,true,true),"held key does not repeat");poll.invoke(m,false,true);check((boolean)poll.invoke(m,true,true)&&(boolean)nativeMode.invoke(m),"second press returns native");poll.invoke(m,false,true);check(!(boolean)poll.invoke(m,true,false)&&(boolean)nativeMode.invoke(m),"typing blocked");check(!(boolean)poll.invoke(m,true,true),"typing-held key cannot trigger after focus changes");System.out.println("RETICLE MODE TESTS PASS");
 }
 static void check(boolean ok,String label){if(!ok)throw new AssertionError(label);System.out.println("PASS: "+label);}
}
