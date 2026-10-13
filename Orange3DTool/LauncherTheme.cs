using System.Drawing;
using System.IO;
namespace PZ3D {
 sealed class LauncherTheme {
  public bool Light;public Color Background,Surface,Text,Muted,Accent,Border;
  public static LauncherTheme Create(bool light){return new LauncherTheme{Light=light,Background=light?Color.FromArgb(232,240,250):Color.FromArgb(26,29,34),Surface=light?Color.FromArgb(248,251,255):Color.FromArgb(38,42,49),Text=light?Color.FromArgb(33,54,78):Color.FromArgb(240,240,235),Muted=light?Color.FromArgb(85,112,139):Color.FromArgb(165,174,181),Accent=light?Color.FromArgb(57,126,207):Color.FromArgb(223,179,87),Border=light?Color.FromArgb(166,193,224):Color.FromArgb(100,87,61)};}
  public static LauncherTheme Load(string directory){try{return Create(File.ReadAllText(Path.Combine(directory,"launcher-theme.txt")).Trim()=="light");}catch{return Create(false);}}
  public void Save(string directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"launcher-theme.txt"),Light?"light":"dark");}
 }
}
