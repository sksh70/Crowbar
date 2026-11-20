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
			ThemeManager.SetNativeFont(Me, Me.theNativeFontHandle)
		Else
			Me.SetStyle(ControlStyles.UserPaint, Me.theOriginalUserPaint)
			Me.SetStyle(ControlStyles.AllPaintingInWmPaint, Me.theOriginalAllPaintingInWmPaint)
			Me.SetStyle(ControlStyles.OptimizedDoubleBuffer, Me.theOriginalOptimizedDoubleBuffer)
		End If
		ThemeManager.ApplyTabScrollArrowsTheme(Me, Me.theThemedUpDownHandle)
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

		ThemeManager.ApplyTabScrollArrowsTheme(Me, Me.theThemedUpDownHandle)
	End Sub

	' While UserPaint is on, WinForms no longer passes the font to the native tab control,
	'     which would then size the tabs for its default font, so pass it here instead.
	Protected Overrides Sub OnHandleCreated(ByVal e As EventArgs)
		MyBase.OnHandleCreated(e)
		If Me.theIsDark Then
			ThemeManager.SetNativeFont(Me, Me.theNativeFontHandle)
		End If
	End Sub

	Protected Overrides Sub OnFontChanged(ByVal e As EventArgs)
		MyBase.OnFontChanged(e)
		If Me.theIsDark Then
			ThemeManager.SetNativeFont(Me, Me.theNativeFontHandle)
		End If
	End Sub

	Protected Overrides Sub Dispose(ByVal disposing As Boolean)
		MyBase.Dispose(disposing)
		ThemeManager.DeleteNativeFont(Me.theNativeFontHandle)
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

	Private theIsDark As Boolean
	Private theHotTabIndex As Integer
	Private theThemedUpDownHandle As IntPtr
	Private theNativeFontHandle As IntPtr
	Private theOriginalUserPaint As Boolean
	Private theOriginalAllPaintingInWmPaint As Boolean
	Private theOriginalOptimizedDoubleBuffer As Boolean

End Class
