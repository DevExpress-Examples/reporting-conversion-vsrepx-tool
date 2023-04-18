using DevExpress.XtraReports.Import;
using DevExpress.XtraReports.UI;
using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelperProject {
    [Designer(typeof(Class2Desinger))]
    [DesignerCategory("Code")]
    public class ConverterComponent : Component {
        public ConverterComponent() {
        }
        public ConverterComponent(IContainer container)
                : this() {
            container.Add(this);
        }
    }
}
namespace HelperProject {
    class Class2Desinger : ComponentDesigner {
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
        }
        public Class2Desinger() {
            Verbs.Add(new DesignerVerb("Convert", TestHandler));
        }

        async void TestHandler(object sender, EventArgs e) {
            try {
                Debug.WriteLine("start", "RRR");
                var dte = (DTE2)GetService(typeof(DTE));

                string projectName = HelperProject.Program.ProjectName;
                Project project = GetProject(dte, projectName);
                string projectPath = project.FileName;

                Program.Convert(projectPath);
                await Task.Delay(2000);

                string projectDirectory = Path.GetDirectoryName(projectPath);
                string layoutsDirectory = Path.Combine(Path.GetDirectoryName(projectDirectory), "Layouts");

                foreach (var directory in Program.EnumerateRepxDirectorys(layoutsDirectory)) {
                    var file = Path.Combine(directory, "report.repx");
                    var relativePath = directory.Substring(layoutsDirectory.Length + 1);
                    var csName = GetCsName(relativePath);
                    var projectItem = GetProjectItem(project, csName);
                    Debug.WriteLine(projectItem.Name, "RRR");

                    await WaitSubType(projectItem);
                    var window = await OpenWindow(projectItem);
                    var host = window.Object as IDesignerHost;

                    var targetReport = (XtraReport)host.RootComponent;
                    new ReportConverterRunner((XtraReport)host.RootComponent, host).Run(() => {
                        var converter = new RepxConverter { UseExpressionBindings = true };
                        ConversionResult res = converter.Convert(file);
                        res.TargetReport.Name = targetReport.Name;
                        return res;
                    });

                    window.Close(vsSaveChanges.vsSaveChangesYes);
                }

                Debug.WriteLine("finish", "RRR");

            }
            catch (Exception ex) {
                foreach (string el in ex.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                    Debug.WriteLine(el, "RRR");
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }
        static string GetCsName(string relativePath) {
            relativePath = Regex.Replace(relativePath, @"\.vsrepx", ".cs", RegexOptions.IgnoreCase);
            relativePath = Regex.Replace(relativePath, @"\.repx", ".cs", RegexOptions.IgnoreCase);
            return relativePath;
        }
        static Project GetProject(DTE2 dte, string projectName) {
            Project project = null;
            for (int i = 1; i <= dte.Solution.Projects.Count; i++) {
                var prj = dte.Solution.Projects.Item(i);
                if (prj.Name == projectName) {
                    project = prj;
                    break;
                }
                //Debug.WriteLine(prj.Name, "RRR");
            }
            if (project == null)
                throw new ArgumentException(string.Format("Cannot find project {0}", projectName));
            return project;
        }
        ProjectItem GetProjectItem(Project project, string name) {
            ProjectItems projectItems = project.ProjectItems;
            string[] path = name.Split('\\');
            if (path.Length > 1) {
                for (int i = 0; i < path.Length - 1; i++) {
                    projectItems = projectItems.Item(path[i]).ProjectItems;
                }
                name = path[path.Length - 1];
                //Debug.WriteLine(name, "RRR");
            }
            return projectItems.Item(name);
        }
        async Task<Window2> OpenWindow(ProjectItem projectItem) {
            Window2 window = (Window2)projectItem.Open(EnvDTE.Constants.vsViewKindDesigner);
            if (window == null)
                throw new Exception("window == null");
            window.Activate();
            await WaitForDesingerHost(window);
            if (window.Object is IDesignerHost) {
                while (((IDesignerHost)window.Object).Loading) {
                    await Task.Delay(1000);
                }
                await Task.Delay(2000);
                return window;
            }
            return null;
        }
        async static Task WaitForDesingerHost(Window2 window) {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (!(window.Object is IDesignerHost)) {
                if (stopwatch.ElapsedMilliseconds > 100000) break;
                await Task.Delay(1000);
            }
        }
        async static Task WaitSubType(ProjectItem projectItem) {
            Property property = GetProperty(projectItem, "SubType");
            await WaitWhile(() => !property.Value.Equals("XtraReport"), 5000);
        }
        static Property GetProperty(ProjectItem projectItem, string propertyName) {
            foreach (Property property in projectItem.Properties) {
                if (property.Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase)) {
                    return property;
                }
            }
            return null;
        }
        async static Task WaitWhile(Func<bool> condition, int timeout) {
            const int delay = 10;
            int count = timeout / delay;
            while (condition() && count-- > 0) {
                await Task.Delay(delay);
            }
        }
    }
}
