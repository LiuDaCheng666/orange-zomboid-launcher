using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
namespace PZ3D {
 sealed class SafehouseCatalogHouse {
  public string id,name,title,owner,location;public string[] members;
  public int x,y,width,height;public SafehouseTransferBounds bounds;
 }
 sealed class SafehouseCatalogResult {
  public string sourceId,state="unreadable",message="",stamp="";public bool detected,readable,manualFallback=true;public int worldVersion;
  public SafehouseCatalogHouse[] houses=new SafehouseCatalogHouse[0];
 }
 // Read only the verified map_meta prefix through the complete SafeHouse list.
 // IsoMetaGrid's later Stash/RDS payload varies with SP/server/client mode and
 // is deliberately not interpreted or represented as whole-world validation.
 static class SafehouseCatalog {
  const int MaximumBytes=64*1024*1024,MaximumCells=1000000,MaximumClaims=1024,MaximumMembers=1024,MaximumText=262144;
  static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  sealed class Unsupported:Exception {internal Unsupported():base("不支持的安全屋元数据版本。") {}}
  sealed class Reader {
   readonly byte[] bytes;internal int position,textCharacters,textCount;
   internal Reader(byte[] value){bytes=value;}
   internal void Need(int count){if(count<0||position<0||count>bytes.Length-position)throw new InvalidDataException("安全屋元数据已截断或长度越界。");}
   internal int Byte(){Need(1);return bytes[position++];}
   internal int Int(){return unchecked((int)(((uint)Byte()<<24)|((uint)Byte()<<16)|((uint)Byte()<<8)|(uint)Byte()));}
   internal void Skip(int count){Need(count);position+=count;}
   internal int Count(int maximum){int count=Int();if(count<0||count>maximum)throw new InvalidDataException("安全屋元数据条目数量无效或超过读取预算。");return count;}
   internal string Text(){if(++textCount>16384)throw new InvalidDataException("安全屋姓名及文本条目超过显示预算。");int length=unchecked((short)((Byte()<<8)|Byte()));if(length<0)throw new InvalidDataException("安全屋文本长度无效。");Need(length);string value;try{value=Utf8.GetString(bytes,position,length);}catch(DecoderFallbackException ex){throw new InvalidDataException("安全屋文本不是有效 UTF-8。",ex);}position+=length;textCharacters=checked(textCharacters+value.Length);if(textCharacters>MaximumText)throw new InvalidDataException("安全屋文本超过显示预算。");return value;}
  }
  static string Hash(byte[] value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(value)).Replace("-","").ToLowerInvariant();}
  static byte[] Read(string path){LauncherMaintenance.NoReparse(path);using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(input.Length<28||input.Length>MaximumBytes)throw new InvalidDataException("安全屋元数据大小无效或超过读取预算。");var result=new byte[(int)input.Length];int offset=0,count;while(offset<result.Length&&(count=input.Read(result,offset,result.Length-offset))>0)offset+=count;if(offset!=result.Length||input.Length!=result.Length)throw new IOException("安全屋元数据在读取期间变化，请刷新。");return result;}}
  // Hold the native source snapshot against writes/deletion for the complete
  // target transaction. Resolve again after opening so a pre-open change cannot
  // authorize the old selection. No caller-supplied filesystem path is exposed.
  public static IDisposable WithSelectionLease(Engine e,string sourceId,string stamp,string houseId){if(e==null)throw new ArgumentNullException("e");string save=SaveProfiles.ResolveSave(e,sourceId),path=Path.Combine(save,"map_meta.bin");LauncherMaintenance.NoReparse(path);var lease=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);try{Resolve(e,sourceId,stamp,houseId);return lease;}catch{lease.Dispose();throw;}}
  public static SafehouseTransferBounds Resolve(Engine e,string sourceId,string stamp,string houseId){var current=List(e,sourceId);if(!current.readable||!current.detected)throw new IOException("此存档没有可核实的安全屋登记，请刷新或手动选择房屋范围。");if(!string.Equals(stamp,current.stamp,StringComparison.Ordinal))throw new IOException("安全屋登记已变化，请刷新并重新选择安全屋。");foreach(var house in current.houses)if(string.Equals(house.id,houseId,StringComparison.Ordinal))return new SafehouseTransferBounds{minX=house.bounds.minX,minY=house.bounds.minY,maxX=house.bounds.maxX,maxY=house.bounds.maxY};throw new IOException("安全屋选择已失效，请刷新并重新选择安全屋。");}
  public static SafehouseCatalogResult List(Engine e,string sourceId){if(e==null)throw new ArgumentNullException("e");var result=new SafehouseCatalogResult{sourceId=sourceId};try{
   string save=SaveProfiles.ResolveSave(e,sourceId),path=Path.Combine(save,"map_meta.bin");LauncherMaintenance.NoReparse(path);if(!File.Exists(path)){result.state="missing";result.message="此存档没有安全屋登记文件，可手动选择房屋范围。";return result;}
   byte[] bytes=Read(path);result.stamp=Hash(bytes);var r=new Reader(bytes);if(r.Byte()!='M'||r.Byte()!='E'||r.Byte()!='T'||r.Byte()!='A')throw new InvalidDataException("安全屋元数据头部无效。");result.worldVersion=r.Int();if(result.worldVersion!=247&&result.worldVersion!=249)throw new Unsupported();int minX=r.Int(),minY=r.Int(),maxX=r.Int(),maxY=r.Int();long columns=(long)maxX-minX+1,rows=(long)maxY-minY+1;if(columns<1||rows<1||columns>MaximumCells||rows>MaximumCells||columns*rows>MaximumCells)throw new InvalidDataException("安全屋元数据的地图单元范围无效或超过读取预算。");
   // IsoMetaGrid.load: room meta-id(long)+flags(short), building meta-id(long)
   // +alarmed(byte)+key(int)+seen(byte)+visited(byte)+respawn(int)+decay(int).
   for(long cell=0;cell<columns*rows;cell++){r.Skip(checked(r.Count(1000000)*10));r.Skip(checked(r.Count(1000000)*23));}
   int count=r.Count(MaximumClaims);var houses=new List<SafehouseCatalogHouse>();for(int index=0;index<count;index++){
    int x=r.Int(),y=r.Int(),width=r.Int(),height=r.Int();long right=(long)x+width-1,bottom=(long)y+height-1;if(width<1||height<1||width>1000000||height>1000000||right>int.MaxValue||right<int.MinValue||bottom>int.MaxValue||bottom<int.MinValue)throw new InvalidDataException("安全屋登记的房屋边界无效。");
    string owner=r.Text();r.Skip(4);int memberCount=r.Count(MaximumMembers);var members=new string[memberCount];for(int member=0;member<memberCount;member++)members[member]=r.Text();r.Skip(8);string title=r.Text();r.Skip(8);string location=r.Text();int respawnCount=r.Count(MaximumMembers);for(int member=0;member<respawnCount;member++)r.Text();
    houses.Add(new SafehouseCatalogHouse{id=Hash(Utf8.GetBytes(sourceId+"\n"+result.stamp+"\n"+index)).Substring(0,32),name=title.Length>0?title:owner.Length>0?owner+" 的安全屋":"安全屋 "+(index+1),title=title,owner=owner,location=location,members=members,x=x,y=y,width=width,height=height,bounds=new SafehouseTransferBounds{minX=x,minY=y,maxX=(int)right,maxY=(int)bottom}});
   }
   result.houses=houses.ToArray();result.readable=true;result.detected=houses.Count>0;result.manualFallback=!result.detected;result.state=result.detected?"detected":"none";result.message=result.detected?"已从存档登记中找到 "+houses.Count+" 处安全屋，请选择要迁移的房屋。":"此存档没有安全屋登记。普通单人存档通常不会登记安全屋，可手动选择房屋范围。";
  }catch(Unsupported){result.state="unsupported";result.message="暂不支持此存档的安全屋登记版本（"+result.worldVersion+"），可手动选择房屋范围。";}catch(Exception error){if(!(error is IOException)&&!(error is InvalidDataException)&&!(error is UnauthorizedAccessException)&&!(error is ArgumentException)&&!(error is OverflowException)&&!(error is System.Security.SecurityException)&&!(error is InvalidOperationException)&&error.GetType()!=typeof(Exception))throw;result.state="unreadable";result.message="无法安全读取此存档的安全屋登记，请确认存档已保存并退出游戏；也可手动选择房屋范围。";}
   return result;
  }
 }
}
