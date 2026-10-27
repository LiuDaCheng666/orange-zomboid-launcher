using System;using System.IO;using System.Threading;
static class OwnedNativeProcessFixture {static void Main(string[] args){File.WriteAllLines("native-arguments.txt",args);DateTime end=DateTime.UtcNow.AddSeconds(30);while(DateTime.UtcNow<end&&!File.Exists("native-stop"))Thread.Sleep(50);}}
