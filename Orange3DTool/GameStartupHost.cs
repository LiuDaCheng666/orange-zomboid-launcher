using System;
namespace PZ3D {
 sealed partial class ModernForm {
  GameStartupObserver gameStartupObserver;
  void InitializeStartupPresentation(){
   gameStartupObserver=new GameStartupObserver();
   controller.StartupPresentation=launch=>{if(!capture)gameStartupObserver.Handle(launch);};
   FormClosed+=(sender,args)=>{controller.StartupPresentation=null;gameStartupObserver.Dispose();};
  }
 }
}
