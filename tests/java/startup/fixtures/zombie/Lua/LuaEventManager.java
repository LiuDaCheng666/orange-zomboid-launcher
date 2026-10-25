package zombie.Lua;
public final class LuaEventManager {
    public static int calls;public static Object last;
    public static void triggerEvent(String e){calls++;}
    public static void triggerEvent(String e,Object a){calls++;last=a;}
    public static void triggerEvent(String e,Object a,Object b){calls++;last=b;}
    public static void triggerEvent(String e,Object a,Object b,Object c){calls++;last=c;}
    public static void triggerEvent(String e,Object a,Object b,Object c,Object d){calls++;last=d;}
}
