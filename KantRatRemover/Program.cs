using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System;
using System.Drawing;
using System.Linq;
using Microsoft.Win32.TaskScheduler;
using Console = Colorful.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KantRatRemover
{
    internal static class Program
    {
        private static void Main()
        {
            var stopwatch = new Stopwatch();
            var minecraftPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\.minecraft";
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] kantDomains =
            {
                "gaymers.ax",
                "mvncentral.net",
                "vladvilcu2006.tech",
                "verble.software",
                "jonathanhardwick.me",
                "etc.catering",
                "tlrepo.cc",
                "batonrogue.tech"
            };
            var detectedJars = new List<string>();
            var ratted = false;

            stopwatch.Start();
            Console.Title = "Kant Rat Remover";

            var javaKilled = 0;
            foreach (var process in Process.GetProcessesByName("java"))
            {
                process.Kill();
                javaKilled++;
            }

            Console.WriteLine($"Killed {javaKilled} java processes", Color.GreenYellow);

            using (var service = new TaskService())
            {
                foreach (var task in service.RootFolder.Tasks)
                {
                    foreach (var action in task.Definition.Actions)
                    {
                        if (!(action.ActionType is TaskActionType.Execute)) continue;
                        var execAction = (ExecAction) action;
                        var onLogon =
                            task.Definition.Triggers.Any(trigger => trigger.TriggerType == TaskTriggerType.Logon);

                        if (!execAction.Path.ToLower().Contains("javaw") ||
                            !execAction.Arguments.ToLower().Contains("-jar") || !onLogon) continue;
                        service.RootFolder.DeleteTask(task.Name);
                        detectedJars.Add(task.Name);
                        ratted = true;
                    }
                }
            }

            if (detectedJars.Count > 0)
                Console.WriteLine($"Detected {detectedJars.Count} TaskScheduler task" +
                                  (detectedJars.Count > 1 ? "s" : "") + ". Deleted" +
                                  (detectedJars.Count > 1 ? "them" : "it") + ".", Color.Red);
            else
                Console.WriteLine("Detected 0 TaskScheduler tasks.", Color.SkyBlue);

            foreach (var jar in detectedJars.Where(jar => File.Exists($@"{programData}\{jar}\{jar}.jar")))
            {
                File.SetAttributes($@"{programData}\{jar}", FileAttributes.Normal);
                File.Delete($@"{programData}\{jar}\{jar}.jar");
                new DirectoryInfo($@"{programData}\{jar}\{jar}.jar").Attributes = FileAttributes.Normal;
                Directory.Delete($@"{programData}\{jar}");
                Console.WriteLine($@"Deleted {jar}.jar from {programData}.");
            }

            var count = 0;
            foreach (var version in Directory.GetDirectories($@"{minecraftPath}\versions"))
            foreach (var versionJson in Directory.GetFiles(version))
            {
                var fileName = Path.GetFileName(versionJson);
                if (!fileName.EndsWith(".json"))
                    continue;
                var cleared = false;
                JObject json;
                using (var streamReader = File.OpenText(versionJson))
                using (var reader = new JsonTextReader(streamReader))
                {
                    json = JObject.Load(reader);
                    var librairies = (JArray) json.SelectToken("libraries");
                    var mainClass = (JValue) json.SelectToken("mainClass");

                    if (mainClass?.Value != null && mainClass.Value.Equals("net.minecraft.client.main.Start"))
                    {
                        mainClass.Value = "net.minecraft.client.main.Main";
                        Console.WriteLine(
                            "Cleared mainClass from " +
                            fileName.Remove(fileName.IndexOf(".json", StringComparison.OrdinalIgnoreCase)),
                            Color.Yellow);
                        cleared = true;
                        count++;
                    }

                    if (librairies != null)
                        foreach (var token in librairies)
                        {
                            if (!(token is JObject)) continue;
                            var name = (JValue) token.SelectToken("name");
                            if (name?.Value != null && !name.Value.Equals("io.netty:netty-all:4.1.51.Final"))
                                continue;
                            token.Remove();
                            Console.WriteLine("Removed kant's dependency from " +
                                              fileName.Remove(fileName.IndexOf(".json",
                                                  StringComparison.OrdinalIgnoreCase)),
                                Color.Yellow);
                            if (!cleared) count++;
                            break;
                        }
                }
                File.WriteAllText(versionJson, json.ToString());
            }
            Console.WriteLine($"Cleared {count} versions successfully.", Color.SkyBlue);

            if (Directory.Exists($@"{minecraftPath}\libraries\io\netty\netty-all\4.1.51.Final"))
            {
                File.Delete($@"{minecraftPath}\libraries\io\netty\netty-all\4.1.51.Final\netty-all-4.1.51.Final.jar");
                Directory.Delete($@"{minecraftPath}\libraries\io\netty\netty-all\4.1.51.Final");
            }

            var blacklisted = 0;
            var hosts = File.ReadAllText($@"{Environment.SystemDirectory}\drivers\etc\hosts");
            try
            {
                using (var writer = File.AppendText($@"{Environment.SystemDirectory}\drivers\etc\hosts"))
                {
                    foreach (var domain in kantDomains)
                    {
                        if (hosts.Contains($"127.0.0.1    {domain}")) continue;
                        writer.WriteLine($"127.0.0.1    {domain}");
                        blacklisted++;
                    }
                }

                Console.WriteLine($"Blacklisted {blacklisted} domains.", Color.SkyBlue);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"Run this as admin to blacklist kant's domains.", Color.Red);
            }

            if (ratted) Console.WriteLine("You were ratted. Deleted the rat");

            Console.WriteLine($"Operation finished in {stopwatch.ElapsedMilliseconds} milliseconds");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }
    }
}
