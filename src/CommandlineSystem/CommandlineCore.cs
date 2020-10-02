using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CommandlineSystem
{
    public static class CommandlineCore
    {

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

        public static void Run(string[] args)
        {
            if (args.Length != 0)
            {
                Tools = GetSystemsTools();
                ICommandlineSystem selected = Tools.FirstOrDefault(x => x.Name == args[0]);
                selected?.Run(args.Skip(1).ToArray());
            }
            else
            {
                System.Console.WriteLine("Argument Mismatch");
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
                return new ICommandlineSystem[0];
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
