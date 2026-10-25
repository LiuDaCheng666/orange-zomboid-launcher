package ozl.startup;

import java.net.*;
import java.nio.*;
import java.nio.charset.*;
import java.util.*;
import java.util.function.BiConsumer;

/** Isolated one-shot public query: one daemon, one pending target, one owned socket. */
public final class ServerQuery {
    public static final class Result {
        public final String name,map;public final int players,maximum;
        Result(String name,String map,int players,int maximum){this.name=name;this.map=map;this.players=players;this.maximum=maximum;}
    }
    private record Request(long serial,long generation,String host,int port){}
    private final Object gate=new Object();
    private final BiConsumer<Long,Result> sink;
    private long serial;
    private Request pending;
    private DatagramSocket active;
    private Thread worker;
    private static final byte[] REQUEST=requestBytes();
    public ServerQuery(BiConsumer<Long,Result> sink){this.sink=sink;}
    private static byte[] requestBytes(){byte[] text="Source Engine Query\0".getBytes(StandardCharsets.US_ASCII),bytes=new byte[text.length+5];Arrays.fill(bytes,0,4,(byte)255);bytes[4]=84;System.arraycopy(text,0,bytes,5,text.length);return bytes;}
    public void request(long generation,String host,int port){
        if(host==null||host.isEmpty()||host.length()>253||!host.matches("[a-zA-Z0-9._:%-]+")||port<1||port>65535)return;
        synchronized(gate){serial++;pending=new Request(serial,generation,host,port);if(active!=null)active.close();if(worker==null){worker=new Thread(this::run,"OZL public server information");worker.setDaemon(true);worker.start();}gate.notifyAll();}
    }
    public void cancel(){synchronized(gate){serial++;pending=null;if(active!=null)active.close();}}
    private boolean current(Request request){synchronized(gate){return serial==request.serial;}}
    private void run(){
        for(;;){Request request;synchronized(gate){while(pending==null)try{gate.wait();}catch(InterruptedException ignored){}request=pending;pending=null;}
            Result result=query(request);if(result!=null)synchronized(gate){if(serial==request.serial)try{sink.accept(request.generation,result);}catch(Throwable ownFailure){}}
        }
    }
    private static InetAddress address(String host)throws Exception{
        if(host.matches("[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+")){String[] parts=host.split("\\.");byte[] value=new byte[4];for(int i=0;i<4;i++){int part=Integer.parseInt(parts[i]);if(part<0||part>255)throw new IllegalArgumentException();value[i]=(byte)part;}return InetAddress.getByAddress(value);}
        // DNS runs on this same bounded worker, never a new per-request executor.
        InetAddress[] resolved=InetAddress.getAllByName(host);for(InetAddress value:resolved)if(value instanceof Inet4Address)return value;return resolved[0];
    }
    private Result query(Request request){
        long deadline=System.nanoTime()+2_500_000_000L;DatagramSocket socket=null;
        try{InetAddress address=address(request.host);if(!current(request)||System.nanoTime()>=deadline)return null;
            synchronized(gate){if(serial!=request.serial)return null;socket=new DatagramSocket();socket.connect(address,request.port);socket.setSoTimeout(100);active=socket;}
            socket.send(new DatagramPacket(REQUEST,REQUEST.length));boolean challenged=false;byte[] bytes=new byte[65507];
            while(current(request)&&System.nanoTime()<deadline){DatagramPacket incoming=new DatagramPacket(bytes,bytes.length);try{socket.receive(incoming);}catch(SocketTimeoutException waiting){continue;}
                int size=incoming.getLength();if(size==9&&bytes[0]==-1&&bytes[1]==-1&&bytes[2]==-1&&bytes[3]==-1&&bytes[4]==65&&!challenged){challenged=true;byte[] retry=Arrays.copyOf(REQUEST,REQUEST.length+4);System.arraycopy(bytes,5,retry,REQUEST.length,4);socket.send(new DatagramPacket(retry,retry.length));continue;}
                return parse(Arrays.copyOf(bytes,size));
            }
        }catch(Exception unavailable){}finally{if(socket!=null)socket.close();synchronized(gate){if(active==socket)active=null;}}
        return null;
    }
    private static final class Packet {
        final ByteBuffer buffer;
        Packet(byte[] data){buffer=ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN);buffer.position(5);}
        int byteValue(){return buffer.get()&255;}int shortValue(){return buffer.getShort()&65535;}
        void skip(int count){if(buffer.remaining()<count)throw new IllegalArgumentException();buffer.position(buffer.position()+count);}
        String text()throws CharacterCodingException{int start=buffer.position(),length=0;while(buffer.hasRemaining()){if(buffer.get()==0){byte[] value=new byte[length];int end=buffer.position();buffer.position(start);buffer.get(value);buffer.position(end);return StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(value)).toString();}if(++length>2048)throw new IllegalArgumentException();}throw new IllegalArgumentException();}
    }
    public static Result parse(byte[] bytes){
        try{if(bytes==null||bytes.length<6||bytes.length>65507||bytes[0]!=-1||bytes[1]!=-1||bytes[2]!=-1||bytes[3]!=-1||bytes[4]!=73)return null;
            Packet p=new Packet(bytes);p.byteValue();String name=p.text(),map=p.text(),folder=p.text(),game=p.text();int app=p.shortValue();
            if(app!=(108600&65535)||(!folder.equalsIgnoreCase("projectzomboid")&&!game.equalsIgnoreCase("Project Zomboid")))return null;
            int players=p.byteValue(),maximum=p.byteValue();if(maximum==0||players>maximum)return null;p.skip(5);p.text();
            if(p.buffer.hasRemaining()){int flags=p.byteValue();if((flags&14)!=0)return null;if((flags&128)!=0)p.skip(2);if((flags&16)!=0)p.skip(8);if((flags&64)!=0){p.skip(2);p.text();}if((flags&32)!=0)p.text();if((flags&1)!=0&&(p.buffer.getLong()&0xffffffL)!=108600)return null;if(p.buffer.hasRemaining())return null;}
            return new Result(LoadingModel.safe(name,96),LoadingModel.safe(map,120),players,maximum);
        }catch(Exception malformed){return null;}
    }
}
