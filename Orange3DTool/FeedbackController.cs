using System;using System.Collections.Generic;
namespace PZ3D {sealed partial class ModernController {
 FeedbackService feedback;
 internal static bool FeedbackAction(string action){return FeedbackService.Action(action);}
 object ExecuteFeedback(string action,Dictionary<string,object> payload){FeedbackService.Validate(action,payload);FeedbackService service;lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");service=feedback??(feedback=new FeedbackService(engine));}return action=="feedbackStatus"?service.Status():action=="feedbackPreview"?service.Preview(payload):service.Send(payload);}
 void DisposeFeedback(){lock(gate){if(feedback!=null){feedback.Dispose();feedback=null;}}}
}}
