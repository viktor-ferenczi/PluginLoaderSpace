﻿using avaness.PluginLoader.Compiler;
using avaness.PluginLoader.GUI;
using Sandbox;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using VRage;
using VRage.Utils;

namespace avaness.PluginLoader.Data
{
    public class LocalFolderPlugin : PluginData
    {
        const string XmlDataType = "Xml files (*.xml)|*.xml|All files (*.*)|*.*";

        public override string Source => MyTexts.GetString(MyCommonTexts.Local);
        private string[] sourceDirectories;

        public Config PathInfo { get; }

        public LocalFolderPlugin(string folder, string xmlFile)
        {
            Id = folder;
            FriendlyName = Path.GetFileName(folder);
            Status = PluginStatus.None;
            PathInfo = new Config(folder, xmlFile);
            DeserializeFile(xmlFile);
        }

        private LocalFolderPlugin(string folder)
        {
            Id = folder;
            Status = PluginStatus.None;
            PathInfo = new Config()
            {
                Folder = folder
            };
        }

        public override Assembly GetAssembly()
        {
            if (Directory.Exists(Id))
            {
                RoslynCompiler compiler = new RoslynCompiler();
                bool hasFile = false;
                StringBuilder sb = new StringBuilder();
                sb.Append("Compiling files from ").Append(Id).Append(":").AppendLine();
                foreach(var file in GetProjectFiles(Id))
                {
                    using (FileStream fileStream = File.OpenRead(file))
                    {
                        hasFile = true;
                        sb.Append(file, Id.Length, file.Length - Id.Length).Append(", ");
                        compiler.Load(fileStream, Path.GetFileName(file));
                    }
                }

                if(hasFile)
                {
                    sb.Length -= 2;
                    LogFile.WriteLine(sb.ToString());
                }
                else
                    return null;

                byte[] data = compiler.Compile(FriendlyName + '_' + Path.GetRandomFileName());
                Assembly a = Assembly.Load(data);
                Version = a.GetName().Version;
                return a;
            }
            return null;
        }

        private IEnumerable<string> GetProjectFiles(string folder)
        {
            string gitOutput = null;
            try
            {
                Process p = new Process();

                // Redirect the output stream of the child process.
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.FileName = "git";
                p.StartInfo.Arguments = "ls-files --cached --others --exclude-standard";
                p.StartInfo.WorkingDirectory = folder;
                p.Start();

                // Do not wait for the child process to exit before
                // reading to the end of its redirected stream.
                // Read the output stream first and then wait.
                gitOutput = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    string[] files = gitOutput.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return files.Where(x => x.EndsWith(".cs") && IsValidProjectFile(x)).Select(x => Path.Combine(folder, x.Trim().Replace('/', Path.DirectorySeparatorChar)));
                }
                else
                {
                    StringBuilder sb = new StringBuilder("An error occurred while checking git for project files.");
                    if (!string.IsNullOrWhiteSpace(gitOutput))
                    {
                        sb.AppendLine("Git output: ");
                        sb.Append(gitOutput).AppendLine();
                    }
                    LogFile.WriteLine(sb.ToString());
                }
            }
            catch (Exception e) 
            {
                StringBuilder sb = new StringBuilder("An error occurred while checking git for project files.");
                if(!string.IsNullOrWhiteSpace(gitOutput))
                {
                    sb.AppendLine("Git output: ");
                    sb.Append(gitOutput).AppendLine();
                }
                sb.AppendLine("Exception: ");
                sb.Append(e);
                LogFile.WriteLine(sb.ToString());
            }


            char sep = Path.DirectorySeparatorChar;
            return Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Where(x => !x.Contains(sep + "bin" + sep) && !x.Contains(sep + "obj" + sep) && IsValidProjectFile(x));
        }

        private bool IsValidProjectFile(string file)
        {
            if (sourceDirectories == null || sourceDirectories.Length == 0)
                return true;
            file = file.Replace('\\', '/');
            foreach(string dir in sourceDirectories)
            {
                if (file.StartsWith(dir))
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Id;
        }

        public override void Show()
        {
            string folder = Path.GetFullPath(Id);
            if (Directory.Exists(folder))
                Process.Start("explorer.exe", $"\"{folder}\"");
        }

        public override bool OpenContextMenu(MyGuiControlContextMenu menu)
        {
            menu.Clear();
            menu.AddItem(new StringBuilder("Remove"));
            menu.AddItem(new StringBuilder("Load data file"));
            return true;
        }

        public override void ContextMenuClicked(MyGuiScreenPluginConfig screen, MyGuiControlContextMenu.EventArgs args)
        {
            switch (args.ItemIndex)
            {
                case 0:
                    Main.Instance.Config.PluginFolders.Remove(Id);
                    screen.RemovePlugin(this);
                    break;
                case 1:
                    LoaderTools.OpenFileDialog("Open an xml data file", Path.GetDirectoryName(PathInfo.DataFile), XmlDataType, (file) => DeserializeFile(file, screen));
                    break;
            }
        }

        // Deserializes a file and refreshes the plugin screen
        private void DeserializeFile(string file, MyGuiScreenPluginConfig screen = null)
        {
            if (!File.Exists(file))
                return;

            try
            {
                XmlSerializer xml = new XmlSerializer(typeof(PluginData));

                using (StreamReader reader = File.OpenText(file))
                {
                    object resultObj = xml.Deserialize(reader);
                    if(resultObj.GetType() != typeof(GitHubPlugin))
                    {
                        throw new Exception("Xml file is not of type GitHubPlugin!");
                    }

                    GitHubPlugin github = (GitHubPlugin)resultObj;
                    github.Init(LoaderTools.PluginsDir);
                    FriendlyName = github.FriendlyName;
                    Tooltip = github.Tooltip;
                    Author = github.Author;
                    Description = github.Description;
                    sourceDirectories = github.SourceDirectories;
                    PathInfo.DataFile = file;
                    if(screen != null && screen.Visible && screen.IsOpened)
                        screen.RefreshSidePanel();
                }
            }
            catch (Exception e)
            {
                LogFile.WriteLine("Error while reading the xml file: " + e);
            }
        }

        public static void CreateNew(Action<LocalFolderPlugin> onComplete)
        {
            LoaderTools.OpenFolderDialog("Open the root of your project", LoaderTools.PluginsDir, (folder) =>
            {
                if (Main.Instance.List.Contains(folder))
                {
                    MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Error, messageText: new StringBuilder("That folder already exists in the list!"));
                    return;
                }

                LocalFolderPlugin plugin = new LocalFolderPlugin(folder);
                LoaderTools.OpenFileDialog("Open the xml data file", folder, XmlDataType, (file) => 
                {
                    plugin.DeserializeFile(file);
                    onComplete(plugin);
                });
            });
        }


        public class Config
        {
            public Config() { }

            public Config(string folder, string dataFile)
            {
                Folder = folder;
                DataFile = dataFile;
            }

            public string Folder { get; set; }
            public string DataFile { get; set; }
        }
    }
}
