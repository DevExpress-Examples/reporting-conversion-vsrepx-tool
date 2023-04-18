Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Namespace HelperProject
    Class Program
        Public Shared ReadOnly ProjectName As String = "ClassLibrary1"

        Shared Sub Main(ByVal args() As String)
            'Dim projectPath As String = GetCanonicalPath(Environment.CurrentDirectory & "\..\..\..\ClassLibrary1\" & ProjectName & ".vbproj")
            'Convert(projectPath)
        End Sub

        Private Shared Function GetCanonicalPath(ByVal v As String) As String
            Return New Uri(v).LocalPath
        End Function

        Public Shared Sub Convert(ByVal projectPath As String)
            Dim projectDirectory As String = Path.GetDirectoryName(projectPath)
            Dim layoutsDirectory As String = Path.Combine(Path.GetDirectoryName(projectDirectory), "Layouts")
            Dim reports = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each file In EnumerateRepxFiles(projectDirectory)
                Dim relativePath = file.Substring(projectDirectory.Length + 1)
                reports.Add(relativePath)
                Console.WriteLine(file)
                ProcessFile(file)
                MoveFile(projectDirectory, layoutsDirectory, relativePath)
            Next

            ProcessProject(projectPath, reports)
        End Sub

        Public Shared Function EnumerateRepxFiles(ByVal directory As String) As IEnumerable(Of String)
            Return System.IO.Directory.EnumerateFiles(directory, "*.vsrepx", SearchOption.AllDirectories).Concat(System.IO.Directory.EnumerateFiles(directory, "*.repx", SearchOption.AllDirectories))
        End Function

        Public Shared Function EnumerateRepxDirectorys(ByVal directory As String) As IEnumerable(Of String)
            Return System.IO.Directory.EnumerateDirectories(directory, "*.vsrepx", SearchOption.AllDirectories).Concat(System.IO.Directory.EnumerateDirectories(directory, "*.repx", SearchOption.AllDirectories))
        End Function

        Private Shared Sub MoveFile(ByVal projectDirectory As String, ByVal layoutsDirectory As String, ByVal relativePath As String)
            Dim target = Path.Combine(layoutsDirectory, relativePath)
            Dim source = Path.Combine(projectDirectory, relativePath)
            Dim targetFile = Path.Combine(target, "report.repx")
            If Not Directory.Exists(target) Then Directory.CreateDirectory(target)
            If File.Exists(targetFile) Then File.Delete(targetFile)
            File.Copy(source, targetFile)
            File.Delete(source)
        End Sub

        Private Shared Sub ProcessFile(ByVal file As String)
            Dim folder = Path.GetDirectoryName(file)
            Dim name = Path.GetFileNameWithoutExtension(file)
            Dim baseClass = ProcessDesignFile(Path.Combine(folder, name & ".Designer.vb"))
            ProcessCodeFile(Path.Combine(folder, name & ".vb"), baseClass)
            ProcessRepxFile(file)
        End Sub

        Private Shared Sub ProcessRepxFile(ByVal file As String)
            Dim text As String = System.IO.File.ReadAllText(file)
            text = Regex.Replace(text, "<Item1 Ref=""(\d+)"" Key=""VSReportExtInfo"" Value="".vsrepx"" />", "<Item1 Ref=""$1"" Key=""Skip"" Value=""Skip"" />")
            System.IO.File.WriteAllText(file, text)
        End Sub

        Private Shared Function ProcessDesignFile(ByVal file As String) As String
            Dim lines = System.IO.File.ReadAllLines(file)
            Dim newLines = New List(Of String)()
            Dim baseClass As String = ""

            For Each line In lines
                Dim m As Match
                m = Regex.Match(line, "Inherits\s+(\S+)")
                If m.Success Then baseClass = m.Groups(1).Value

                If line.IndexOf("Private Sub InitializeComponent()", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    newLines.Add(line)
                    newLines.Add("End Sub")
                    newLines.Add("End Class")
                    newLines.Add("End Namespace")
                    Exit For
                End If

                newLines.Add(line)
            Next

            System.IO.File.WriteAllLines(file, newLines)
            Return baseClass
        End Function

        Private Shared Sub ProcessCodeFile(ByVal file As String, ByVal baseClass As String)
            Dim lines = System.IO.File.ReadAllLines(file)
            Dim newLines = New List(Of String)()

            For Each line In lines
                Dim newLine As String = line
                Dim m = Regex.Match(line, "partial class \S+")

                If m.Success Then
                    newLine = line.Replace(m.Value, m.Value & " : " & baseClass)
                End If

                newLines.Add(newLine)
            Next

            System.IO.File.WriteAllLines(file, newLines)
        End Sub

        Private Shared Sub ProcessProject(ByVal file As String, ByVal reports As HashSet(Of String))
            Dim lines = System.IO.File.ReadAllLines(file)
            Dim newLines = New List(Of String)()
            Dim inCompileUpdate As Boolean = False
            Dim baseFile As String = ""

            For Each line In lines
                Dim newLine As String = line
                Dim m As Match

                If Not inCompileUpdate Then
                    m = Regex.Match(line, "<None Remove=""([^""]+)""\s*/>")
                    If m.Success AndAlso reports.Contains(m.Groups(1).Value) Then Continue For
                    m = Regex.Match(line, "<EmbeddedResource Include=""([^""]+)""\s*/>")
                    If m.Success AndAlso reports.Contains(m.Groups(1).Value) Then Continue For
                    m = Regex.Match(line, "<Compile Update=""([^""]+)""\s*>")

                    If m.Success AndAlso (reports.Contains(Regex.Replace(m.Groups(1).Value, "\.vb", ".vsrepx", RegexOptions.IgnoreCase)) OrElse reports.Contains(Regex.Replace(m.Groups(1).Value, "\.vb", ".repx", RegexOptions.IgnoreCase)) OrElse reports.Contains(Regex.Replace(m.Groups(1).Value, "\.designer\.vb", ".vsrepx", RegexOptions.IgnoreCase)) OrElse reports.Contains(Regex.Replace(m.Groups(1).Value, "\.designer\.vb", ".repx", RegexOptions.IgnoreCase))) Then
                        inCompileUpdate = True
                        baseFile = m.Groups(1).Value
                    End If
                Else

                    If line.Contains("DependentUpon") Then

                        If baseFile.IndexOf(".Designer.", StringComparison.OrdinalIgnoreCase) >= 0 Then
                            newLine = Regex.Replace(newLine, "\.vsrepx", ".vb", RegexOptions.IgnoreCase)
                            newLine = Regex.Replace(newLine, "\.repx", ".vb", RegexOptions.IgnoreCase)
                        Else
                            Continue For
                        End If
                    ElseIf line.Contains("</Compile>") Then
                        inCompileUpdate = False
                        baseFile = ""
                    End If
                End If

                newLines.Add(newLine)
            Next

            System.IO.File.WriteAllLines(file, newLines)
        End Sub
    End Class
End Namespace
