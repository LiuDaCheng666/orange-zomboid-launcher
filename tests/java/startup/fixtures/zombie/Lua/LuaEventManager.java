package zombie.Lua;
public final class LuaEventManager {
    public static int calls;public static Object last;
    public static java.util.function.BiConsumer<String,Object[]> original;
    static void dispatch(String e,Object... values){calls++;if(values.length>0)last=values[values.length-1];if(original!=null)original.accept(e,values);}
    public static void triggerEvent(String e){dispatch(e);}
    public static void triggerEvent(String e,Object a){dispatch(e,a);}
    public static void triggerEvent(String e,Object a,Object b){dispatch(e,a,b);}
    public static void triggerEvent(String e,Object a,Object b,Object c){dispatch(e,a,b,c);}
    public static void triggerEvent(String e,Object a,Object b,Object c,Object d){dispatch(e,a,b,c,d);}
}
