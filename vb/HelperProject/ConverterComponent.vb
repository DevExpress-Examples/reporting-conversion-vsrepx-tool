Imports DevExpress.XtraReports.Import
Imports DevExpress.XtraReports.UI
Imports EnvDTE
Imports EnvDTE80
Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.ComponentModel.Design
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Namespace HelperProject
    <Designer(GetType(Class2Desinger))>
    <DesignerCategory("Code")>
    Public Class ConverterComponent
        Inherits Component

        Public Sub New()
        End Sub

        Public Sub New(ByVal container As IContainer)
            Me.New()
            container.Add(Me)
        End Sub
    End Class
End Namespace

Namespace HelperProject
    Class Class2Desinger
        Inherits ComponentDesigner

        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            MyBase.Dispose(disposing)
        End Sub

        Public Sub New()
            Verbs.Add(New DesignerVerb("Convert", AddressOf TestHandler))
        End Sub

        Private Async Sub TestHandler(ByVal sender As Object, ByVal e As EventArgs)
            Try
                Debug.WriteLine("start", "RRR")
                Dim dte = CType(GetService(GetType(DTE)), DTE2)
                Dim projectName As String = HelperProject.Program.ProjectName
                Dim project As Project = GetProject(dte, projectName)
                Dim projectPath As String = project.FileName
                Program.Convert(projectPath)
                Await Task.Delay(2000)
                Dim projectDirectory As String = Path.GetDirectoryName(projectPath)
                Dim layoutsDirectory As String = Path.Combine(Path.GetDirectoryName(projectDirectory), "Layouts")

                For Each directory In Program.EnumerateRepxDirectorys(layoutsDirectory)
                    Dim file = Path.Combine(directory, "report.repx")
                    Dim relativePath = directory.Substring(layoutsDirectory.Length + 1)
                    Dim csName = GetCsName(relativePath)
                    Dim projectItem = GetProjectItem(project, csName)
                    Debug.WriteLine(projectItem.Name, "RRR")
                    Await WaitSubType(projectItem)
                    Dim window = Await OpenWindow(projectItem)
                    Dim host = TryCast(window.Object, IDesignerHost)
                    Dim targetReport = CType(host.RootComponent, XtraReport)
                    Call (New ReportConverterRunner(CType(host.RootComponent, XtraReport), host)).Run(Function()
                                                                                                          Dim converter = New RepxConverter With {.UseExpressionBindings = True}
                                                                                                          Dim res As ConversionResult = converter.Convert(file)
                                                                                                          res.TargetReport.Name = targetReport.Name
                                                                                                          Return res
                                                                                                      End Function)
                    window.Close(vsSaveChanges.vsSaveChangesYes)
                Next

                Debug.WriteLine("finish", "RRR")
            Catch ex As Exception

                For Each el As String In ex.ToString().Split(New String() {Environment.NewLine}, StringSplitOptions.None)
                    Debug.WriteLine(el, "RRR")
                Next

                System.Windows.Forms.MessageBox.Show(ex.Message)
            End Try
        End Sub

        Private Shared Function GetCsName(ByVal relativePath As String) As String
            relativePath = Regex.Replace(relativePath, "\.vsrepx", ".vb", RegexOptions.IgnoreCase)
            relativePath = Regex.Replace(relativePath, "\.repx", ".vb", RegexOptions.IgnoreCase)
            Return relativePath
        End Function

        Private Shared Function GetProject(ByVal dte As DTE2, ByVal projectName As String) As Project
            Dim project As Project = Nothing

            For i As Integer = 1 To dte.Solution.Projects.Count
                Dim prj = dte.Solution.Projects.Item(i)

                If prj.Name = projectName Then
                    project = prj
                    Exit For
                End If
            Next

            If project Is Nothing Then Throw New ArgumentException(String.Format("Cannot find project {0}", projectName))
            Return project
        End Function

        Private Function GetProjectItem(ByVal project As Project, ByVal name As String) As ProjectItem
            Dim projectItems As ProjectItems = project.ProjectItems
            Dim path As String() = name.Split("\"c)

            If path.Length > 1 Then

                For i As Integer = 0 To path.Length - 1 - 1
                    projectItems = projectItems.Item(path(i)).ProjectItems
                Next

                name = path(path.Length - 1)
            End If

            Return projectItems.Item(name)
        End Function

        Private Async Function OpenWindow(ByVal projectItem As ProjectItem) As Task(Of Window2)
            Dim window As Window2 = CType(projectItem.Open(EnvDTE.Constants.vsViewKindDesigner), Window2)
            If window Is Nothing Then Throw New Exception("window == null")
            window.Activate()
            Await WaitForDesingerHost(window)

            If TypeOf window.Object Is IDesignerHost Then

                While (CType(window.Object, IDesignerHost)).Loading
                    Await Task.Delay(1000)
                End While

                Await Task.Delay(2000)
                Return window
            End If

            Return Nothing
        End Function

        Private Shared Async Function WaitForDesingerHost(ByVal window As Window2) As Task
            Dim stopwatch = System.Diagnostics.Stopwatch.StartNew()

            While Not (TypeOf window.Object Is IDesignerHost)
                If stopwatch.ElapsedMilliseconds > 100000 Then Exit While
                Await Task.Delay(1000)
            End While
        End Function

        Private Shared Async Function WaitSubType(ByVal projectItem As ProjectItem) As Task
            Dim [property] As [Property] = GetProperty(projectItem, "SubType")
            Await WaitWhile(Function() Not [property].Value.Equals("XtraReport"), 5000)
        End Function

        Private Shared Function GetProperty(ByVal projectItem As ProjectItem, ByVal propertyName As String) As [Property]
            For Each [property] As [Property] In projectItem.Properties

                If [property].Name.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase) Then
                    Return [property]
                End If
            Next

            Return Nothing
        End Function

        Private Shared Async Function WaitWhile(ByVal condition As Func(Of Boolean), ByVal timeout As Integer) As Task
            Const delay As Integer = 10
            Dim count As Integer = CInt(timeout / delay)

            While condition() AndAlso Math.Max(System.Threading.Interlocked.Decrement(count), count + 1) > 0
                Await Task.Delay(delay)
            End While
        End Function
    End Class
End Namespace

