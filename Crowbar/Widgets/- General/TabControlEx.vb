Imports System.Runtime.InteropServices

' Purpose: TabControl that draws its tab headers itself in dark mode, because Windows always draws them light.
Public Class TabControlEx
	Inherits TabControl

	Public Sub New()
		MyBase.New()

		Me.theHotTabIndex = -1
	End Sub

	Friend Sub SetDarkMode(ByVal isDark As Boolean)
		If Me.theIsDark = isDark Then
			Exit Sub
		End If
		Me.theIsDark = isDark

		If isDark Then
			Me.theOriginalUserPaint = Me.GetStyle(ControlStyles.UserPaint)
			Me.theOriginalAllPaintingInWmPaint = Me.GetStyle(ControlStyles.AllPaintingInWmPaint)
			Me.theOriginalOptimizedDoubleBuffer = Me.GetStyle(ControlStyles.OptimizedDoubleBuffer)
			Me.SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
			Me.UpdateNativeFont()
		Else
			Me.SetStyle(ControlStyles.UserPaint, Me.theOriginalUserPaint)
			Me.SetStyle(ControlStyles.AllPaintingInWmPaint, Me.theOriginalAllPaintingInWmPaint)
			Me.SetStyle(ControlStyles.OptimizedDoubleBuffer, Me.theOriginalOptimizedDoubleBuffer)
		End If
		Me.UpdateScrollArrowsTheme()
		Me.Invalidate()
	End Sub

	Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
		If Not Me.theIsDark Then
			MyBase.OnPaint(e)
			Exit Sub
		End If

		Dim g As Graphics = e.Graphics
		Dim stripBackColor As Color = Me.BackColor
		If Me.Parent IsNot Nothing Then
			stripBackColor = Me.Parent.BackColor
		End If
		Using backBrush As New SolidBrush(stripBackColor)
			g.FillRectangle(backBrush, Me.ClientRectangle)
		End Using

		Dim pageRect As Rectangle = Me.DisplayRectangle
		pageRect.Inflate(2, 2)
		Using borderPen As New Pen(ThemeManager.Border)
			g.DrawRectangle(borderPen, pageRect.X, pageRect.Y, pageRect.Width - 1, pageRect.Height - 1)

			For i As Integer = 0 To Me.TabCount - 1
				Dim tabRect As Rectangle = Me.GetTabRect(i)
				Dim isSelected As Boolean = (i = Me.SelectedIndex)
				Dim tabBackColor As Color
				If isSelected Then
					tabRect.Inflate(2, 2)
					tabRect.Height -= 1
					tabBackColor = Me.TabPages(i).BackColor
				ElseIf i = Me.theHotTabIndex Then
					tabBackColor = ThemeManager.HotHeaderBack
				Else
					tabBackColor = ThemeManager.HeaderBack
				End If

				Using tabBrush As New SolidBrush(tabBackColor)
					' Selected tab extends down over the page border so they look joined.
					g.FillRectangle(tabBrush, tabRect.X, tabRect.Y, tabRect.Width, tabRect.Height + If(isSelected, 2, 0))
				End Using
				g.DrawLine(borderPen, tabRect.Left, tabRect.Bottom, tabRect.Left, tabRect.Top)
				g.DrawLine(borderPen, tabRect.Left, tabRect.Top, tabRect.Right - 1, tabRect.Top)
				g.DrawLine(borderPen, tabRect.Right - 1, tabRect.Top, tabRect.Right - 1, tabRect.Bottom)

				Dim textColor As Color = If(Me.Enabled, Me.ForeColor, ThemeManager.GrayText)
				TextRenderer.DrawText(g, Me.TabPages(i).Text, Me.Font, tabRect, textColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix)
			Next
		End Using

		Me.UpdateScrollArrowsTheme()
	End Sub

	Protected Overrides Sub OnHandleCreated(ByVal e As EventArgs)
		MyBase.OnHandleCreated(e)
		If Me.theIsDark Then
			Me.UpdateNativeFont()
		End If
	End Sub

	Protected Overrides Sub OnFontChanged(ByVal e As EventArgs)
		MyBase.OnFontChanged(e)
		If Me.theIsDark Then
			Me.UpdateNativeFont()
		End If
	End Sub

	Protected Overrides Sub Dispose(ByVal disposing As Boolean)
		MyBase.Dispose(disposing)
		If Me.theNativeFontHandle <> IntPtr.Zero Then
			DeleteObject(Me.theNativeFontHandle)
			Me.theNativeFontHandle = IntPtr.Zero
		End If
	End Sub

	Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
		MyBase.OnMouseMove(e)
		' Hot tracking is only drawn in dark mode.
		If Not Me.theIsDark Then
			Exit Sub
		End If

		Dim hotTabIndex As Integer = -1
		For i As Integer = 0 To Me.TabCount - 1
			If Me.GetTabRect(i).Contains(e.Location) Then
				hotTabIndex = i
				Exit For
			End If
		Next
		Me.SetHotTabIndex(hotTabIndex)
	End Sub

	Protected Overrides Sub OnMouseLeave(ByVal e As EventArgs)
		MyBase.OnMouseLeave(e)
		Me.SetHotTabIndex(-1)
	End Sub

	Private Sub SetHotTabIndex(ByVal hotTabIndex As Integer)
		If Me.theHotTabIndex <> hotTabIndex Then
			Me.theHotTabIndex = hotTabIndex
			If Me.theIsDark Then
				Me.Invalidate()
			End If
		End If
	End Sub

	' While UserPaint is on, WinForms no longer passes the font to the native tab control,
	'     which would then size the tabs for its default font. Pass it here instead.
	Private Sub UpdateNativeFont()
		If Not Me.IsHandleCreated Then
			Exit Sub
		End If

		Dim fontHandle As IntPtr = Me.Font.ToHfont()
		SendMessage(Me.Handle, WM_SETFONT, fontHandle, New IntPtr(1))
		If Me.theNativeFontHandle <> IntPtr.Zero Then
			DeleteObject(Me.theNativeFontHandle)
		End If
		Me.theNativeFontHandle = fontHandle
	End Sub

	' The scroll arrows shown when the tabs do not fit are a separate native window.
	Private Sub UpdateScrollArrowsTheme()
		If Not Me.IsHandleCreated Then
			Exit Sub
		End If

		Dim upDownHandle As IntPtr = FindWindowEx(Me.Handle, IntPtr.Zero, "msctls_updown32", Nothing)
		If upDownHandle <> Me.theThemedUpDownHandle Then
			Me.theThemedUpDownHandle = IntPtr.Zero
		End If
		If upDownHandle <> IntPtr.Zero AndAlso (Me.theIsDark <> (Me.theThemedUpDownHandle <> IntPtr.Zero)) Then
			Try
				If Me.theIsDark Then
					SetWindowTheme(upDownHandle, "DarkMode_Explorer", Nothing)
					Me.theThemedUpDownHandle = upDownHandle
				Else
					SetWindowTheme(upDownHandle, Nothing, Nothing)
					Me.theThemedUpDownHandle = IntPtr.Zero
				End If
			Catch ex As Exception
			End Try
		End If
	End Sub

	Private Const WM_SETFONT As Integer = &H30

	<DllImport("user32.dll")>
	Private Shared Function SendMessage(ByVal hWnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function DeleteObject(ByVal hObject As IntPtr) As Boolean
	End Function

	<DllImport("user32.dll", CharSet:=CharSet.Unicode)>
	Private Shared Function FindWindowEx(ByVal hwndParent As IntPtr, ByVal hwndChildAfter As IntPtr, ByVal lpszClass As String, ByVal lpszWindow As String) As IntPtr
	End Function

	<DllImport("uxtheme.dll", CharSet:=CharSet.Unicode)>
	Private Shared Function SetWindowTheme(ByVal hWnd As IntPtr, ByVal pszSubAppName As String, ByVal pszSubIdList As String) As Integer
	End Function

	Private theIsDark As Boolean
	Private theHotTabIndex As Integer
	Private theThemedUpDownHandle As IntPtr
	Private theNativeFontHandle As IntPtr
	Private theOriginalUserPaint As Boolean
	Private theOriginalAllPaintingInWmPaint As Boolean
	Private theOriginalOptimizedDoubleBuffer As Boolean

End Class
