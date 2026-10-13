using System;
using System.Reflection;
namespace PZ3D {
 static class GameProcessGuardTests {
  static void Check(bool value,string label){Console.WriteLine((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
  public static int Main(){try{Run();return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
  public static void Run(){
   Type type=typeof(Engine).Assembly.GetType("PZ3D.GameProcessGuard");Check(type!=null,"shared selected-game process guard exists");
   var method=type.GetMethod("MatchesJava",BindingFlags.Public|BindingFlags.Static);string game=@"E:\SteamLibrary\steamapps\common\ProjectZomboid";
   Func<string,string,bool> match=(name,path)=>(bool)method.Invoke(null,new object[]{game,name,path});
   Check(match("java",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre64\bin\java.exe"),"direct selected-game jre64 Java counts as running without native EXE");
   Check(match("javaw",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre64\bin\javaw.exe"),"selected-game windowless Java counts as running");
   Check(match("java.exe",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre\bin\java.exe")&&match("javaw.exe",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre\bin\javaw.exe"),"both bundled jre Java forms count as running");
   Check(match("JAVA",@"e:\steamlibrary\steamapps\common\PROJECTZOMBOID\jre64\bin\JAVA.EXE"),"Java module paths compare Windows case-insensitively");
   Check(!match("java",@"C:\Program Files\Java\jdk-21\bin\java.exe"),"another JDK does not claim selected game is running");
   Check(!match("java",@"E:\SteamLibrary\steamapps\common\ProjectZomboidOther\jre64\bin\java.exe"),"lookalike installation prefix does not match");
   Check(!match("java",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\other\java.exe")&&!match("notepad",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre64\bin\java.exe"),"arbitrary image paths and non-Java process names do not match");
   Check(!match("java",null)&&!match("java",""),"missing module path does not fabricate selected-game evidence");
  }
 }
}
