package zombie.Lua;
public final class LuaManager {
    public static se.krka.kahlua.vm.KahluaTable env;
    public static StackTraceElement[] getLuaStackStrace(se.krka.kahlua.vm.Coroutine value){try{Object[] frames=(Object[])value.getClass().getMethod("getCallframeStack").invoke(value);java.util.ArrayList<StackTraceElement> stack=new java.util.ArrayList<>();for(Object f:frames)if(f!=null){Object c=f.getClass().getField("closure").get(f);if(c==null)continue;Object p=c.getClass().getField("prototype").get(c);int[] lines=(int[])p.getClass().getField("lines").get(p);int pc=(int)f.getClass().getField("pc").get(f);stack.add(new StackTraceElement("lua","fixture","extracted",lines[Math.max(0,pc-1)]));}return stack.toArray(StackTraceElement[]::new);}catch(Exception e){throw new AssertionError(e);}}
    public static Object RunLua(String file,boolean rewrite){return "original-return";}
}
