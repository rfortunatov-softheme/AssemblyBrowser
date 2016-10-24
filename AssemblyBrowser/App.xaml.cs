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
        public static string AssembliesPath => Path.GetFullPath(Directory.Exists("Assemblies") ? "Assemblies" : @"..\..\..\..\..\Assemblies");

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

            var assembly = Assembly.LoadFrom(assemblyPath);
            return assembly;
        }
    }
}
