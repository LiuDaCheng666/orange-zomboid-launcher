using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Threading;
namespace PZ3D {static class SnapshotWorkshopResolverTests {
 static int checks;static void Check(bool value,string message){if(!value)throw new Exception("FAIL "+message);checks++;}
 static object Row(string id,string description,string title="author mod"){return new{id=id,title=title,description=description};}
 static object Page(params object[] rows){return new{items=rows};}
 public static int Main(){try{
  string query=null;var one=SnapshotWorkshopResolver.Resolve("Unrelated_ID",new HashSet<string>(),q=>{query=q;return Page(Row("123","Workshop ID: 123\nMod ID: Unrelated_ID\n"));},CancellationToken.None,null);
  Check(query=="\"Unrelated_ID\"","native search quotes the whole mod identity");Check(one.workshopId=="123","arbitrary author-declared mod ID resolves without a built-in registry");
  var wrong=SnapshotWorkshopResolver.Resolve("Demo",new HashSet<string>(),q=>Page(Row("124","Mod ID: Demo_Plus\n"),Row("125","Requires Demo; see Mod ID: Other\n")),CancellationToken.None,null);Check(wrong.workshopId==""&&wrong.candidates.Length==0,"substring and dependency mentions cannot select a source");
  var duplicates=SnapshotWorkshopResolver.Resolve("Demo",new HashSet<string>(),q=>Page(Row("1","Mod ID: Demo\n"),Row("2","Mod ID: Demo\n")),CancellationToken.None,null);Check(duplicates.workshopId==""&&duplicates.candidates.Length==2,"multiple exact declarations remain explicit choices");
  var historical=SnapshotWorkshopResolver.Resolve("Demo",new HashSet<string>{"2"},q=>Page(Row("1","Mod ID: Demo\n"),Row("2","Mod ID: Demo\n")),CancellationToken.None,null);Check(historical.workshopId=="2","a unique previously used Workshop item disambiguates exact author declarations");
  var markup=SnapshotWorkshopResolver.Resolve("A&B",new HashSet<string>(),q=>Page(Row("55","[b]Mod ID:[/b] A&amp;B[br]Workshop ID: 55")),CancellationToken.None,null);Check(markup.workshopId=="55","Steam BBCode and HTML entities preserve exact identity");
  var invalid=SnapshotWorkshopResolver.Resolve("Demo",new HashSet<string>(),q=>Page(Row("0","Mod ID: Demo"),Row("-2","Mod ID: Demo")),CancellationToken.None,null);Check(invalid.workshopId=="","non-Workshop numeric identities cannot become downloads");
  Check(SnapshotWorkshopResolver.Sources("{\"Demo\":\"123\"}")["Demo"]=="123","explicit chosen Workshop IDs survive the native boundary");bool refused=false;try{SnapshotWorkshopResolver.Sources("{\"Demo\":\"https://unrelated.invalid/123\"}");}catch(IOException){refused=true;}Check(refused,"arbitrary URLs never become download targets");
  using(var stop=new CancellationTokenSource()){stop.Cancel();bool cancelled=false;try{SnapshotWorkshopResolver.Resolve("Demo",new HashSet<string>(),q=>{throw new Exception("should not search");},stop.Token,null);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancellation precedes network work");}
  Console.WriteLine("SnapshotWorkshopResolver PASS: "+checks+" assertions");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}}
