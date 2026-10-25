package se.krka.kahlua.vm;
/** Compile-only signatures; never put this directory on a runtime classpath. */
public interface KahluaTable {Object rawget(Object key);void rawset(Object key,Object value);KahluaTable getMetatable();void setMetatable(KahluaTable value);}
