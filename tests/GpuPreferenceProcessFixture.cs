using System;
using System.Diagnostics;
using Microsoft.Win32;

static class GpuPreferenceProcessFixture {
 static int Main(){
  string exe=Process.GetCurrentProcess().MainModule.FileName;
  using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences")){
   string value=key==null?"":(string)key.GetValue(exe,"");
   Console.WriteLine(value);
   return value.Contains("GpuPreference=2;")?0:2;
  }
 }
}
