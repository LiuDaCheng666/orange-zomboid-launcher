package se.krka.kahlua.vm;
public interface KahluaTable {
    Object rawget(Object key);
    void rawset(Object key, Object value);
}
