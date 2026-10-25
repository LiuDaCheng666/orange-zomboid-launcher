using System;
using System.IO;
namespace PZ3D {
 sealed class GameLogInput {
  internal string Id,Name,Kind,SessionKey,ModifiedUtc,Encoding,Warning;
  internal long Length;
  internal bool Truncated;
  internal Func<TextReader> Open;
 }
 sealed class GameLogEvidence {public string source,text;public int line;}
 sealed class GameLogFrame {public string mod,file,function;public int line;}
 sealed class GameLogIssue {
  public string id,type,severity,impact,title,meaning,advice,phase,attemptId;
  public int count;
  public string[] mods=new string[0];
  public GameLogFrame[] frames=new GameLogFrame[0];
  public GameLogEvidence[] evidence=new GameLogEvidence[0];
 }
 sealed class GameLogAttempt {public string id,sessionKey,state,stage,summary;public int firstLine,lastLine;}
 sealed class GameLogSource {public string id,name,kind,encoding,status,modifiedUtc;public long bytesRead;public bool truncated;}
 sealed class GameLogReport {
  public string checkedUtc,origin,summary,state,stage,reportText;
  public bool incomplete;
  public int fileCount,errorCount,warningCount;
  public long bytesRead;
  public GameLogIssue[] issues=new GameLogIssue[0];
  public GameLogAttempt[] attempts=new GameLogAttempt[0];
  public GameLogSource[] sources=new GameLogSource[0];
  public string[] warnings=new string[0];
 }
 sealed class GameLogAnalysisStatus {public string jobId,state,message;public int progress;public GameLogReport report;}
}
