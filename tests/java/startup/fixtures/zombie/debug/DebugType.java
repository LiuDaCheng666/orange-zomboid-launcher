package zombie.debug;
public enum DebugType {General,Lua;
 public DebugLogStream getLogStream(){throw new AssertionError("Unexpected native logging");}
 public void error(Object value){System.err.println(value);}
}
