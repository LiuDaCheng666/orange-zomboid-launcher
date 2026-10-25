using System;

namespace PZ3D {
 interface IGameStartupMonitor:IDisposable {
  void Start();
  void Submitted();
  void Fail(string message);
 }
 sealed class GameStartupLaunch {
  public string SessionId, GamePath, UserPath, DataPath, TargetKind, Stage, Message;
  public DateTime StartedUtc;
 }

 sealed class GameStartupSnapshot {
  public string SessionId, Stage, Message;
  public string[] Lines;
  public double ElapsedSeconds;
  public int? ProcessId, CompletedItems, TotalItems;
  public long? WorkingSetBytes, TotalPhysicalBytes, HeapLimitBytes;
  public bool Complete, Failed, WaitingForInput;
 }
}
