package ozl.startup.tests;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.lang.reflect.*;
import java.util.*;
import java.util.concurrent.*;
import java.util.function.BiConsumer;

/** Only loopback UDP; exercises the owned production parser and isolated worker. */
public final class ServerQueryTest {
    static int checks;
    static Class<?> type;
    static void check(boolean b,String why){if(!b)throw new AssertionError(why);checks++;}
    static void str(ByteArrayOutputStream b,String s)throws Exception{b.write(s.getBytes(StandardCharsets.UTF_8));b.write(0);}
    static byte[] info(int players,int max)throws Exception{ByteArrayOutputStream b=new ByteArrayOutputStream();b.write(new byte[]{-1,-1,-1,-1,73,17});str(b,"Loopback server");str(b,"Rosewood");str(b,"projectzomboid");str(b,"Project Zomboid");b.write(0x38);b.write(0xa8);b.write(players);b.write(max);b.write(new byte[]{0,100,119,0,1});str(b,"42.21");return b.toByteArray();}
    static Object parse(byte[] p)throws Exception{return type.getMethod("parse",byte[].class).invoke(null,(Object)p);}
    static Object field(Object o,String name)throws Exception{return o.getClass().getField(name).get(o);}
    static Object worker(BiConsumer<Long,Object> callback)throws Exception{return type.getConstructor(BiConsumer.class).newInstance(callback);}
    static void request(Object q,long generation,int port)throws Exception{type.getMethod("request",long.class,String.class,int.class).invoke(q,generation,"127.0.0.1",port);}
    static void cancel(Object q)throws Exception{type.getMethod("cancel").invoke(q);}
    interface Responder {void reply(DatagramSocket s,DatagramPacket p)throws Exception;}
    static final class Loopback implements AutoCloseable {
        final DatagramSocket socket;final Thread thread;volatile Throwable failure;
        Loopback(Responder response)throws Exception{socket=new DatagramSocket(new InetSocketAddress("127.0.0.1",0));socket.setSoTimeout(4000);thread=new Thread(()->{try{DatagramPacket p=new DatagramPacket(new byte[4096],4096);socket.receive(p);response.reply(socket,p);}catch(Throwable t){if(!socket.isClosed())failure=t;}},"OZL test UDP");thread.setDaemon(true);thread.start();}
        int port(){return socket.getLocalPort();}
        public void close()throws Exception{socket.close();thread.join(500);if(failure!=null)throw new AssertionError("Loopback fixture failed",failure);}
    }
    static void send(DatagramSocket s,DatagramPacket p,byte[] bytes)throws Exception{s.send(new DatagramPacket(bytes,bytes.length,p.getSocketAddress()));}
    public static void main(String[] args)throws Exception{
        try{type=Class.forName("ozl.startup.ServerQuery");}catch(ClassNotFoundException missing){throw new AssertionError("Owned bounded server query missing");}
        byte[] packet=info(17,32);Object parsed=parse(packet);check(parsed!=null&&(int)field(parsed,"players")==17&&field(parsed,"map").equals("Rosewood"),"valid low16 app108600 A2S packet supplies real population/map");
        for(int i=0;i<packet.length;i++)check(parse(Arrays.copyOf(packet,i))==null,"truncated A2S packet rejected at "+i);
        byte[] bad=packet.clone();bad[0]=-2;check(parse(bad)==null,"split packets are rejected");
        bad=packet.clone();bad[6]=(byte)0xff;check(parse(bad)==null,"invalid UTF8 packet rejected");
        check(parse(info(33,32))==null,"impossible population rejected");
        ByteArrayOutputStream edf=new ByteArrayOutputStream();edf.write(packet);edf.write(1);long game=108600;for(int i=0;i<8;i++)edf.write((int)(game>>>(8*i))&255);check(parse(edf.toByteArray())!=null,"EDFID full app identity uses lower24 game-ID bits");
        byte[] wrong=edf.toByteArray();wrong[packet.length+1]=1;check(parse(wrong)==null,"wrong EDFID rejected");
        CountDownLatch received=new CountDownLatch(1);List<Long> generations=new CopyOnWriteArrayList<>();Object q=worker((g,r)->{generations.add(g);received.countDown();});
        try(Loopback server=new Loopback((s,p)->{check(p.getLength()==25&&p.getData()[4]==84,"request has exact A2S_INFO shape");send(s,p,new byte[]{-1,-1,-1,-1,65,1,2,3,4});DatagramPacket retry=new DatagramPacket(new byte[128],128);s.receive(retry);check(retry.getLength()==29&&retry.getData()[25]==1&&retry.getData()[28]==4,"one challenge token appended unchanged");send(s,retry,packet);})){
            request(q,7,server.port());check(received.await(3,TimeUnit.SECONDS)&&generations.equals(List.of(7L)),"ordinary/immediate join gets off-thread loopback public information");
        }
        cancel(q);CountDownLatch oldArrived=new CountDownLatch(1),release=new CountDownLatch(1),newArrived=new CountDownLatch(1);List<Long> late=new CopyOnWriteArrayList<>();Object current=worker((g,r)->{late.add(g);newArrived.countDown();});
        try(Loopback old=new Loopback((s,p)->{oldArrived.countDown();release.await(3,TimeUnit.SECONDS);send(s,p,packet);});Loopback fresh=new Loopback((s,p)->send(s,p,packet))){request(current,1,old.port());check(oldArrived.await(1,TimeUnit.SECONDS),"old query started");request(current,2,fresh.port());release.countDown();check(newArrived.await(2,TimeUnit.SECONDS)&&late.equals(List.of(2L)),"new request closes old work and suppresses late old reply");}
        cancel(current);CountDownLatch cancelArrived=new CountDownLatch(1);List<Long> cancelled=new CopyOnWriteArrayList<>();Object cancelledWorker=worker((g,r)->cancelled.add(g));
        try(Loopback server=new Loopback((s,p)->{cancelArrived.countDown();Thread.sleep(150);send(s,p,packet);})){request(cancelledWorker,9,server.port());check(cancelArrived.await(1,TimeUnit.SECONDS),"cancel query started");cancel(cancelledWorker);Thread.sleep(250);check(cancelled.isEmpty(),"cancelled socket cannot publish late response");}
        CountDownLatch timed=new CountDownLatch(1);List<Long> timeout=new CopyOnWriteArrayList<>();Object timeWorker=worker((g,r)->{timeout.add(g);timed.countDown();});
        try(Loopback blackhole=new Loopback((s,p)->{});Loopback after=new Loopback((s,p)->send(s,p,packet))){request(timeWorker,10,blackhole.port());Thread.sleep(2700);check(timeout.isEmpty(),"timeout invents no zero/default result");request(timeWorker,11,after.port());check(timed.await(2,TimeUnit.SECONDS)&&timeout.equals(List.of(11L)),"single daemon worker remains usable after timeout");}cancel(timeWorker);
        System.out.println("PASS "+checks+" bounded A2S parser/challenge/loopback/cancel/timeout contracts");
    }
}
