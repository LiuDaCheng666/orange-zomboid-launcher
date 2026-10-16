namespace PZ3D {
 // Keep old internal callers compatible, with cleanup as the only supported operation.
 sealed class LegacyPresentationBridge {
  public const string Name=PresentationBridge.Name,ConfigName=PresentationBridge.ConfigName;
  public string LastBackup;
  public bool Enabled(Engine e){return new PresentationBridge().Enabled(e);}
  public string Enable(Engine e,string source){return Disable(e);}
  public string Disable(Engine e){var retired=new PresentationBridge();string result=retired.Retire(e);LastBackup=retired.LastBackup;return result;}
 }
}
