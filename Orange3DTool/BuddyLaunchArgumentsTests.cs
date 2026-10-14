using System;
using System.IO;
namespace PZ3D {
 static class BuddyLaunchArgumentsTests {
  static void Check(bool b,string name){SteamLaunchOptionsTests.Check(b,name);}
  public static int Main(string[] args){try{if(Array.IndexOf(args,"--bat-output-context")>=0)BatOutputContext();else if(Array.IndexOf(args,"--bat-harmless")>=0)HarmlessStatements(false);else if(Array.IndexOf(args,"--bat-harmless-guard")>=0)HarmlessStatements(true);else if(Array.IndexOf(args,"--java-boundary")>=0)JavaBoundary();else if(Array.IndexOf(args,"--bat-context")>=0)BatContext();else if(Array.IndexOf(args,"--bat-quoted-block")>=0)QuotedContext(false);else if(Array.IndexOf(args,"--bat-quoted-caret")>=0)QuotedContext(true);else if(Array.IndexOf(args,"--bat-lookalikes")>=0)BatLookalikes();else if(Array.IndexOf(args,"--bat-chain")>=0)ShellCommands(false);else if(Array.IndexOf(args,"--bat-pipe")>=0)ShellCommands(true);else if(Array.IndexOf(args,"--bat-chain-guard")>=0)ShellCommands(false,true);else if(Array.IndexOf(args,"--bat-pipe-guard")>=0)ShellCommands(true,true);else if(Array.IndexOf(args,"--bat-shell-literals")>=0)ShellLiterals();else Run();return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
  public static void Run(){
   JavaBoundary();BatContext();QuotedContext(false);QuotedContext(true);BatLookalikes();ShellCommands(false);ShellCommands(true);ShellLiterals();HarmlessStatements(false);HarmlessStatements(true);BatOutputContext();
   GuardRegression();
   foreach(string arg in new[]{"-javaagent:ZombieBuddy.jar","-javaagent:\"C:\\My Mods\\ZombieBuddy.jar\"","-agentlib:zbNative","-agentpath:C:\\mods\\zbNative.dll"})Check(BuddyLaunchArguments.Classify(arg)==BuddyArgumentKind.Known,"exact Buddy recognized");
   foreach(string arg in new[]{"-Dnote=ZombieBuddy","-javaagent:MyZombieBuddy.jar","-javaagent:ZombieBuddy.jar.bak","-agentlib:other","-agentlib:zbNativeExtra","-agentpath:C:\\mods\\myzbNative.dll"})Check(BuddyLaunchArguments.Classify(arg)==BuddyArgumentKind.None,"lookalike and unrelated arguments preserved");
   foreach(string arg in new[]{"-javaagent:ZombieBuddy.jar=config_dir=C:\\other","-agentlib:zbNative=custom","-agentpath:zbNative.dll=prefix=x","-javaagent:relative/ZombieBuddy.jar"})Check(BuddyLaunchArguments.Classify(arg)==BuddyArgumentKind.Unknown,"custom Buddy options and ambiguous paths refused");
   var clean=BuddyLaunchArguments.CleanCommandLine("-Xmx28g -javaagent:\"C:\\My Mods\\ZombieBuddy.jar\" -agentlib:zbNative -Dkeep=yes -javaagent:Other.jar");Check(clean.Safe&&clean.Changed&&clean.Text=="-Xmx28g -Dkeep=yes -javaagent:Other.jar"&&clean.RemovedCount==2,"duplicate Buddy cleanup preserves other arguments");Check(!clean.Summary.Contains("C:")&&!clean.Summary.Contains("keep"),"summary contains no options or paths");
   foreach(string line in new[]{"-agentlib:zbNative=config_dir=secret","-javaagent:\"ZombieBuddy.jar","-agentlib:zbNative & echo hi"}){var result=BuddyLaunchArguments.CleanCommandLine(line);Check(!result.Safe&&!result.Changed&&result.Text==line,"ambiguous command never partly cleaned");}
   string json="{\"vmArgs\":[\"-Xmx28g\",\"-agentlib:zbNative\"],\"windows\":{\"10\":{\"vmArgs\":[\"-javaagent:ZombieBuddy.jar\",\"-javaagent:Other.jar\"]}},\"note\":\"ZombieBuddy\"}";clean=BuddyLaunchArguments.CleanJson(json);Check(clean.Safe&&clean.Changed&&clean.Text.Contains("-agentlib:zbNative")&&clean.Text.Contains("Other.jar")&&!clean.Text.Contains("-javaagent:ZombieBuddy.jar"),"JSON normal loader retained while legacy branch removed");string normal="{\"vmArgs\":[\"-agentlib:zbNative\"]}";Check(!BuddyLaunchArguments.CleanJson(normal).Changed,"normal JSON native loader is no conflict");Check(!BuddyLaunchArguments.CleanJson("{\"vmArgs\":[],\"vmArgs\":[\"-agentlib:zbNative\"]}").Safe,"duplicate JSON keys rejected");Check(!BuddyLaunchArguments.CleanJson("{\"vmArgs\":[\"-agentlib:zbNative=custom\"]}").Safe,"unknown JSON Buddy rejected");
   string bat="@echo off\r\nrem -agentlib:zbNative\r\n:: -javaagent:ZombieBuddy.jar\r\nSET \"_JAVA_OPTIONS=-Xmx28g -agentlib:zbNative -Dkeep=yes\"\r\njava.exe -javaagent:ZombieBuddy.jar -jar game.jar\r\n";clean=BuddyLaunchArguments.CleanBat(bat);Check(clean.Safe&&clean.Text=="@echo off\r\nrem -agentlib:zbNative\r\n:: -javaagent:ZombieBuddy.jar\r\nSET \"_JAVA_OPTIONS=-Xmx28g -Dkeep=yes\"\r\njava.exe -jar game.jar\r\n","BAT cleanup preserves comments and line endings");
   foreach(string batLine in new[]{"java %OPTS% -agentlib:zbNative","java -agentlib:zbNative | more","set X=-agentlib:zbNative","java -agentlib:zbNative ^\r\n -jar game.jar","java !OPTS! -agentlib:zbNative"})Check(!BuddyLaunchArguments.CleanBat(batLine).Safe,"complex Buddy BAT requires manual review");
   Check(!BuddyLaunchArguments.CleanBat("echo ZombieBuddy\r\njava -Dnote=zbNative -jar game.jar").Changed,"ordinary BAT text never counts as Buddy");
   Check(BuddyLaunchArguments.CleanBat("set X=-javaagent:MyZombieBuddy.jar").Safe,"lookalike agent in an unrelated BAT assignment is untouched");
   Console.WriteLine("BUDDY ARGUMENTS PASSED");
  }
  static void JavaBoundary(){
   string[] inputs={"java -jar game.jar -javaagent:ZombieBuddy.jar","java -cp app.jar GameMain -agentlib:zbNative","java -agentlib:zbNative -jar game.jar -javaagent:ZombieBuddy.jar","java -javaagent:ZombieBuddy.jar -classpath \"app jars\\*\" GameMain -agentlib:zbNative"};
   string[] expected={"java -jar game.jar -javaagent:ZombieBuddy.jar","java -cp app.jar GameMain -agentlib:zbNative","java -jar game.jar -javaagent:ZombieBuddy.jar","java -classpath \"app jars\\*\" GameMain -agentlib:zbNative"};
   for(int i=0;i<inputs.Length;i++){var result=BuddyLaunchArguments.CleanBat(inputs[i]);Check(result.Safe&&result.Text==expected[i]&&result.RemovedCount==(i<2?0:1),"Java entry point preserves literal game arguments case "+i);}
   var operand=BuddyLaunchArguments.CleanBat("java -javaagent:ZombieBuddy.jar -cp -agentlib:zbNative GameMain -agentlib:zbNative");Check(operand.Safe&&operand.RemovedCount==1&&operand.Text=="java -cp -agentlib:zbNative GameMain -agentlib:zbNative","classpath operand and trailing argument are not JVM agents");
   foreach(string line in new[]{"java --unknown operand -agentlib:zbNative GameMain","java @args.txt -agentlib:zbNative GameMain","java --source 17 Game.java -agentlib:zbNative"}){var result=BuddyLaunchArguments.CleanBat(line);Check(!result.Safe&&!result.Changed&&result.Text==line,"unproved Java option boundary refuses unchanged invocation");}
  }
  static void BatContext(){
   foreach(string text in new[]{"if exist game.jar (\r\n java -agentlib:zbNative -jar game.jar\r\n)","java -agentlib:zbNative -jar first.jar\r\nif exist game.jar (\r\n java -agentlib:zbNative -jar game.jar\r\n)","echo continued ^\r\njava -agentlib:zbNative -jar game.jar","if exist game.jar ^\r\njava -agentlib:zbNative -jar game.jar","(\r\n set _JAVA_OPTIONS=-agentlib:zbNative\r\n)"}){var result=BuddyLaunchArguments.CleanBat(text);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"block or continuation keeps complete BAT unchanged");}
  }
  static void QuotedContext(bool continuation){
   string text=continuation?"echo continued ^\r\njava -agentlib:z\"bNative\" -jar game.jar":"if exist game.jar (\r\n java -agentlib:z\"bNative\" -jar game.jar\r\n)";var result=BuddyLaunchArguments.CleanBat(text);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"normalized quoted Buddy refuses complete "+(continuation?"continued":"block")+" BAT");
   string root=Path.Combine(LauncherTestPaths.Root,"quoted-bat-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid64.bat"),text);SteamLaunchOptionsTests.Reject(()=>e.Guard(),"Engine guard refuses normalized Buddy in ambiguous context");
  }
  static void BatLookalikes(){
   string root=Path.Combine(LauncherTestPaths.Root,"lookalike-bat-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");
   foreach(string text in new[]{"echo (hello)\r\njava -Dnote=-agentlib:zbNative -jar game.jar","echo (hello)\r\njava -Dnote=-agentlib:z\"bNative\" -jar game.jar","echo (hello)\r\njava -jar game.jar -agentlib:z\"bNative\"","echo (hello)\r\njava -cp -agentlib:z\"bNative\" GameMain","echo (hello)\r\nset _JAVA_OPTIONS=-Dnote=-agentlib:zbNative"}){var result=BuddyLaunchArguments.CleanBat(text);Check(result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"property game and classpath lookalikes remain safe unchanged BAT");File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid64.bat"),text);e.Guard();Check(true,"non-agent BAT lookalike never blocks Engine guard");}
   var normalized=BuddyLaunchArguments.CleanBat("java -agentlib:z\"bNative\" -jar game.jar");Check(normalized.Safe&&normalized.RemovedCount==1&&normalized.Text=="java -jar game.jar","same normalized actual JVM agent is cleaned in unambiguous invocation");
  }
  static void ShellCommands(bool pipe,bool guardOnly=false){
   string text=pipe?"java -cp app.jar GameMain | java -agentlib:z\"bNative\" -jar other.jar":"java -jar game.jar & java -agentlib:zbNative -jar other.jar";var result=BuddyLaunchArguments.CleanBat(text);if(!guardOnly)Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"second Java command after "+(pipe?"pipe":"chain")+" refuses complete original BAT");
   string root=Path.Combine(LauncherTestPaths.Root,"shell-bat-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid64.bat"),text);SteamLaunchOptionsTests.Reject(()=>e.Guard(),"Engine guard rejects independent chained or piped Buddy loader");
  }
  static void ShellLiterals(){
   string pipedOutput="echo java -agentlib:zbNative -jar game.jar | cmd";var pipedResult=BuddyLaunchArguments.CleanBat(pipedOutput);Check(!pipedResult.Safe&&!pipedResult.Changed&&pipedResult.RemovedCount==0&&pipedResult.Text==pipedOutput,"Buddy output piped to a possible command evaluator requires manual review");
   foreach(string text in new[]{"echo java -agentlib:zbNative -jar game.jar > start-java.bat & call start-java.bat","echo java -javaagent:ZombieBuddy.jar -jar game.jar>start-java.bat","echo -agentlib:zbNative >> options.txt","echo -agentlib:zbNative < options.txt","if exist game.jar echo -agentlib:zbNative"}){var result=BuddyLaunchArguments.CleanBat(text);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"redirected or control-wrapped Buddy output requires manual review");}
   foreach(string text in new[]{@"C:\tools\echo -agentlib:zbNative",@"echo ready & C:\tools\echo -javaagent:ZombieBuddy.jar","echo ready & cmd /c \"java -agentlib:z^bNative -jar game.jar\""}){var result=BuddyLaunchArguments.CleanBat(text);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"external echo wrapper and caret-obscured possible loader refuse whole script");}
   string wrapped="java -jar game.jar & cmd /c \"java -agentlib:zbNative -jar other.jar\"";var unsupported=BuddyLaunchArguments.CleanBat(wrapped);Check(!unsupported.Safe&&!unsupported.Changed&&unsupported.Text==wrapped,"unsupported second command wrapper cannot be declared agent-free");
   foreach(string text in new[]{"cmd /c \"java -agentlib:z\"bNative\" -jar other.jar\"","echo ready | cmd /c \"java -javaagent:ZombieBuddy.jar -jar other.jar\"","echo -agentlib:zbNative & java -agentlib:zbNative -jar game.jar","echo ready & java --unknown operand -agentlib:zbNative GameMain","java -jar game.jar & start java -agentpath:zbNative.dll=custom -jar other.jar","echo ready & cmd /c \"java -agentlib:zbNative&pause\"","echo ready | cmd /c \"java -javaagent:ZombieBuddy.jar|more\"","echo ready & java --unknown \"java -agentlib:zbNative\" GameMain"}){var result=BuddyLaunchArguments.CleanBat(text);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"possible loader in unsupported wrapper or chained invocation refuses whole script");}
   foreach(string text in new[]{"java -jar game.jar \"& java -agentlib:zbNative -jar other.jar\"","java -cp app.jar GameMain \"| java -agentlib:zbNative -jar other.jar\"","java \"-Dnote=& java -agentlib:zbNative\" -jar game.jar","java -cp \"app.jar|other.jar\" GameMain","set \"_JAVA_OPTIONS=-Dnote=literal&text|text\"","echo ready & java -Dnote=-agentlib:zbNative -jar game.jar","java -jar game.jar ^& java -agentlib:zbNative -jar other.jar"}){var result=BuddyLaunchArguments.CleanBat(text);Check(result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"literal separators and no-agent commands remain safe unchanged");}
   foreach(string text in new[]{"set \"_JAVA_OPTIONS=-Dnote=keep\" & java -agentlib:z\"bNative\" -jar other.jar","set \"_JAVA_OPTIONS=-agentlib:zbNative\" & java -jar game.jar","echo continued ^\r\njava -jar game.jar & java -agentlib:zbNative -jar other.jar","java -jar game.jar&&java -agentlib:zbNative -jar other.jar","java -jar game.jar||java -agentlib:z\"bNative\" -jar other.jar"}){var result=BuddyLaunchArguments.CleanBat(text);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"assignment continuation and adjacent command separators refuse whole script");}
  }
  static void HarmlessStatements(bool guard){
   string[] inputs={"echo -agentlib:zbNative","echo -javaagent:ZombieBuddy.jar","echo use -javaagent:ZombieBuddy.jar to start","echo ready & pause","java -jar game.jar & pause","echo ready | findstr ready",@"setlocal & cd /d C:\games","java -jar game.jar & cmd /c echo ready","echo -agentlib:zbNative & java -jar game.jar","echo -Dnote=zbNative","@ECHO \"-agentlib:z\"bNative\"\"","echo ready&&pause","echo ready||pause","echo \"-agentlib:zbNative > literal\"","echo -agentlib:zbNative ^> literal","echo ready > ready.txt & pause","echo -agentlib:zbNative || pause","echo -agentlib:zbNative && pause","echo \"-agentlib:zbNative & literal\"","echo -agentlib:zbNative ^& literal","echo \"-agentlib:zbNative | literal\"","echo -agentlib:zbNative ^| literal"};
   string root=Path.Combine(LauncherTestPaths.Root,"harmless-bat-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);
   if(guard)Directory.CreateDirectory(e.Game);
   int first=Array.IndexOf(Environment.GetCommandLineArgs(),"--chains-first")>=0?3:0;
   for(int i=first;i<inputs.Length;i++){
    string text="@echo off\r\n"+inputs[i]+"\r\n";
    if(!guard){var result=BuddyLaunchArguments.CleanBat(text);Check(result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==text,"harmless BAT statement preserves exact text case "+i);continue;}
    File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Xmx8g\",\"-javaagent:Other.jar\"]}");
    foreach(string name in new[]{"ProjectZomboid64.bat","ProjectZomboid32.bat"})File.WriteAllText(Path.Combine(e.Game,name),text);
    e.Guard();Check(true,"Engine guard accepts harmless BAT statement case "+i);
    e.Switch(false,false);string json=File.ReadAllText(e.Config);Check(!json.Contains("-agentlib:zbNative")&&json.Contains("-Xmx8g")&&json.Contains("-javaagent:Other.jar"),"ordinary component removal completes through guard case "+i);
    foreach(string name in new[]{"ProjectZomboid64.bat","ProjectZomboid32.bat"})Check(File.ReadAllText(Path.Combine(e.Game,name))==text,"ordinary operation preserves harmless BAT bytes case "+i+" "+name);
   }
  }
  static void BatOutputContext(){
   // Losing the enclosing/continued output boundary must fail these literal inputs.
   string[] refused={
    "(\r\necho java -agentlib:zbNative -jar game.jar\r\n) > start-java.bat\r\ncall start-java.bat\r\n",
    "(\r\n(\r\necho java -agentlib:zbNative -jar game.jar\r\n)\r\n) > start-java.bat\r\n",
    "(\r\necho java -javaagent:ZombieBuddy.jar -jar game.jar\r\n) >> start-java.bat\r\n",
    "(\r\necho java -agentlib:zbNative -jar game.jar\r\n) | cmd\r\n",
    "> start-java.bat (\r\necho java -agentlib:zbNative -jar game.jar\r\n)\r\n",
    "(\r\n@ECHO java -agentlib:z\"bNative\" -jar game.jar\r\n) > start-java.bat\r\n",
    "(\r\necho java \"-javaagent:C:\\My Mods\\ZombieBuddy.jar\" -jar game.jar\r\n) > start-java.bat\r\n",
    "(\r\necho \"java -agentpath:C:\\mods\\zbNative.dll -jar game.jar\"\r\n) > start-java.bat\r\n",
    "echo java -agentlib:zbNative -jar game.jar ^\r\n > start-java.bat\r\ncall start-java.bat\r\n",
    "echo java -agentlib:zb^\r\nNative -jar game.jar > start-java.bat\r\n",
    "echo java -agentlib:z\"b^\r\nNative\" -jar game.jar > start-java.bat\r\n",
    "echo java ^\r\n -javaagent:ZombieBuddy.jar -jar game.jar | cmd\r\n",
    "(\r\necho java -agentlib:zbNative -jar game.jar\r\n) ^\r\n > start-java.bat\r\n",
    "(\r\n(\r\necho java -agentlib:zbNative -jar game.jar\r\n) > start-java.bat\r\necho ready\r\n)\r\n",
    "(\necho java -agentlib:zbNative -jar game.jar\n) > start-java.bat\n",
    "(\recho java -agentlib:zbNative -jar game.jar\r) > start-java.bat\r",
    "java -javaagent:ZombieBuddy.jar -jar game.jar\r\n(\r\necho java -agentlib:zbNative -jar game.jar\r\n) > start-java.bat\r\n",
    "> start-java.bat (\r\necho java -agentlib:zbNative -jar game.jar\r\n",
    "> start-java.bat (\r\necho java -agentlib:zb^\r\nNative -jar game.jar\r\n",
    "(\r\necho java -agentlib:zb^\r\nNative -jar game.jar\r\n) > start-java.bat\r\n",
    "echo java -agentlib:zb^\r\n\"Native\" -jar game.jar > start-java.bat\r\n",
    "if exist game.jar (\r\necho java -agentlib:zbNative -jar game.jar\r\n) else (\r\necho ready\r\n) > start-java.bat\r\n",
    "> start-java.bat if exist game.jar (\r\necho ready\r\n) else (\r\necho java -agentlib:zbNative -jar game.jar\r\n)\r\n",
    "if exist game.jar (\r\necho java -agentlib:zbNative -jar game.jar\r\n) e^lse (\r\necho ready\r\n) | cmd\r\n",
    "if exist game.jar (\r\necho java -agentlib:zbNative -jar game.jar\r\n) ^\r\n else (\r\necho ready\r\n) >> start-java.bat\r\n"
   };
   // Treating literal operators, unrelated output, or Java operands as agents is a break.
   string[] harmless={
    "(\r\necho java -agentlib:zbNative -jar game.jar\r\n)\r\n",
    "(\r\n(\r\necho -javaagent:ZombieBuddy.jar\r\n)\r\n)\r\n",
    "echo \"(java -agentlib:zbNative) > literal | literal\"\r\n",
    "echo ^(java -agentlib:zbNative^) ^> literal ^| literal\r\n",
    "(\r\necho -agentlib:zbNative ^> literal\r\n)\r\n",
    "(\r\necho -agentlib:zbNative\r\n) ^> literal\r\n",
    "(\r\necho -agentlib:zbNative\r\n) || pause\r\n",
    "(\r\necho -agentlib:zbNative\r\n) & echo ready > ready.txt\r\n",
    "(\r\necho -agentlib:zbNative\r\necho ready > ready.txt\r\n)\r\n",
    "(\r\necho ready & pause\r\njava -jar game.jar & cmd /c echo ready\r\n) > ready.txt\r\n",
    "(\r\njava -Dnote=-agentlib:zbNative -jar game.jar\r\njava -cp -agentlib:z\"bNative\" GameMain\r\njava -jar game.jar -javaagent:ZombieBuddy.jar\r\n) > game-output.txt\r\n",
    "echo java -agentlib:zbNative ^\r\n -jar game.jar\r\n",
    "echo -agentlib:zbNative ^\r\n & echo ready > ready.txt\r\n",
    "rem ( echo -agentlib:zbNative > file\r\n:: ) | cmd\r\necho -agentlib:zbNative\r\n",
    "echo -agentlib:zbNative ^^\r\necho ready > ready.txt\r\n",
    "(\r\necho \"-agentlib:zbNative > literal | literal\"\r\n)\r\n",
    "if exist game.jar (\r\necho -agentlib:zbNative\r\n) else (\r\necho ready\r\n)\r\n",
    "(\r\necho -agentlib:zbNative\r\n)\r\n(\r\necho ready\r\n) > ready.txt\r\n",
    "(\r\necho -agentlib:zbNative\r\n) & (\r\necho ready\r\n) > ready.txt\r\n",
    "echo -agentlib:zbNative & (\r\necho ready\r\n) > ready.txt\r\n",
    "(\r\necho ready\r\n) > ready.txt & echo -agentlib:zbNative\r\n",
    "(\r\necho ready\r\n) > ready.txt || echo -agentlib:zbNative\r\n",
    "echo -agentlib:zbNative ^\r\n & (\r\necho ready\r\n) > ready.txt\r\n",
    "(\r\necho ready\r\n) ^\r\n > ready.txt & echo -agentlib:zbNative\r\n"
   };
   int failures=0;
   for(int i=0;i<refused.Length;i++){
    string input=refused[i];
    try{var result=BuddyLaunchArguments.CleanBat(input);Check(!result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==input,"active BAT output context refuses exact original case "+i);}catch(Exception ex){Console.WriteLine(ex.Message);failures++;}
    foreach(string name in new[]{"ProjectZomboid64.bat","ProjectZomboid32.bat"})try{BatOutputCallers(input,name,i);}catch(Exception ex){Console.WriteLine(ex.Message);failures++;}
   }
   for(int i=0;i<harmless.Length;i++)try{var result=BuddyLaunchArguments.CleanBat(harmless[i]);Check(result.Safe&&!result.Changed&&result.RemovedCount==0&&result.Text==harmless[i],"literal terminal and agent-free BAT output preserves exact text case "+i);}catch(Exception ex){Console.WriteLine(ex.Message);failures++;}
   if(failures>0)throw new Exception("FAIL: BAT output context regressions: "+failures);
  }
  static void BatOutputCallers(string text,string name,int number){
   string root=Path.Combine(LauncherTestPaths.Root,"output-bat-"+Guid.NewGuid().ToString("N")),steam=Path.Combine(root,"steam");
   var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true,()=>SteamLaunchOptions.Read(steam,42));
   Directory.CreateDirectory(e.Game);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));
   SteamLaunchOptionsTests.Fixture(steam,42,SteamLaunchOptionsTests.Wrap("\"108600\" { \"LaunchOptions\" \"-Xmx8g\" }"));
   File.WriteAllText(Path.Combine(steam,"steam.exe"),"fixture only; never execute");
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Xmx8g\",\"-javaagent:Other.jar\"]}",new System.Text.UTF8Encoding(true));
   File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid32.json"),"{\"vmArgs\":[\"-Xmx4g\"]}");
   File.WriteAllText(e.Mods,"VERSION = 1,\r\nmods\r\n{\r\n    mod = Viewpoint,\r\n    mod = OtherMod,\r\n}\r\nmaps\r\n{\r\n}\r\n");
   File.WriteAllText(Path.Combine(e.Game,"ZombieBuddy.jar"),"fixture loader bytes");
   foreach(string batName in new[]{"ProjectZomboid64.bat","ProjectZomboid32.bat"})File.WriteAllText(Path.Combine(e.Game,batName),batName==name?text:"@echo off\r\necho ready\r\n",batName.EndsWith("32.bat")?(System.Text.Encoding)new System.Text.UnicodeEncoding(false,true):new System.Text.UTF8Encoding(true));
   var before=new System.Collections.Generic.Dictionary<string,byte[]>();foreach(string path in Directory.GetFiles(root,"*",SearchOption.AllDirectories))before[path]=File.ReadAllBytes(path);
   string label="case "+number+" "+name;
   int failures=0;
   foreach(var check in new Action[]{
    ()=>SteamLaunchOptionsTests.Reject(()=>e.Guard(),"real Guard refuses output loader "+label),
    ()=>SteamLaunchOptionsTests.Reject(()=>e.Switch(false,false),"real ordinary disable refuses output loader "+label),
    ()=>{var preview=BuddyTakeover.Preview(e,new BatOutputFixtureEnvironment(steam,e.Game));Check(!preview.supported&&preview.stamp==""&&preview.locations.Length==0&&!preview.hasConflicts&&!preview.requiresSteamExit&&preview.message.Contains("复杂 BAT"),"real takeover Preview refuses rather than omits output loader "+label);},
    ()=>Check(!Directory.Exists(e.Data)&&Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length==before.Count&&System.Linq.Enumerable.All(before,p=>File.Exists(p.Key)&&System.Linq.Enumerable.SequenceEqual(p.Value,File.ReadAllBytes(p.Key))),"refused callers preserve all fixture bytes and create no writes "+label)
   })try{check();}catch(Exception ex){Console.WriteLine(ex.Message);failures++;}
   if(failures>0)throw new Exception("FAIL: output loader callers "+label+": "+failures);
  }
  sealed class BatOutputFixtureEnvironment:BuddyTakeoverEnvironment {
   readonly string steam,game;
   internal BatOutputFixtureEnvironment(string steam,string game){this.steam=steam;this.game=game;}
   internal override BuddySteamIdentity Capture(){return new BuddySteamIdentity(SteamLaunchOptions.Read(steam,42),77,100,Path.Combine(steam,"steam.exe"));}
   internal override bool GameRunning(string path){if(path!=game)throw new InvalidOperationException("Fixture path escaped");return false;}
   internal override BuddySteamState Inspect(BuddySteamIdentity bound){throw new InvalidOperationException("Process action disabled");}
   internal override void Shutdown(BuddySteamIdentity bound){throw new InvalidOperationException("Process action disabled");}
   internal override bool Reopen(BuddySteamIdentity bound){throw new InvalidOperationException("Process action disabled");}
   internal override long Milliseconds{get{return 0;}}
   internal override void Wait(int milliseconds){throw new InvalidOperationException("Process action disabled");}
   internal override void Replace(string temporary,string target,string displaced){throw new InvalidOperationException("Preview writes disabled");}
   internal override void MoveAbsent(string source,string target){throw new InvalidOperationException("Preview writes disabled");}
   internal override bool RestoreOwned(string candidate,string target,string retired,byte[] expected){throw new InvalidOperationException("Preview writes disabled");}
  }
  static void GuardRegression(){
   string root=Path.Combine(LauncherTestPaths.Root,"buddy-guard-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Dnote=zbNative\",\"-javaagent:OtherZombieBuddy.jar\"]}");string bat=Path.Combine(e.Game,"ProjectZomboid64.bat");File.WriteAllText(bat,"java -Dnote=zbNative -jar game.jar");e.Guard();Check(true,"unrelated BAT text and normal JSON loader do not block guard");e.Switch(false,false);string after=File.ReadAllText(e.Config);Check(after.Contains("-Dnote=zbNative")&&after.Contains("OtherZombieBuddy.jar")&&!after.Contains("-agentlib:zbNative"),"Switch removes only exact Buddy and preserves lookalike arguments");
   string steam=Path.Combine(root,"client");e=new Engine(e.Game,e.User,e.Data,true,()=>SteamLaunchOptions.Read(steam,42));SteamLaunchOptionsTests.Fixture(steam,42,SteamLaunchOptionsTests.Wrap("\"999\" { \"LaunchOptions\" \"-agentlib:zbNative\" } \"108600\" {}"));SteamLaunchOptionsTests.Fixture(steam,7,SteamLaunchOptionsTests.Wrap("\"108600\" { \"LaunchOptions\" \"-agentlib:zbNative\" }"));e.Guard();Check(true,"other app and account Buddy do not block Engine guard; absent own field stays valid");
   SteamLaunchOptionsTests.Fixture(steam,42,SteamLaunchOptionsTests.Wrap("\"108600\" { \"LaunchOptions\" \"-agentlib:zbNative\" }"));SteamLaunchOptionsTests.Reject(()=>e.Guard(),"own Steam Buddy conflict is actionable");SteamLaunchOptionsTests.Fixture(steam,42,SteamLaunchOptionsTests.Wrap("\"108600\" {}"));File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative=config_dir=secret\"]}");byte[] before=File.ReadAllBytes(e.Config);SteamLaunchOptionsTests.Reject(()=>e.Switch(false,false),"unknown JSON Buddy is refused before Switch writes");Check(System.Linq.Enumerable.SequenceEqual(before,File.ReadAllBytes(e.Config)),"unknown cleanup preserves original JSON bytes");var unknown=new Engine(e.Game,e.User,e.Data,true,()=>SteamLaunchOptions.Read(steam,0));SteamLaunchOptionsTests.Reject(()=>unknown.Guard(),"unknown active identity is not silently treated as cleared Steam options");
  }
 }
}
