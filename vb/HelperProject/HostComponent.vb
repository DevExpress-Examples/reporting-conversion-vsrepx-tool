Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Linq
Imports System.Text
Imports System.Threading.Tasks

Namespace HelperProject
	Public Class HostComponent
		Inherits Component

		Private converterComponent1 As ConverterComponent

		Private Sub InitializeComponent()
			Me.converterComponent1 = New HelperProject.ConverterComponent()

		End Sub
	End Class
End Namespace
