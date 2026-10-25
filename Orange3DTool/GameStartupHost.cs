using System;
namespace PZ3D {
 sealed partial class ModernForm {
  GameStartupPresenter gameStartupPresenter;
  void InitializeStartupPresentation(){
   gameStartupPresenter=new GameStartupPresenter(this);
   gameStartupPresenter.PrelaunchClosed=id=>controller.CancelStartup(id);
   controller.StartupPresentation=launch=>{if(!capture)gameStartupPresenter.Handle(launch);};
   FormClosed+=(sender,args)=>{controller.StartupPresentation=null;gameStartupPresenter.Dispose();};
  }
 }
}
