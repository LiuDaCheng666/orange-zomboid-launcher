using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PZ3D {
 public static class AppVersion {
  static readonly string current=Read();
  public static string Current { get { return current; } }
  public static string ReleaseTag { get { return "v"+current; } }
  static string Read() {
   using(var stream=typeof(AppVersion).Assembly.GetManifestResourceStream("Launcher.Version.txt")) {
    if(stream==null)throw new InvalidDataException("Missing embedded launcher VERSION.");
    using(var reader=new StreamReader(stream,new UTF8Encoding(false,true))) { string value=reader.ReadToEnd().Trim(); CoreReleaseProtocol.ParseVersion(value); return value; }
   }
  }
 }
 public sealed class CoreFile { public string path,sha256,url; public long length; }
 public sealed class CoreManifest {
  public int schema; public string version,platform,minimumUpdaterVersion,notes; public List<CoreFile> files;
  internal CoreVerifiedEnvelope VerifiedEnvelope { get; private set; }
  internal void AttachProof(byte[] raw,byte[] signature,string identity) { VerifiedEnvelope=new CoreVerifiedEnvelope(raw,signature,identity); }
 }
 public sealed class CoreInstallRecord { public int schema; public string root,installationId,version; public List<CoreFile> files; }
 // This proof preserves transport bytes, not mutable model fields. Consumers must reverify
 // Raw/Signature with their embedded trusted key before using staged metadata.
 internal sealed class CoreVerifiedEnvelope {
  readonly byte[] raw,signature;
  public byte[] Raw { get { return (byte[])raw.Clone(); } }
  public byte[] Signature { get { return (byte[])signature.Clone(); } }
  public string KeyIdentity { get; private set; }
  internal CoreVerifiedEnvelope(byte[] raw,byte[] signature,string identity) { this.raw=(byte[])raw.Clone(); this.signature=(byte[])signature.Clone(); KeyIdentity=identity; }
 }
 public static class CoreReleaseProtocol {
  public const int MaxManifestBytes=1048576, MaxFiles=32;
  public const long MaxFileBytes=67108864, MaxTotalBytes=134217728;
  public const string UpdaterVersion="1.0.0";
  public const string ReleaseBase="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/";
  static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  public static readonly ReadOnlyCollection<string> OwnedPaths=Array.AsReadOnly(new[]{
   "橙子启动器.exe","橙子更新助手.exe","卸载橙子启动器.exe","VERSION","CHANGELOG.md","使用说明.txt","release-public-key.xml",
   "Web/index.html","Web/app.js","Web/app.css","Web/home.png","Web/orange-app.ico","Web/WebView2-LICENSE.txt",
   "Web/Microsoft.Web.WebView2.Core.dll","Web/Microsoft.Web.WebView2.WinForms.dll","Web/WebView2Loader.dll",
   "Assets/OrangeShootPresentation.jar","Assets/OrangeLaunchBridge.lua"
  });
  internal static Version ParseVersion(string value) {
   if(value==null || !Regex.IsMatch(value,@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z"))throw new InvalidDataException("Invalid release version.");
   string[] parts=value.Split('.'); var numbers=new int[3];
   for(int i=0;i<3;i++)if(!int.TryParse(parts[i],NumberStyles.None,CultureInfo.InvariantCulture,out numbers[i]) || numbers[i]>65534)throw new InvalidDataException("Release version component out of range.");
   return new Version(numbers[0],numbers[1],numbers[2]);
  }
  public static bool IsNewer(string candidate,string currentVersion) { return ParseVersion(candidate)>ParseVersion(currentVersion); }
  public static CoreManifest Verify(byte[] raw,byte[] signature,string publicKeyXml,string currentVersion) {
   if(raw==null || raw.Length==0 || raw.Length>MaxManifestBytes)throw new InvalidDataException("Manifest size is invalid.");
   if(signature==null || signature.Length==0 || signature.Length>1024)throw new InvalidDataException("Signature size is invalid.");
   // Snapshot caller-owned buffers so the parsed bytes are exactly those authenticated.
   raw=(byte[])raw.Clone(); signature=(byte[])signature.Clone();
   string identity;
   using(var rsa=new RSACryptoServiceProvider()) {
    rsa.PersistKeyInCsp=false; rsa.FromXmlString(publicKeyXml);
    if(rsa.KeySize<2048 || !rsa.VerifyData(raw,CryptoConfig.MapNameToOID("SHA256"),signature))throw new CryptographicException("Release signature is invalid.");
    identity=Hash(Utf8.GetBytes(rsa.ToXmlString(false)));
   }
   var manifest=ParseManifest(raw); Validate(manifest,currentVersion); manifest.AttachProof(raw,signature,identity); return manifest;
  }
  // Parsing and validation are deliberately separate from authentication.
  internal static Dictionary<string,object> ReadStoredObject(byte[] raw,params string[] fields) {
   if(raw==null||raw.Length==0||raw.Length>MaxManifestBytes)throw new InvalidDataException("Stored document size is invalid.");
   return Object(new StrictJson(Utf8.GetString(raw),true).Read(),fields);
  }
  public static CoreManifest ParseManifest(byte[] raw) {
   if(raw==null || raw.Length==0 || raw.Length>MaxManifestBytes)throw new InvalidDataException("Manifest size is invalid.");
   var map=Object(new StrictJson(Utf8.GetString(raw)).Read(),"schema","version","platform","minimumUpdaterVersion","notes","files");
   return new CoreManifest { schema=Int(map["schema"]),version=String(map["version"]),platform=String(map["platform"]),minimumUpdaterVersion=String(map["minimumUpdaterVersion"]),notes=String(map["notes"]),files=Files(map["files"]) };
  }
  public static byte[] SerializeManifest(CoreManifest manifest) { Validate(manifest,"0.0.0"); byte[] raw=Utf8.GetBytes(new JavaScriptSerializer().Serialize(manifest)); if(raw.Length>MaxManifestBytes)throw new InvalidDataException("Manifest is too large."); return raw; }
  public static void Validate(CoreManifest manifest,string currentVersion) {
   if(manifest==null || manifest.schema!=1 || manifest.platform!="win-x64" || manifest.notes==null)throw new InvalidDataException("Invalid manifest metadata.");
   if(ParseVersion(manifest.version)<ParseVersion(currentVersion))throw new InvalidDataException("Older release replay rejected.");
   if(ParseVersion(manifest.minimumUpdaterVersion)>ParseVersion(UpdaterVersion))throw new InvalidDataException("This release requires a newer updater.");
   if(manifest.files==null || manifest.files.Count==0 || manifest.files.Count>MaxFiles)throw new InvalidDataException("Invalid file count.");
   var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total=0;
   foreach(var file in manifest.files) {
    if(file==null)throw new InvalidDataException("Missing file metadata."); Owned(file.path);
    if(!paths.Add(file.path))throw new InvalidDataException("Duplicate release path.");
    if(file.length<=0 || file.length>MaxFileBytes || (total+=file.length)>MaxTotalBytes)throw new InvalidDataException("Invalid release file length.");
    if(file.sha256==null || !Regex.IsMatch(file.sha256,@"\A[0-9A-Fa-f]{64}\z"))throw new InvalidDataException("Invalid SHA256.");
    ValidateUrl(file.url,manifest.version);
   }
  }
  static void ValidateUrl(string value,string version) {
   string prefix=ReleaseBase+"v"+version+"/";
   if(value==null || !value.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("Untrusted release URL.");
   string asset=value.Substring(prefix.Length); Uri uri;
   if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.IsDefaultPort || uri.UserInfo!="" || uri.Query!="" || uri.Fragment!="" || asset.Length==0)throw new InvalidDataException("Invalid release asset URL.");
   string decoded=Uri.UnescapeDataString(asset);
   if(decoded=="." || decoded==".." || decoded.IndexOfAny(new[]{'/','\\',':','?','#','%','\0'})>=0 || decoded.Trim()!=decoded || decoded.EndsWith(".",StringComparison.Ordinal) || decoded.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || Uri.EscapeDataString(decoded)!=asset)throw new InvalidDataException("Unsafe release asset name.");
  }
  static void Owned(string path) { if(path==null || !OwnedPaths.Contains(path))throw new InvalidDataException("Path is not launcher-owned."); }
  public static string Target(string root,string relative) {
   Owned(relative);
   if(string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))throw new InvalidDataException("Installation root must be absolute.");
   string fullRoot=Path.GetFullPath(root);
   if(!string.Equals(Path.GetPathRoot(root),Path.GetPathRoot(fullRoot),StringComparison.OrdinalIgnoreCase) || root.StartsWith(@"\\?\",StringComparison.Ordinal) || root.StartsWith(@"\\.\",StringComparison.Ordinal))throw new InvalidDataException("Installation root must be fully qualified and not a device path.");
   string target=Path.GetFullPath(Path.Combine(fullRoot,relative.Replace('/',Path.DirectorySeparatorChar)));
   if(!target.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Path escapes installation root.");
   // Check even ancestors of a not-yet-created root and the final file itself.
   for(string item=target; item!=null; item=Path.GetDirectoryName(item)) {
    try { if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Reparse points are not permitted in release targets."); }
    catch(FileNotFoundException) {} catch(DirectoryNotFoundException) {}
   }
   return target;
  }
  public static string Hash(byte[] data) { using(var sha=SHA256.Create())return Hex(sha.ComputeHash(data)); }
  public static string HashFile(string path) { using(var input=File.OpenRead(path))using(var sha=SHA256.Create())return Hex(sha.ComputeHash(input)); }
  static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-",""); }
  static Dictionary<string,object> Object(object value,params string[] names) {
   var map=value as Dictionary<string,object>; if(map==null || map.Count!=names.Length)throw new InvalidDataException("Unexpected JSON fields.");
   foreach(string name in names)if(!map.ContainsKey(name))throw new InvalidDataException("Missing JSON field: "+name); return map;
  }
  static string String(object value) { var result=value as string; if(result==null)throw new InvalidDataException("Expected JSON string."); return result; }
  static long Long(object value) { if(!(value is long))throw new InvalidDataException("Expected JSON integer."); return (long)value; }
  static int Int(object value) { long number=Long(value); if(number<int.MinValue || number>int.MaxValue)throw new InvalidDataException("Integer out of range."); return (int)number; }
  static List<CoreFile> Files(object value) {
   var list=value as List<object>; if(list==null || list.Count>MaxFiles)throw new InvalidDataException("Expected bounded file list."); var result=new List<CoreFile>();
   foreach(var item in list) { var map=Object(item,"path","sha256","url","length"); result.Add(new CoreFile {path=String(map["path"]),sha256=String(map["sha256"]),url=String(map["url"]),length=Long(map["length"])}); } return result;
  }
  // A small strict reader avoids JavaScriptSerializer's duplicate-key overwrite and
  // permissive JavaScript syntax. The wire schema requires only objects, arrays,
  // strings and integers; other JSON scalar types cannot occur in valid metadata.
  sealed class StrictJson {
   readonly string text; readonly bool allowNull; int at;
   internal StrictJson(string text,bool allowNull=false) { this.text=text; this.allowNull=allowNull; }
   internal object Read() { object value=Value(0); Space(); if(at!=text.Length)Bad(); return value; }
   void Bad() { throw new InvalidDataException("Invalid or ambiguous release JSON."); }
   void Space() { while(at<text.Length && (text[at]==' ' || text[at]=='\t' || text[at]=='\r' || text[at]=='\n'))at++; }
   bool Take(char c) { Space(); if(at<text.Length && text[at]==c) { at++; return true; } return false; }
   void Need(char c) { if(!Take(c))Bad(); }
   object Value(int depth) {
    if(depth>8)Bad(); Space(); if(at>=text.Length)Bad();
    if(allowNull&&at+4<=text.Length&&text.Substring(at,4)=="null"){at+=4;return null;}
    if(text[at]=='"')return Text();
    if(Take('{')) {
     var map=new Dictionary<string,object>(StringComparer.Ordinal); var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     if(Take('}'))return map;
     do { Space(); if(at>=text.Length || text[at]!='"')Bad(); string name=Text(); if(!seen.Add(name))Bad(); Need(':'); map.Add(name,Value(depth+1)); if(Take('}'))return map; Need(','); } while(true);
    }
    if(Take('[')) { var list=new List<object>(); if(Take(']'))return list; do { list.Add(Value(depth+1)); if(list.Count>MaxFiles)Bad(); if(Take(']'))return list; Need(','); } while(true); }
    int start=at; if(text[at]=='-')at++; if(at>=text.Length || text[at]<'0' || text[at]>'9')Bad();
    if(text[at]=='0')at++; else while(at<text.Length && text[at]>='0' && text[at]<='9')at++;
    long number; if(!long.TryParse(text.Substring(start,at-start),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out number))Bad(); return number;
   }
   string Text() {
    Need('"'); var result=new StringBuilder(); bool closed=false;
    while(at<text.Length) {
     char c=text[at++]; if(c=='"') { closed=true; break; } if(c<' ')Bad();
     if(c=='\\') {
      if(at>=text.Length)Bad(); c=text[at++];
      switch(c) {
       case '"': case '\\': case '/': break;
       case 'b': c='\b'; break; case 'f': c='\f'; break; case 'n': c='\n'; break; case 'r': c='\r'; break; case 't': c='\t'; break;
       case 'u': int value; if(at+4>text.Length || !int.TryParse(text.Substring(at,4),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out value)) { Bad(); return null; } c=(char)value; at+=4; break;
       default: Bad(); break;
      }
     }
     result.Append(c);
    }
    if(!closed)Bad(); string valueText=result.ToString();
    // Also reject unpaired surrogate escapes, which could re-encode differently.
    try { Utf8.GetByteCount(valueText); } catch(EncoderFallbackException) { Bad(); }
    return valueText;
   }
  }
 }
}
