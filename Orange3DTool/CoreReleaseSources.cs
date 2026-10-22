using System;
using System.IO;
using System.Net;
using System.Threading;

namespace PZ3D {
 // Only transport locations change. Original GitHub-format bytes stay signed and
 // are verified again by the update service before planning any local changes.
 internal static class CoreReleaseSources {
  const string Origin="https://orangezl.com";
  internal static byte[][] Lookup(CancellationToken token,Func<string,CancellationToken,Stream> open,Func<CancellationToken,byte[][]> github,string key,string current) {
   byte[] raw,signature;
   try {
    token.ThrowIfCancellationRequested();
    using(var stream=open(Origin+"/updates/latest/update-manifest.json",token))raw=CoreReleaseNetwork.ReadBounded(stream,CoreReleaseProtocol.MaxManifestBytes,token);
    token.ThrowIfCancellationRequested();
    // The version is only an untrusted routing hint to a fixed origin. The
    // immutable signature avoids mixing two releases during a latest switch.
    string version=CoreReleaseProtocol.ParseManifest(raw).version;CoreReleaseProtocol.ParseVersion(version);
    using(var stream=open(Origin+"/releases/v"+version+"/update-manifest.sig",token))signature=CoreReleaseNetwork.ReadBounded(stream,1024,token);
   } catch(Exception error) {
    token.ThrowIfCancellationRequested();if(!TransportFailure(error))throw;
    DisposeResponse(error);return github(token);
   }
   token.ThrowIfCancellationRequested();
   var manifest=CoreReleaseProtocol.Verify(raw,signature,key,"0.0.0");
   if(CoreReleaseProtocol.ParseVersion(manifest.version)<CoreReleaseProtocol.ParseVersion(current))return github(token);
   return new[]{raw,signature};
  }
  internal static string[] AssetUrls(string signedUrl) {
   if(signedUrl==null||!signedUrl.StartsWith(CoreReleaseProtocol.ReleaseBase,StringComparison.Ordinal))throw new InvalidDataException("Untrusted release URL.");
   string relative=signedUrl.Substring(CoreReleaseProtocol.ReleaseBase.Length);int slash=relative.IndexOf('/');
   if(slash<2||relative[0]!='v')throw new InvalidDataException("Invalid release asset URL.");
   string version=relative.Substring(1,slash-1);CoreReleaseProtocol.ParseVersion(version);CoreReleaseProtocol.ValidateUrl(signedUrl,version);
   return new[]{Origin+"/releases/"+relative,signedUrl};
  }
  internal static bool Allowed(Uri uri) {
   if(uri.Scheme!="https"||uri.Host!="orangezl.com"||!uri.IsDefaultPort||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")return false;
   if(uri.AbsoluteUri==Origin+"/updates/latest/update-manifest.json"||uri.AbsoluteUri==Origin+"/updates/latest/update-manifest.sig")return true;
   if(!uri.AbsoluteUri.StartsWith(Origin+"/releases/",StringComparison.Ordinal))return false;
   try{AssetUrls(CoreReleaseProtocol.ReleaseBase+uri.AbsoluteUri.Substring((Origin+"/releases/").Length));return true;}catch(InvalidDataException){return false;}
  }
  internal static bool TransportFailure(Exception error){return error is WebException||error is IOException&&!(error is InvalidDataException);}
  internal static void DisposeResponse(Exception error){var web=error as WebException;if(web!=null&&web.Response!=null)web.Response.Dispose();}
 }
}
