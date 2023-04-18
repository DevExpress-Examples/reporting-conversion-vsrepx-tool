using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelperProject {
    class Program {
        public static readonly string ProjectName = "ClassLibrary1";

        static void Main(string[] args) {
            //string projectPath = GetCanonicalPath(Environment.CurrentDirectory + @"\..\..\..\ClassLibrary1\" + ProjectName + ".csproj");
            //Convert(projectPath);
        }
        static string GetCanonicalPath(string v) {
            return new Uri(v).LocalPath;
        }
        public static void Convert(string projectPath) {
            string projectDirectory = Path.GetDirectoryName(projectPath);
            string layoutsDirectory = Path.Combine(Path.GetDirectoryName(projectDirectory), "Layouts");

            var reports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in EnumerateRepxFiles(projectDirectory)) {
                var relativePath = file.Substring(projectDirectory.Length + 1);
                reports.Add(relativePath);
                Console.WriteLine(file);
                ProcessFile(file);
                MoveFile(projectDirectory, layoutsDirectory, relativePath);
            }
            ProcessProject(projectPath, reports);
        }

        public static IEnumerable<string> EnumerateRepxFiles(string directory) {
            return Directory.EnumerateFiles(directory, "*.vsrepx", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(directory, "*.repx", SearchOption.AllDirectories));
        }
        public static IEnumerable<string> EnumerateRepxDirectorys(string directory) {
            return Directory.EnumerateDirectories(directory, "*.vsrepx", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateDirectories(directory, "*.repx", SearchOption.AllDirectories));
        }

        static void MoveFile(string projectDirectory, string layoutsDirectory, string relativePath) {
            var target = Path.Combine(layoutsDirectory, relativePath);
            var source = Path.Combine(projectDirectory, relativePath);
            var targetFile = Path.Combine(target, "report.repx");
            if(!Directory.Exists(target))
                Directory.CreateDirectory(target);
            if(File.Exists(targetFile))
                File.Delete(targetFile);
            File.Copy(source, targetFile);
            File.Delete(source);
        }
        static void ProcessFile(string file) {
            var folder = Path.GetDirectoryName(file);
            var name = Path.GetFileNameWithoutExtension(file);
            var baseClass = ProcessDesignFile(Path.Combine(folder, name + ".Designer.cs"));
            ProcessCodeFile(Path.Combine(folder, name + ".cs"), baseClass);
            ProcessRepxFile(file);
        }
        static void ProcessRepxFile(string file) {
            string text = File.ReadAllText(file);
            text = Regex.Replace(text, @"<Item1 Ref=""(\d+)"" Key=""VSReportExtInfo"" Value="".vsrepx"" />", @"<Item1 Ref=""$1"" Key=""Skip"" Value=""Skip"" />");
            File.WriteAllText(file, text);
        }
        static string ProcessDesignFile(string file) {
            var lines = File.ReadAllLines(file);
            var newLines = new List<string>();
            string baseClass = "";
            foreach(var line in lines) {
                Match m;
                m = Regex.Match(line, @"partial class \S+\s*:\s*(\S+)");
                if(m.Success)
                    baseClass = m.Groups[1].Value;
                if(line.IndexOf("void InitializeComponent()", StringComparison.OrdinalIgnoreCase) >= 0) {
                    newLines.Add(line);
                    if(!line.Contains("{"))
                        newLines.Add("{");
                    newLines.Add("}}}");
                    break;
                }
                newLines.Add(line);
            }
            File.WriteAllLines(file, newLines);
            return baseClass;
        }
        static void ProcessCodeFile(string file, string baseClass) {
            var lines = File.ReadAllLines(file);
            var newLines = new List<string>();
            foreach(var line in lines) {
                string newLine = line;
                var m = Regex.Match(line, @"partial class \S+");
                if(m.Success) {
                    newLine = line.Replace(m.Value, m.Value + " : " + baseClass);
                }
                newLines.Add(newLine);
            }
            File.WriteAllLines(file, newLines);
        }
        static void ProcessProject(string file, HashSet<string> reports) {
            var lines = File.ReadAllLines(file);
            var newLines = new List<string>();

            bool inCompileUpdate = false;
            string baseFile = "";
            foreach(var line in lines) {
                string newLine = line;
                Match m;
                if(!inCompileUpdate) {
                    m = Regex.Match(line, @"<None Remove=""([^""]+)""\s*/>");
                    if(m.Success && reports.Contains(m.Groups[1].Value))
                        continue;
                    m = Regex.Match(line, @"<EmbeddedResource Include=""([^""]+)""\s*/>");
                    if(m.Success && reports.Contains(m.Groups[1].Value))
                        continue;

                    m = Regex.Match(line, @"<Compile Update=""([^""]+)""\s*>");
                    if(m.Success && (
                        reports.Contains(Regex.Replace(m.Groups[1].Value, @"\.cs", ".vsrepx", RegexOptions.IgnoreCase)) ||
                        reports.Contains(Regex.Replace(m.Groups[1].Value, @"\.cs", ".repx", RegexOptions.IgnoreCase)) ||
                        reports.Contains(Regex.Replace(m.Groups[1].Value, @"\.designer\.cs", ".vsrepx", RegexOptions.IgnoreCase)) ||
                        reports.Contains(Regex.Replace(m.Groups[1].Value, @"\.designer\.cs", ".repx", RegexOptions.IgnoreCase))
                        )) {
                        inCompileUpdate = true;
                        baseFile = m.Groups[1].Value;
                    }
                } else {
                    if(line.Contains("DependentUpon")) {
                        if(baseFile.IndexOf(".Designer.", StringComparison.OrdinalIgnoreCase) >= 0) {
                            newLine = Regex.Replace(newLine, @"\.vsrepx", ".cs", RegexOptions.IgnoreCase);
                            newLine = Regex.Replace(newLine, @"\.repx", ".cs", RegexOptions.IgnoreCase);
                        } else
                            continue;
                    } else if(line.Contains("</Compile>")) {
                        inCompileUpdate = false;
                        baseFile = "";
                    }
                }

            newLines.Add(newLine);
        }
        File.WriteAllLines(file, newLines);
        }
}
}
