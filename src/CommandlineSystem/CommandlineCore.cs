using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CommandlineSystem
{
    public static class CommandlineCore
    {

        public static void Run(string[] args)
        {
            if (args.Length != 0)
            {
                ICommandlineSystem[] tools = GetBuildTools();
                ICommandlineSystem selected = tools.FirstOrDefault(x => x.Name == args[0]);
                selected?.Run(args.Skip(1).ToArray());
            }
            else
            {
                System.Console.WriteLine("Argument Mismatch");
            }

#if DEBUG
            System.Console.ReadLine();
#endif
        }

        private static ICommandlineSystem[] GetBuildTools(Assembly target)
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

        private static ICommandlineSystem[] GetBuildTools()
        {
            List<ICommandlineSystem> tools = new List<ICommandlineSystem>();
            string path = Path.Combine(
                                       Path.GetDirectoryName(
                                                             new Uri(Assembly.GetExecutingAssembly().Location)
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
                    tools.AddRange(GetBuildTools(asm));
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
