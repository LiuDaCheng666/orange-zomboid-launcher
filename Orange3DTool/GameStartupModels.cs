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
  internal DateTime ObservationUtc;
  internal long ObservationFloor;
  public string SessionId, Stage, Message;
  public string[] Lines;
  public GameLogIssue[] Issues=new GameLogIssue[0];
  public double ElapsedSeconds;
  public long ProcessBirth;
  public int? ProcessId, CompletedItems, TotalItems;
  public long? WorkingSetBytes, TotalPhysicalBytes, HeapLimitBytes;
  public bool Complete, Failed, WaitingForInput;
 }
}
