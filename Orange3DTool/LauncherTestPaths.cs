using System;using System.IO;
namespace PZ3D {static class LauncherTestPaths {public static string Root{get{string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestFixtures");Directory.CreateDirectory(root);return root;}}}}
