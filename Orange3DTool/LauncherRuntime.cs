using System;
namespace PZ3D {static class LauncherRuntime {public static void Initialize(){AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);}}}
