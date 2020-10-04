using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;

namespace CommandlineSystem
{
    internal enum UpdateType
    {

        Self,
        Systems

    }

    internal static class Bootstrap
    {

        public static void Update(UpdateType type, string url)
        {
            string pathAddition = type == UpdateType.Systems ? "systems" : null;
            InitiateBatchUpdate(type.ToString(), url, pathAddition);
        }

        private static void InitiateBatchUpdate(string type, string url, string pathAddition)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Assembly.GetEntryAssembly().GetName().Name, type);
            string selfTarget = Path.GetDirectoryName(new Uri(Assembly.GetEntryAssembly().CodeBase).AbsolutePath);
            string logTarget = Path.Combine(selfTarget, $"update_{type}.log");
            string downloadTarget = Path.Combine(tempPath, "update.zip");
            string extractTarget = Path.Combine(tempPath, "update");
            string updateBatchTarget = Path.Combine(tempPath, "update.bat");


            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            Directory.CreateDirectory(tempPath);


            Directory.CreateDirectory(extractTarget);

            DownloadFile(url, downloadTarget);

            ZipFile.ExtractToDirectory(downloadTarget, extractTarget);

            File.Delete(downloadTarget);

            List<string> batchUpdate = new List<string>
                                       {
                                           $"@ECHO OFF",
                                           $"echo Waiting for Process {Process.GetCurrentProcess().Id} to Close for Automatic Update>>{logTarget}",
                                           $":LOOP",
                                           $"tasklist | find /i \"{Process.GetCurrentProcess().Id}\" >nul 2>&1",
                                           $"IF ERRORLEVEL 1 (",
                                           $"GOTO CONTINUE",
                                           $") ELSE (",
                                           $"Timeout /T 5 /Nobreak",
                                           $"GOTO LOOP",
                                           $")",
                                           $":CONTINUE",
                                           $"echo Updating>>{logTarget}",
                                           $"xcopy {extractTarget} {Path.Combine(selfTarget, pathAddition??"")} /e /f /y>>{logTarget}",
                                           $"echo Update Complete.>>{logTarget}",
                                           $"ping localhost -n 2 > NUL",
                                           $"del {updateBatchTarget}"
                                       };
            File.WriteAllLines(updateBatchTarget, batchUpdate);
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", $"/C call {updateBatchTarget}");
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            Process.Start(info);
        }

        private static void DownloadFile(string url, string target)
        {
            using (WebClient wc = new WebClient())
            {
                wc.DownloadFile(url, target);
            }
        }

    }

    public static class CommandlineCore
    {

        private class BootstrapSystem : ICommandlineSystem
        {

            public string Name => "update";

            public void Run(string[] args)
            {
                foreach (string s in args)
                {
                    if (Enum.TryParse(s, true, out UpdateType type))
                    {
                        if (type == UpdateType.Self && ApplicationUpdateUrl == null || 
                            type == UpdateType.Systems && SystemUpdateUrl == null)
                        {
                            Console.WriteLine($"Can not Update {type}, no URL provided.");
                        }
                        else if (type == UpdateType.Self)
                        {
                            Bootstrap.Update(type, ApplicationUpdateUrl);
                        }
                        else if (type == UpdateType.Systems)
                        {
                            Bootstrap.Update(type, SystemUpdateUrl);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Can not parse: {s}");
                    }
                }
            }

        }


        private class HelpSystem : ICommandlineSystem
        {

            public string Name => "help";

            public void Run(string[] args)
            {
                foreach (ICommandlineSystem commandlineSystem in Tools)
                {
                    Console.WriteLine($"Tool: {Path.GetFileName(Assembly.GetEntryAssembly().CodeBase)} {commandlineSystem.Name}");
                }
            }

        }

        private static ICommandlineSystem[] Tools;
        private static string ApplicationUpdateUrl;
        private static string SystemUpdateUrl;

        public static void Run(string[] args, string applicationUpdateUrl = null, string systemUpdateUrl=null)
        {
            ApplicationUpdateUrl = applicationUpdateUrl;
            SystemUpdateUrl = systemUpdateUrl;
            Tools = GetSystemsTools();
            if (args.Length != 0)
            {
                ICommandlineSystem selected = Tools.FirstOrDefault(x => x.Name == args[0]);
                selected?.Run(args.Skip(1).ToArray());
            }
            else
            {
                System.Console.WriteLine("Argument Mismatch");
                Tools.First(x => x is HelpSystem).Run(new string[0]);
            }

#if DEBUG
            System.Console.WriteLine("Press any key to exit..");
            System.Console.ReadLine();
#endif
        }

        private static ICommandlineSystem[] GetSystemsTools(Assembly target)
        {
            Type[] asmTypes = target
                              .GetTypes().Where(
                                                x => typeof(ICommandlineSystem).IsAssignableFrom(x) &&
                                                     !x.IsAbstract &&
                                                     !x.IsInterface
                                               ).ToArray();
            ICommandlineSystem[] ret = new ICommandlineSystem[asmTypes.Length];
            for (int i = 0; i < asmTypes.Length; i++)
            {
                Type asmType = asmTypes[i];
                ret[i] = (ICommandlineSystem)Activator.CreateInstance(asmType);
            }

            return ret;
        }

        private static ICommandlineSystem[] GetSystemsTools()
        {
            List<ICommandlineSystem> tools = new List<ICommandlineSystem>();
            tools.Add(new HelpSystem());
            tools.Add(new BootstrapSystem());
            Assembly asmbly = Assembly.GetExecutingAssembly();
            string path = Path.Combine(
                                       Path.GetDirectoryName(
                                                             new Uri(asmbly.CodeBase)
                                                                 .AbsolutePath
                                                            ),
                                       "systems"
                                      );
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return tools.ToArray();
            }

            string[] files = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                try
                {
                    Assembly asm = Assembly.LoadFrom(file);
                    tools.AddRange(GetSystemsTools(asm));
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("Loading " + file + " failed.");
                }
            }

            return tools.ToArray();
        }

    }
}
