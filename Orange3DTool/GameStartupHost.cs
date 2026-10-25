using System;
namespace PZ3D {
 sealed partial class ModernForm {
  GameStartupPresenter gameStartupPresenter;
  void InitializeStartupPresentation(){
   gameStartupPresenter=new GameStartupPresenter(this);
   controller.StartupPresentation=launch=>{if(!capture)gameStartupPresenter.Handle(launch);};
   FormClosed+=(sender,args)=>{controller.StartupPresentation=null;gameStartupPresenter.Dispose();};
  }
 }
}
