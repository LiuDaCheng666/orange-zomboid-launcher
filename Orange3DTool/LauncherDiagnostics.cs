using System;using System.Windows.Forms;
namespace PZ3D {static class LauncherDiagnostics {
 static string directory;static bool installed;
 public static void Install(string data){if(installed)return;installed=true;directory=data;AppDomain.CurrentDomain.UnhandledException+=(sender,e)=>Write("未处理异常",e.ExceptionObject as Exception);Application.ThreadException+=(sender,e)=>Write("界面线程异常",e.Exception);Write("启动器会话开始",null);}
 public static void Write(string phase,Exception error){try{new LauncherJournal(directory).Diagnostic(phase+(error==null?"":" · "+error));}catch{ /* Last-resort crash logging must not recursively crash the process. */ }}
}}
