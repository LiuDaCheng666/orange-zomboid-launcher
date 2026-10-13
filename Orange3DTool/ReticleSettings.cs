using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class ReticleSettings {
  public bool StartNative{get;set;}
  public string KeyText{get;set;}
  public ReticleSettings(){StartNative=true;KeyText="";}
  public static ReticleSettings Load(string data){string p=Path.Combine(data,"aim-fallback.json");return File.Exists(p)?new JavaScriptSerializer().Deserialize<ReticleSettings>(File.ReadAllText(p)):new ReticleSettings();}
  public void Save(string data){Properties("validation");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"aim-fallback.json"),new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));}
  public string Properties(string author){string text=(KeyText??"").Trim().ToUpperInvariant();string[] parts=text.Length==0?new string[0]:text.Split('+');bool ctrl=parts.Contains("CTRL"),shift=parts.Contains("SHIFT"),alt=parts.Contains("ALT");var keys=parts.Where(p=>p!="CTRL"&&p!="SHIFT"&&p!="ALT").ToArray();if(parts.Distinct().Count()!=parts.Length||keys.Length!=(parts.Length==0?0:1))throw new Exception("快捷键需一个主键，可加 CTRL / SHIFT / ALT；按退格清空。");int code=0;if(keys.Length==1){string key=keys[0];string[] letters={"A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"};int[] codes={30,48,46,32,18,33,34,35,23,36,37,38,50,49,24,25,16,19,31,20,22,47,17,45,21,44};int i=Array.IndexOf(letters,key),n;if(i>=0)code=codes[i];else if(key.StartsWith("F")&&int.TryParse(key.Substring(1),out n)&&n>=1&&n<=12)code=n<=10?58+n:n==11?87:88;else if(key.Length==1&&char.IsDigit(key[0]))code=key=="0"?11:key[0]-'0'+1;else throw new Exception("支持字母、数字、F1–F12及CTRL/SHIFT/ALT组合；此键暂不支持。");}
   return "shoot.jar="+author.Replace("\\","\\\\")+"\naim.mode="+(StartNative?"native":"extension")+"\naim.key="+code+"\naim.ctrl="+ctrl.ToString().ToLowerInvariant()+"\naim.shift="+shift.ToString().ToLowerInvariant()+"\naim.alt="+alt.ToString().ToLowerInvariant()+"\n";
  }
 }
}
