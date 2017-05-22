using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace AssemblyBrowser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string AssembliesPath => Path.GetFullPath(Directory.Exists("Debug") ? "Debug" : @"..\..\..\..\..\..\appassure\bin\Debug");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += new ResolveEventHandler(LoadFromSameFolder);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var procs = Process.GetProcessesByName("dot");
            foreach (var process in procs)
            {
                process.Kill();
                process.WaitForExit();
            }

            base.OnExit(e);
        }

        private static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
        {
            var assemblyPath = Path.Combine(AssembliesPath, new AssemblyName(args.Name).Name + ".dll");
            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            using (Stream stream = File.OpenRead(assemblyPath))
            {
                byte[] rawAssembly = new byte[stream.Length];
                stream.Read(rawAssembly, 0, (int)stream.Length);
                return Assembly.Load(rawAssembly);
            }
        }
    }
}
