package ozl.startup;
import java.lang.reflect.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.*;
import java.util.function.BiFunction;

/** Blocking I/O boundary only is replaced; results and the production sampler run for real. */
public final class IssueSamplerTest {
 static int checks;static Class<?> type;static Method latest,close;
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 static Object sampler(BiFunction<Long,Long,String[]> source)throws Exception{var constructor=type.getDeclaredConstructor(BiFunction.class);constructor.setAccessible(true);return constructor.newInstance(source);}
 static String[] read(Object sampler,long now,long floor,long operation)throws Exception{return (String[])latest.invoke(sampler,now,floor,operation);}
 static void end(Object sampler)throws Exception{close.invoke(sampler);}
 static String[] issue(String title){return new String[]{title,"meaning","mod","frame","evidence"};}
 static boolean title(String[] value,String wanted){return value.length==5&&value[0].equals(wanted);}
 static boolean awaitTitle(Object value,long now,long floor,long operation,String wanted)throws Exception{long until=System.nanoTime()+TimeUnit.SECONDS.toNanos(3);do{if(title(read(value,now,floor,operation),wanted))return true;Thread.sleep(2);}while(System.nanoTime()<until);return false;}
 public static void main(String[] args)throws Exception{
  try{type=Class.forName("ozl.startup.IssueSampler");}catch(ClassNotFoundException absent){throw new AssertionError("Issue-file reads still lack a nonblocking render-thread sampler",absent);}
  latest=type.getDeclaredMethod("latest",long.class,long.class,long.class);latest.setAccessible(true);close=type.getDeclaredMethod("close");close.setAccessible(true);
  CountDownLatch entered=new CountDownLatch(1),release=new CountDownLatch(1),newEntered=new CountDownLatch(1),newRelease=new CountDownLatch(1);AtomicInteger oldCalls=new AtomicInteger();
  Object blocked=sampler((now,floor)->{oldCalls.incrementAndGet();try{if(floor==0){entered.countDown();release.await();}else{newEntered.countDown();newRelease.await();}}catch(InterruptedException ignored){}return issue(floor==0?"old connection":"new connection");});
  try{
   check(read(blocked,1000,0,1).length==0,"first request returns immediately while data is unavailable");check(entered.await(3,TimeUnit.SECONDS),"owned worker started the read");
   long began=System.nanoTime();for(int i=0;i<180;i++)check(read(blocked,1000,0,1).length==0,"render reads never wait for blocked issue I/O");
   check(System.nanoTime()-began<TimeUnit.MILLISECONDS.toNanos(200),"180 render reads stay responsive while disk is blocked");
   check(read(blocked,1100,50,2).length==0,"new connection immediately clears old cached evidence");release.countDown();
   check(newEntered.await(3,TimeUnit.SECONDS),"new reader starts only after the old read completes");for(int i=0;i<100;i++)check(read(blocked,1100,50,2).length==0,"old completion is never visible while newer connection read remains blocked");newRelease.countDown();
   check(awaitTitle(blocked,1100,50,2,"new connection"),"late old-connection read cannot replace the new connection result");
  }finally{release.countDown();newRelease.countDown();end(blocked);}
  AtomicInteger calls=new AtomicInteger();Object regular=sampler((now,floor)->issue("result "+calls.incrementAndGet()));
  try{
   check(awaitTitle(regular,1000,0,1,"result 1"),"completed actual reader result becomes visible");
   for(long now=1000;now<1250;now++)check(title(read(regular,now,0,1),"result 1"),"cached result remains available inside sampling interval");
   check(calls.get()==1,"frames do not trigger repeated file reads");
   read(regular,1250,0,1);check(awaitTitle(regular,1250,0,1,"result 2"),"next sampling interval refreshes the result");
   check(calls.get()==2,"refresh schedules one read");
  }finally{end(regular);}
  AtomicInteger failures=new AtomicInteger();Object recover=sampler((now,floor)->{if(failures.incrementAndGet()==1)throw new IllegalStateException("read failed");return issue("recovered");});
  try{read(recover,1000,0,1);long until=System.nanoTime()+TimeUnit.SECONDS.toNanos(3);while(failures.get()==0&&System.nanoTime()<until)Thread.sleep(2);check(failures.get()==1,"failure path executes reader");check(awaitTitle(recover,1250,0,1,"recovered"),"read failure does not disable later updates or drawing");}finally{end(recover);}
  CountDownLatch hold=new CountDownLatch(1),closingRead=new CountDownLatch(1);AtomicReference<Thread> closingWorker=new AtomicReference<>();Object closing=sampler((now,floor)->{closingWorker.set(Thread.currentThread());closingRead.countDown();boolean done=false;while(!done)try{hold.await();done=true;}catch(InterruptedException ignored){}return issue("closed");});
  try{read(closing,1000,0,1);check(closingRead.await(3,TimeUnit.SECONDS),"close path has an active blocked reader");long began=System.nanoTime();end(closing);check(System.nanoTime()-began<TimeUnit.MILLISECONDS.toNanos(200),"close never joins blocked I/O on caller thread");check(read(closing,1500,0,1).length==0,"closed sampler cannot restart or publish stale results");}finally{hold.countDown();end(closing);}
  closingWorker.get().join(1000);check(!closingWorker.get().isAlive(),"closed worker exits after its in-flight read finishes");
  System.out.println("PASS "+checks+" nonblocking issue sampling, lifecycle and refresh contracts");
 }
}
