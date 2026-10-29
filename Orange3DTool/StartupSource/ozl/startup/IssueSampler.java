package ozl.startup;

import java.util.function.BiFunction;

/** Demand-driven file observations. The render caller never performs or waits for I/O. */
final class IssueSampler implements AutoCloseable {
 private static final String[] EMPTY=new String[0];
 private record Request(long epoch,long now,long floor) {}
 private final Object gate=new Object();
 private final BiFunction<Long,Long,String[]> source;
 private Thread worker;
 private Request pending;
 private String[] value=EMPTY;
 private boolean closed,requested;
 private long epoch,operation=Long.MIN_VALUE,floor=Long.MIN_VALUE,requestedAt;
 IssueSampler(BiFunction<Long,Long,String[]> source){this.source=source;}
 String[] latest(long nowMillis,long newerThan,long currentOperation){
  synchronized(gate){
   if(closed)return EMPTY;
   if(operation!=currentOperation||floor!=newerThan){operation=currentOperation;floor=newerThan;epoch++;requested=false;value=EMPTY;}
   if(!requested||nowMillis<requestedAt||nowMillis-requestedAt>=250){
    requested=true;requestedAt=nowMillis;pending=new Request(epoch,nowMillis,newerThan);
    if(worker==null){worker=new Thread(this::run,"OZL loading issue reader");worker.setDaemon(true);worker.start();}
    gate.notifyAll();
   }
   return value;
  }
 }
 private void run(){
  for(;;){
   Request request;
   synchronized(gate){
    while(!closed&&pending==null)try{gate.wait();}catch(InterruptedException ignored){}
    if(closed)return;request=pending;pending=null;
   }
   String[] result;
   try{result=source.apply(request.now,request.floor);if(result==null)result=EMPTY;}catch(Exception unavailable){result=EMPTY;}
   synchronized(gate){if(!closed&&epoch==request.epoch)value=result;}
  }
 }
 @Override public void close(){
  synchronized(gate){if(closed)return;closed=true;epoch++;pending=null;value=EMPTY;gate.notifyAll();}
 }
}
