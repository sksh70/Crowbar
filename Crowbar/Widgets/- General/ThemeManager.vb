Imports System.ComponentModel
Imports System.Drawing.Drawing2D
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports Microsoft.Win32

' Purpose: Applies the Light or Dark theme to forms and their widgets.
' Designer and code always set light colors. Dark mode maps those light colors onto a dark palette.
' The original values are remembered when a widget is darkened and restored when switching back to light,
'     so light mode looks exactly like it does without theming.
Public NotInheritable Class ThemeManager

#Region "Create and Destroy"

	Private Sub New()
	End Sub

#End Region

#Region "Properties"

	Public Shared ReadOnly Property IsDark() As Boolean
		Get
			Return theIsDark
		End Get
	End Property

	Public Shared ReadOnly Property GrayText() As Color
		Get
			Return ThemeManager.Map(SystemColors.GrayText)
		End Get
	End Property

	Public Shared ReadOnly Property Border() As Color
		Get
			If theIsDark Then
				Return theDarkBorder
			Else
				Return SystemColors.ControlDark
			End If
		End Get
	End Property

	Public Shared ReadOnly Property HeaderBack() As Color
		Get
			If theIsDark Then
				Return theDarkHeaderBack
			Else
				Return SystemColors.Control
			End If
		End Get
	End Property

	Public Shared ReadOnly Property HotHeaderBack() As Color
		Get
			If theIsDark Then
				Return theDarkHotHeaderBack
			Else
				Return SystemColors.ControlLight
			End If
		End Get
	End Property

#End Region

#Region "Methods"

	' Returns the color to use for a color that the designer or code would use in light mode.
	Public Shared Function Map(ByVal lightColor As Color) As Color
		If Not theIsDark Then
			Return lightColor
		End If

		If lightColor = SystemColors.Control OrElse lightColor = SystemColors.ButtonFace OrElse lightColor = SystemColors.Menu Then
			Return theDarkBack
		ElseIf lightColor = SystemColors.Window OrElse lightColor = SystemColors.Info Then
			Return theDarkInputBack
		ElseIf lightColor = SystemColors.ControlText OrElse lightColor = SystemColors.WindowText OrElse lightColor = SystemColors.MenuText OrElse lightColor = SystemColors.InfoText Then
			Return theDarkFore
		ElseIf lightColor = SystemColors.GrayText Then
			Return theDarkGrayText
		ElseIf lightColor = SystemColors.Highlight Then
			Return theDarkHighlight
		ElseIf lightColor = SystemColors.HighlightText Then
			Return Color.White
		ElseIf lightColor = SystemColors.ControlDark OrElse lightColor = SystemColors.ControlDarkDark Then
			Return theDarkSelectionBack
		ElseIf lightColor = SystemColors.ControlLight OrElse lightColor = SystemColors.ControlLightLight Then
			Return theDarkHotHeaderBack
		ElseIf lightColor = SystemColors.AppWorkspace Then
			Return theDarkBack
		ElseIf lightColor = Color.Green Then
			Return theDarkGreenLink
		End If
		Return lightColor
	End Function

	' Applies the current theme to a form or widget and all of its child widgets.
	Public Shared Sub Apply(ByVal root As Control)
		ThemeManager.EnsureInitialized()
		ThemeManager.ApplyToControl(root)
	End Sub

	' Call from a form's OnLoad, before MyBase.OnLoad.
	Public Shared Sub ApplyToForm(ByVal aForm As Form)
		If aForm.Site IsNot Nothing AndAlso aForm.Site.DesignMode Then
			Exit Sub
		End If
		ThemeManager.Apply(aForm)
	End Sub

	' Sets a color that code chooses at run time, given as the color it would use in light mode.
	' In dark mode the mapped color is set, and the light color is what returns when switching back to light.
	Public Shared Sub SetColor(ByVal owner As Control, ByVal lightColor As Color, ByVal setColor As Action(Of Color))
		Dim state As ThemedState = Nothing
		If theIsDark AndAlso theThemedStates.TryGetValue(owner, state) Then
			setColor(ThemeManager.Map(lightColor))
			' Inserted first so it runs last, after restoring the colors remembered when the widget was darkened.
			state.RestoreActions.Insert(0, Sub() setColor(lightColor))
		Else
			' A widget that is not darkened yet gets the light color and is mapped when it is darkened.
			setColor(lightColor)
		End If
	End Sub

	' The scroll arrows shown when a TabControl's tabs do not fit are a separate native window, created when needed.
	' themedUpDownHandle remembers the arrows window that was themed dark, so it is themed only once.
	Friend Shared Sub ApplyTabScrollArrowsTheme(ByVal aTabControl As TabControl, ByRef themedUpDownHandle As IntPtr)
		If Not aTabControl.IsHandleCreated Then
			Exit Sub
		End If

		Try
			Dim upDownHandle As IntPtr = FindWindowEx(aTabControl.Handle, IntPtr.Zero, "msctls_updown32", Nothing)
			If upDownHandle <> themedUpDownHandle Then
				themedUpDownHandle = IntPtr.Zero
			End If
			If upDownHandle <> IntPtr.Zero AndAlso (theIsDark <> (themedUpDownHandle <> IntPtr.Zero)) Then
				ThemeManager.SetNativeTheme(upDownHandle, "DarkMode_Explorer")
				themedUpDownHandle = If(theIsDark, upDownHandle, IntPtr.Zero)
			End If
		Catch ex As Exception
		End Try
	End Sub

	' Gives a widget's native window the widget's font, for widgets that paint themselves (WinForms then skips this).
	' fontHandle is the font handle passed last time; it is replaced and the old one deleted.
	Friend Shared Sub SetNativeFont(ByVal aControl As Control, ByRef fontHandle As IntPtr)
		If Not aControl.IsHandleCreated Then
			Exit Sub
		End If

		Dim newFontHandle As IntPtr = aControl.Font.ToHfont()
		SendMessage(aControl.Handle, WM_SETFONT, newFontHandle, New IntPtr(1))
		ThemeManager.DeleteNativeFont(fontHandle)
		fontHandle = newFontHandle
	End Sub

	Friend Shared Sub DeleteNativeFont(ByRef fontHandle As IntPtr)
		If fontHandle <> IntPtr.Zero Then
			DeleteObject(fontHandle)
			fontHandle = IntPtr.Zero
		End If
	End Sub

	' Re-reads the theme setting and, if it changed, applies it to all open forms.
	Public Shared Sub ApplyAll()
		ThemeManager.EnsureInitialized()

		Dim isDark As Boolean = ThemeManager.ResolveIsDark()
		If isDark = theIsDark Then
			Exit Sub
		End If
		theIsDark = isDark

		ThemeManager.UpdateToolStripRenderer()
		Dim openForms As New List(Of Form)()
		For Each aForm As Form In Application.OpenForms
			openForms.Add(aForm)
		Next
		For Each aForm As Form In openForms
			ThemeManager.ApplyToControl(aForm)
			aForm.Invalidate(True)
		Next
	End Sub

	Public Shared Sub DrawCheckGlyph(ByVal g As Graphics, ByVal bounds As Rectangle, ByVal state As CheckState, ByVal enabled As Boolean)
		Dim boxRect As New Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1)
		Dim isAccented As Boolean = enabled AndAlso state <> CheckState.Unchecked

		Using backBrush As New SolidBrush(If(isAccented, theDarkAccent, If(enabled, theDarkInputBack, theDarkBack)))
			g.FillRectangle(backBrush, boxRect)
		End Using
		Using borderPen As New Pen(If(isAccented, theDarkAccent, If(enabled, theDarkGlyphBorder, theDarkBorder)))
			g.DrawRectangle(borderPen, boxRect)
		End Using

		Dim markColor As Color = If(enabled, Color.White, theDarkGrayText)
		If state = CheckState.Checked Then
			Dim savedState As GraphicsState = g.Save()
			g.SmoothingMode = SmoothingMode.AntiAlias
			Using markPen As New Pen(markColor, Math.Max(1.5F, bounds.Width / 8.0F))
				Dim points() As PointF = {
					New PointF(bounds.X + bounds.Width * 0.22F, bounds.Y + bounds.Height * 0.5F),
					New PointF(bounds.X + bounds.Width * 0.42F, bounds.Y + bounds.Height * 0.7F),
					New PointF(bounds.X + bounds.Width * 0.78F, bounds.Y + bounds.Height * 0.3F)}
				g.DrawLines(markPen, points)
			End Using
			g.Restore(savedState)
		ElseIf state = CheckState.Indeterminate Then
			Dim markRect As Rectangle = boxRect
			markRect.Inflate(-3, -3)
			Using markBrush As New SolidBrush(markColor)
				g.FillRectangle(markBrush, markRect)
			End Using
		End If
	End Sub

	Public Shared Sub DrawRadioGlyph(ByVal g As Graphics, ByVal bounds As Rectangle, ByVal isChecked As Boolean, ByVal enabled As Boolean)
		Dim circleRect As New Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1)
		Dim isAccented As Boolean = enabled AndAlso isChecked

		Dim savedState As GraphicsState = g.Save()
		g.SmoothingMode = SmoothingMode.AntiAlias
		Using backBrush As New SolidBrush(If(enabled, theDarkInputBack, theDarkBack))
			g.FillEllipse(backBrush, circleRect)
		End Using
		Using borderPen As New Pen(If(isAccented, theDarkAccent, If(enabled, theDarkGlyphBorder, theDarkBorder)))
			g.DrawEllipse(borderPen, circleRect)
		End Using
		If isChecked Then
			Dim dotRect As Rectangle = circleRect
			dotRect.Inflate(-CInt(Math.Ceiling(bounds.Width / 4.0)), -CInt(Math.Ceiling(bounds.Height / 4.0)))
			Using dotBrush As New SolidBrush(If(enabled, theDarkAccent, theDarkGrayText))
				g.FillEllipse(dotBrush, dotRect)
			End Using
		End If
		g.Restore(savedState)
	End Sub

	Public Shared Sub DrawProgressBar(ByVal g As Graphics, ByVal bounds As Rectangle, ByVal percent As Double)
		Using backBrush As New SolidBrush(theDarkInputBack)
			g.FillRectangle(backBrush, bounds)
		End Using
		If percent > 0 Then
			Dim barRect As Rectangle = bounds
			barRect.Inflate(-2, -2)
			barRect.Width = Math.Max(1, CInt(barRect.Width * Math.Min(percent, 1.0)))
			Using barBrush As New SolidBrush(theDarkAccent)
				g.FillRectangle(barBrush, barRect)
			End Using
		End If
		Using borderPen As New Pen(theDarkBorder)
			g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1)
		End Using
	End Sub

	Private Shared Sub EnsureInitialized()
		If theIsInitialized Then
			Exit Sub
		End If
		theIsInitialized = True

		theIsDark = ThemeManager.ResolveIsDark()
		If TheApp IsNot Nothing AndAlso TheApp.Settings IsNot Nothing Then
			AddHandler TheApp.Settings.PropertyChanged, AddressOf ThemeManager.Settings_PropertyChanged
		End If
		AddHandler SystemEvents.UserPreferenceChanged, AddressOf ThemeManager.SystemEvents_UserPreferenceChanged
		ThemeManager.UpdateToolStripRenderer()
	End Sub

	Private Shared Function ResolveIsDark() As Boolean
		If TheApp Is Nothing OrElse TheApp.Settings Is Nothing Then
			Return False
		End If

		Select Case TheApp.Settings.AppTheme
			Case AppThemeOptions.Dark
				Return True
			Case AppThemeOptions.Light
				Return False
			Case Else
				Return ThemeManager.WindowsAppsUseDarkTheme()
		End Select
	End Function

	Private Shared Function WindowsAppsUseDarkTheme() As Boolean
		Try
			Using key As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
				If key IsNot Nothing Then
					Dim value As Object = key.GetValue("AppsUseLightTheme")
					If TypeOf value Is Integer Then
						Return CInt(value) = 0
					End If
				End If
			End Using
		Catch ex As Exception
		End Try
		Return False
	End Function

	Private Shared Sub UpdateToolStripRenderer()
		If theIsDark Then
			If theOriginalToolStripRenderer Is Nothing Then
				theOriginalToolStripRenderer = ToolStripManager.Renderer
			End If
			ToolStripManager.Renderer = New DarkToolStripRenderer()
		ElseIf theOriginalToolStripRenderer IsNot Nothing Then
			ToolStripManager.Renderer = theOriginalToolStripRenderer
		End If
	End Sub

	Private Shared Sub ApplyToControl(ByVal aControl As Control)
		ThemeManager.HookControl(aControl)

		Dim state As ThemedState = Nothing
		Dim isThemed As Boolean = theThemedStates.TryGetValue(aControl, state)
		If theIsDark AndAlso Not isThemed Then
			state = New ThemedState()
			theThemedStates.Add(aControl, state)
			ThemeManager.DarkenControl(aControl, state)
			ThemeManager.ApplyNativeTheme(aControl)
		ElseIf Not theIsDark AndAlso isThemed Then
			theThemedStates.Remove(aControl)
			For i As Integer = state.RestoreActions.Count - 1 To 0 Step -1
				state.RestoreActions(i)()
			Next
			ThemeManager.ApplyNativeTheme(aControl)
		End If

		For Each child As Control In aControl.Controls
			ThemeManager.ApplyToControl(child)
		Next
	End Sub

	Private Shared Sub HookControl(ByVal aControl As Control)
		Dim marker As Object = Nothing
		If theHookedControls.TryGetValue(aControl, marker) Then
			Exit Sub
		End If
		theHookedControls.Add(aControl, aControl)

		AddHandler aControl.ControlAdded, AddressOf ThemeManager.Control_ControlAdded
		AddHandler aControl.HandleCreated, AddressOf ThemeManager.Control_HandleCreated

		If TypeOf aControl Is TextBoxBase Then
			Dim aTextBox As TextBoxBase = CType(aControl, TextBoxBase)
			AddHandler aTextBox.ReadOnlyChanged, AddressOf ThemeManager.TextBox_ReadOnlyOrEnabledChanged
			AddHandler aTextBox.EnabledChanged, AddressOf ThemeManager.TextBox_ReadOnlyOrEnabledChanged
		ElseIf TypeOf aControl Is GroupBox Then
			AddHandler aControl.Paint, AddressOf ThemeManager.GroupBox_Paint
		ElseIf ThemeManager.IsGlyphButton(aControl) Then
			AddHandler aControl.Paint, AddressOf ThemeManager.GlyphButton_Paint
		ElseIf TypeOf aControl Is DataGridView Then
			AddHandler CType(aControl, DataGridView).ColumnAdded, AddressOf ThemeManager.DataGridView_ColumnAdded
		End If
	End Sub

	Private Shared Sub DarkenControl(ByVal aControl As Control, ByVal state As ThemedState)
		' The DataGridView sets its editing widget's colors from the (already mapped) cell style.
		If TypeOf aControl Is IDataGridViewEditingControl Then
			Exit Sub
		End If

		If TypeOf aControl Is Form Then
			ThemeManager.SetBackColor(state, aControl, theDarkBack)
			ThemeManager.SetForeColor(state, aControl, theDarkFore)
		ElseIf TypeOf aControl Is TextBoxBase OrElse TypeOf aControl Is ListBox OrElse TypeOf aControl Is ListView OrElse TypeOf aControl Is TreeView OrElse TypeOf aControl Is ComboBox Then
			state.HasExplicitBackColor = ThemeManager.IsExplicitlySet(aControl, "BackColor")
			If state.HasExplicitBackColor Then
				ThemeManager.SetBackColor(state, aControl, ThemeManager.Map(aControl.BackColor))
			Else
				ThemeManager.SetBackColor(state, aControl, ThemeManager.GetInputBackColor(aControl))
			End If
			ThemeManager.SetForeColor(state, aControl, ThemeManager.Map(aControl.ForeColor))

			If TypeOf aControl Is TreeView Then
				Dim aTreeView As TreeView = CType(aControl, TreeView)
				Dim originalLineColor As Color = aTreeView.LineColor
				state.RestoreActions.Add(Sub() aTreeView.LineColor = originalLineColor)
				aTreeView.LineColor = theDarkGlyphBorder
				state.RestoreActions.Add(Sub() ThemeManager.RemapTreeNodeForeColors(aTreeView.Nodes, theDarkGrayText, SystemColors.GrayText))
				ThemeManager.RemapTreeNodeForeColors(aTreeView.Nodes, SystemColors.GrayText, theDarkGrayText)
			End If
		ElseIf TypeOf aControl Is DataGridView Then
			ThemeManager.DarkenDataGridView(CType(aControl, DataGridView), state)
		ElseIf TypeOf aControl Is ButtonBase AndAlso Not ThemeManager.IsGlyphButton(aControl) Then
			ThemeManager.DarkenButton(CType(aControl, ButtonBase), state)
		ElseIf TypeOf aControl Is ProgressBar Then
			ThemeManager.SetBackColor(state, aControl, theDarkInputBack)
			ThemeManager.MapExplicitForeColor(aControl, state)
		ElseIf TypeOf aControl Is TabControlEx Then
			Dim aTabControl As TabControlEx = CType(aControl, TabControlEx)
			state.RestoreActions.Add(Sub() aTabControl.SetDarkMode(False))
			aTabControl.SetDarkMode(True)
		Else
			If TypeOf aControl Is TabPage Then
				Dim aTabPage As TabPage = CType(aControl, TabPage)
				Dim originalUseVisualStyleBackColor As Boolean = aTabPage.UseVisualStyleBackColor
				state.RestoreActions.Add(Sub() aTabPage.UseVisualStyleBackColor = originalUseVisualStyleBackColor)
				aTabPage.UseVisualStyleBackColor = False
			ElseIf TypeOf aControl Is LinkLabel Then
				Dim aLinkLabel As LinkLabel = CType(aControl, LinkLabel)
				Dim originalLinkColor As Color = aLinkLabel.LinkColor
				Dim originalVisitedLinkColor As Color = aLinkLabel.VisitedLinkColor
				Dim originalActiveLinkColor As Color = aLinkLabel.ActiveLinkColor
				state.RestoreActions.Add(Sub()
											 aLinkLabel.LinkColor = originalLinkColor
											 aLinkLabel.VisitedLinkColor = originalVisitedLinkColor
											 aLinkLabel.ActiveLinkColor = originalActiveLinkColor
										 End Sub)
				aLinkLabel.LinkColor = ThemeManager.MapLinkColor(originalLinkColor)
				aLinkLabel.VisitedLinkColor = ThemeManager.MapLinkColor(originalVisitedLinkColor)
				aLinkLabel.ActiveLinkColor = theDarkActiveLink
			End If
			ThemeManager.MapExplicitBackColor(aControl, state)
			ThemeManager.MapExplicitForeColor(aControl, state)
		End If
	End Sub

	Private Shared Sub DarkenButton(ByVal aButton As ButtonBase, ByVal state As ThemedState)
		Dim originalFlatStyle As FlatStyle = aButton.FlatStyle
		Dim originalUseVisualStyleBackColor As Boolean = aButton.UseVisualStyleBackColor
		Dim originalBorderColor As Color = aButton.FlatAppearance.BorderColor
		Dim originalMouseOverBackColor As Color = aButton.FlatAppearance.MouseOverBackColor
		Dim originalMouseDownBackColor As Color = aButton.FlatAppearance.MouseDownBackColor
		Dim originalCheckedBackColor As Color = aButton.FlatAppearance.CheckedBackColor
		' Added first so it runs last: restoring BackColor turns off UseVisualStyleBackColor.
		state.RestoreActions.Add(Sub()
									 aButton.FlatStyle = originalFlatStyle
									 aButton.FlatAppearance.BorderColor = originalBorderColor
									 aButton.FlatAppearance.MouseOverBackColor = originalMouseOverBackColor
									 aButton.FlatAppearance.MouseDownBackColor = originalMouseDownBackColor
									 aButton.FlatAppearance.CheckedBackColor = originalCheckedBackColor
									 aButton.UseVisualStyleBackColor = originalUseVisualStyleBackColor
								 End Sub)

		aButton.FlatStyle = FlatStyle.Flat
		aButton.FlatAppearance.BorderColor = theDarkButtonBorder
		aButton.FlatAppearance.MouseOverBackColor = theDarkButtonHotBack
		aButton.FlatAppearance.MouseDownBackColor = theDarkButtonPressedBack
		aButton.FlatAppearance.CheckedBackColor = theDarkHighlight
		ThemeManager.SetBackColor(state, aButton, theDarkButtonBack)
		ThemeManager.MapExplicitForeColor(aButton, state)
	End Sub

	Private Shared Sub DarkenDataGridView(ByVal aGrid As DataGridView, ByVal state As ThemedState)
		Dim originalBackgroundColor As Color = aGrid.BackgroundColor
		Dim originalGridColor As Color = aGrid.GridColor
		Dim originalEnableHeadersVisualStyles As Boolean = aGrid.EnableHeadersVisualStyles
		Dim originalDefaultCellStyle As New DataGridViewCellStyle(aGrid.DefaultCellStyle)
		' DefaultCellStyle fills an unset ForeColor with the grid's inherited ForeColor, which is already dark here.
		If originalDefaultCellStyle.ForeColor = aGrid.ForeColor AndAlso Not ThemeManager.IsExplicitlySet(aGrid, "ForeColor") Then
			originalDefaultCellStyle.ForeColor = Color.Empty
		End If
		Dim originalColumnHeadersDefaultCellStyle As New DataGridViewCellStyle(aGrid.ColumnHeadersDefaultCellStyle)
		Dim originalRowHeadersDefaultCellStyle As New DataGridViewCellStyle(aGrid.RowHeadersDefaultCellStyle)
		state.RestoreActions.Add(Sub()
									 aGrid.BackgroundColor = originalBackgroundColor
									 aGrid.GridColor = originalGridColor
									 aGrid.EnableHeadersVisualStyles = originalEnableHeadersVisualStyles
									 aGrid.DefaultCellStyle = originalDefaultCellStyle
									 aGrid.ColumnHeadersDefaultCellStyle = originalColumnHeadersDefaultCellStyle
									 aGrid.RowHeadersDefaultCellStyle = originalRowHeadersDefaultCellStyle
								 End Sub)

		aGrid.BackgroundColor = theDarkBack
		aGrid.GridColor = theDarkBorder
		aGrid.EnableHeadersVisualStyles = False

		Dim aStyle As New DataGridViewCellStyle(aGrid.DefaultCellStyle)
		ThemeManager.MapCellStyle(aStyle)
		aGrid.DefaultCellStyle = aStyle
		aGrid.ColumnHeadersDefaultCellStyle = ThemeManager.CreateDarkHeaderStyle(aGrid.ColumnHeadersDefaultCellStyle)
		aGrid.RowHeadersDefaultCellStyle = ThemeManager.CreateDarkHeaderStyle(aGrid.RowHeadersDefaultCellStyle)

		For Each aColumn As DataGridViewColumn In aGrid.Columns
			ThemeManager.DarkenDataGridViewColumn(aColumn, state)
		Next
	End Sub

	Private Shared Sub DarkenDataGridViewColumn(ByVal aColumn As DataGridViewColumn, ByVal state As ThemedState)
		If aColumn.HasDefaultCellStyle Then
			Dim originalStyle As New DataGridViewCellStyle(aColumn.DefaultCellStyle)
			state.RestoreActions.Add(Sub() aColumn.DefaultCellStyle = originalStyle)
			Dim aStyle As New DataGridViewCellStyle(aColumn.DefaultCellStyle)
			ThemeManager.MapCellStyle(aStyle)
			aColumn.DefaultCellStyle = aStyle
		End If

		If TypeOf aColumn Is DataGridViewButtonColumn Then
			Dim aButtonColumn As DataGridViewButtonColumn = CType(aColumn, DataGridViewButtonColumn)
			Dim originalFlatStyle As FlatStyle = aButtonColumn.FlatStyle
			state.RestoreActions.Add(Sub() aButtonColumn.FlatStyle = originalFlatStyle)
			aButtonColumn.FlatStyle = FlatStyle.Flat
		End If
	End Sub

	Private Shared Function CreateDarkHeaderStyle(ByVal lightStyle As DataGridViewCellStyle) As DataGridViewCellStyle
		Dim aStyle As New DataGridViewCellStyle(lightStyle)
		aStyle.BackColor = theDarkHeaderBack
		aStyle.ForeColor = theDarkFore
		aStyle.SelectionBackColor = theDarkHotHeaderBack
		aStyle.SelectionForeColor = theDarkFore
		Return aStyle
	End Function

	Private Shared Sub MapCellStyle(ByVal aStyle As DataGridViewCellStyle)
		aStyle.BackColor = ThemeManager.Map(aStyle.BackColor)
		aStyle.ForeColor = ThemeManager.Map(aStyle.ForeColor)
		aStyle.SelectionBackColor = ThemeManager.Map(aStyle.SelectionBackColor)
		aStyle.SelectionForeColor = ThemeManager.Map(aStyle.SelectionForeColor)
	End Sub

	Private Shared Function GetInputBackColor(ByVal aControl As Control) As Color
		' Same rule as TextBoxBase uses for its default color: read-only and disabled boxes use the control color.
		If TypeOf aControl Is TextBoxBase Then
			Dim aTextBox As TextBoxBase = CType(aControl, TextBoxBase)
			If aTextBox.ReadOnly OrElse Not aTextBox.Enabled Then
				Return theDarkBack
			End If
		End If
		Return theDarkInputBack
	End Function

	Private Shared Function MapLinkColor(ByVal lightColor As Color) As Color
		If lightColor = Color.Green Then
			Return theDarkGreenLink
		End If
		Return theDarkLink
	End Function

	Private Shared Sub RemapTreeNodeForeColors(ByVal nodes As TreeNodeCollection, ByVal fromColor As Color, ByVal toColor As Color)
		For Each aNode As TreeNode In nodes
			If aNode.ForeColor = fromColor Then
				aNode.ForeColor = toColor
			End If
			ThemeManager.RemapTreeNodeForeColors(aNode.Nodes, fromColor, toColor)
		Next
	End Sub

	Private Shared Function IsGlyphButton(ByVal aControl As Control) As Boolean
		If TypeOf aControl Is CheckBox Then
			Return CType(aControl, CheckBox).Appearance = Appearance.Normal
		ElseIf TypeOf aControl Is RadioButton Then
			Return CType(aControl, RadioButton).Appearance = Appearance.Normal
		End If
		Return False
	End Function

	Private Shared Function IsExplicitlySet(ByVal aControl As Control, ByVal propertyName As String) As Boolean
		Return TypeDescriptor.GetProperties(aControl)(propertyName).ShouldSerializeValue(aControl)
	End Function

	Private Shared Sub SetBackColor(ByVal state As ThemedState, ByVal aControl As Control, ByVal newColor As Color)
		If ThemeManager.IsExplicitlySet(aControl, "BackColor") Then
			Dim originalColor As Color = aControl.BackColor
			state.RestoreActions.Add(Sub() aControl.BackColor = originalColor)
		Else
			state.RestoreActions.Add(Sub() aControl.ResetBackColor())
		End If
		aControl.BackColor = newColor
	End Sub

	Private Shared Sub SetForeColor(ByVal state As ThemedState, ByVal aControl As Control, ByVal newColor As Color)
		If ThemeManager.IsExplicitlySet(aControl, "ForeColor") Then
			Dim originalColor As Color = aControl.ForeColor
			state.RestoreActions.Add(Sub() aControl.ForeColor = originalColor)
		Else
			state.RestoreActions.Add(Sub() aControl.ResetForeColor())
		End If
		aControl.ForeColor = newColor
	End Sub

	' Widgets without an explicit color inherit the (dark) color of their parent.
	Private Shared Sub MapExplicitBackColor(ByVal aControl As Control, ByVal state As ThemedState)
		If ThemeManager.IsExplicitlySet(aControl, "BackColor") Then
			ThemeManager.SetBackColor(state, aControl, ThemeManager.Map(aControl.BackColor))
		End If
	End Sub

	Private Shared Sub MapExplicitForeColor(ByVal aControl As Control, ByVal state As ThemedState)
		If ThemeManager.IsExplicitlySet(aControl, "ForeColor") Then
			ThemeManager.SetForeColor(state, aControl, ThemeManager.Map(aControl.ForeColor))
		End If
	End Sub

	' Native widget parts (scrollbars, tree glyphs, list headers, title bar) are themed by Windows.
	' Must be re-done whenever the window handle is re-created.
	Private Shared Sub ApplyNativeTheme(ByVal aControl As Control)
		If Not aControl.IsHandleCreated Then
			Exit Sub
		End If

		Try
			If TypeOf aControl Is Form Then
				Dim useDarkMode As Integer = If(theIsDark, 1, 0)
				If DwmSetWindowAttribute(aControl.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, useDarkMode, 4) <> 0 Then
					DwmSetWindowAttribute(aControl.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, useDarkMode, 4)
				End If
				' Redraw the title bar now instead of when the window is next activated.
				SetWindowPos(aControl.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE Or SWP_NOSIZE Or SWP_NOZORDER Or SWP_NOACTIVATE Or SWP_FRAMECHANGED)
			ElseIf TypeOf aControl Is ComboBox Then
				Dim aComboBox As ComboBox = CType(aControl, ComboBox)
				Dim info As New COMBOBOXINFO()
				info.cbSize = Marshal.SizeOf(info)
				If GetComboBoxInfo(aComboBox.Handle, info) AndAlso info.hwndList <> IntPtr.Zero Then
					ThemeManager.SetNativeTheme(info.hwndList, "DarkMode_Explorer")
				End If
				ThemeManager.UpdatePainter(aComboBox, New ComboBoxPainter(aComboBox))
			ElseIf TypeOf aControl Is ListView Then
				ThemeManager.SetNativeTheme(aControl.Handle, "DarkMode_Explorer")
				Dim headerHandle As IntPtr = SendMessage(aControl.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero)
				If headerHandle <> IntPtr.Zero Then
					ThemeManager.SetNativeTheme(headerHandle, "DarkMode_ItemsView")
				End If
				ThemeManager.UpdatePainter(aControl, New ListViewPainter(headerHandle))
			ElseIf TypeOf aControl Is TextBox AndAlso Not CType(aControl, TextBox).Multiline Then
				ThemeManager.SetNativeTheme(aControl.Handle, "DarkMode_CFD")
			ElseIf TypeOf aControl Is TextBoxBase OrElse TypeOf aControl Is TreeView OrElse TypeOf aControl Is ListBox OrElse TypeOf aControl Is ScrollBar OrElse TypeOf aControl Is DataGridView OrElse (TypeOf aControl Is ScrollableControl AndAlso CType(aControl, ScrollableControl).AutoScroll) Then
				ThemeManager.SetNativeTheme(aControl.Handle, "DarkMode_Explorer")
			End If

			Dim borderWidth As Integer = ThemeManager.GetBorderWidth(aControl)
			If borderWidth > 0 OrElse TypeOf aControl Is RichTextBox Then
				ThemeManager.UpdatePainter(aControl, New BorderPainter(aControl, borderWidth))
			End If
		Catch ex As Exception
			' Older Windows versions do not have these themes or functions; widgets keep their native look.
		End Try
	End Sub

	Private Shared Sub SetNativeTheme(ByVal handle As IntPtr, ByVal darkThemeName As String)
		If theIsDark Then
			SetWindowTheme(handle, darkThemeName, Nothing)
		Else
			SetWindowTheme(handle, Nothing, Nothing)
		End If
	End Sub

	' Width of the border that Windows draws in the non-client area with light system colors.
	Private Shared Function GetBorderWidth(ByVal aControl As Control) As Integer
		Dim aBorderStyle As BorderStyle = BorderStyle.None
		If TypeOf aControl Is Panel Then
			aBorderStyle = CType(aControl, Panel).BorderStyle
		ElseIf TypeOf aControl Is Label Then
			aBorderStyle = CType(aControl, Label).BorderStyle
		ElseIf TypeOf aControl Is RichTextBox OrElse (TypeOf aControl Is TextBox AndAlso CType(aControl, TextBox).Multiline) Then
			' Single-line text boxes get a dark border from their native theme.
			aBorderStyle = CType(aControl, TextBoxBase).BorderStyle
		End If

		If aBorderStyle = BorderStyle.FixedSingle Then
			Return 1
		ElseIf aBorderStyle = BorderStyle.Fixed3D Then
			Return 2
		End If
		Return 0
	End Function

	' In dark mode, attaches the painter to the widget's current window handle; in light mode, removes any painter.
	Private Shared Sub UpdatePainter(ByVal aControl As Control, ByVal newPainter As OverPainter)
		Dim painter As OverPainter = Nothing
		If thePainters.TryGetValue(aControl, painter) Then
			thePainters.Remove(aControl)
			painter.ReleaseHandle()
		End If
		If theIsDark Then
			newPainter.AssignHandle(aControl.Handle)
			thePainters.Add(aControl, newPainter)
		End If
		RedrawWindow(aControl.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE Or RDW_FRAME)
	End Sub

#End Region

#Region "Event Handlers"

	Private Shared Sub Settings_PropertyChanged(ByVal sender As Object, ByVal e As PropertyChangedEventArgs)
		If e.PropertyName = "AppTheme" Then
			ThemeManager.ApplyAll()
		End If
	End Sub

	Private Shared Sub SystemEvents_UserPreferenceChanged(ByVal sender As Object, ByVal e As UserPreferenceChangedEventArgs)
		If TheApp IsNot Nothing AndAlso TheApp.Settings IsNot Nothing AndAlso TheApp.Settings.AppTheme = AppThemeOptions.System Then
			ThemeManager.ApplyAll()
		End If
	End Sub

	Private Shared Sub Control_ControlAdded(ByVal sender As Object, ByVal e As ControlEventArgs)
		ThemeManager.ApplyToControl(e.Control)
	End Sub

	Private Shared Sub Control_HandleCreated(ByVal sender As Object, ByVal e As EventArgs)
		Dim aControl As Control = CType(sender, Control)
		Dim state As ThemedState = Nothing
		If theIsDark AndAlso theThemedStates.TryGetValue(aControl, state) Then
			ThemeManager.ApplyNativeTheme(aControl)
		End If
	End Sub

	Private Shared Sub TextBox_ReadOnlyOrEnabledChanged(ByVal sender As Object, ByVal e As EventArgs)
		Dim aTextBox As TextBoxBase = CType(sender, TextBoxBase)
		Dim state As ThemedState = Nothing
		If theIsDark AndAlso theThemedStates.TryGetValue(aTextBox, state) AndAlso Not state.HasExplicitBackColor Then
			aTextBox.BackColor = ThemeManager.GetInputBackColor(aTextBox)
		End If
	End Sub

	Private Shared Sub DataGridView_ColumnAdded(ByVal sender As Object, ByVal e As DataGridViewColumnEventArgs)
		Dim state As ThemedState = Nothing
		If theIsDark AndAlso theThemedStates.TryGetValue(sender, state) Then
			ThemeManager.DarkenDataGridViewColumn(e.Column, state)
		End If
	End Sub

	' Draws the border and caption in theme colors over the light ones drawn by Windows.
	Private Shared Sub GroupBox_Paint(ByVal sender As Object, ByVal e As PaintEventArgs)
		If Not theIsDark Then
			Exit Sub
		End If

		Dim aGroupBox As GroupBox = CType(sender, GroupBox)
		Dim g As Graphics = e.Graphics
		Dim textSize As Size = TextRenderer.MeasureText(g, aGroupBox.Text, aGroupBox.Font, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.SingleLine)
		Dim borderTop As Integer = textSize.Height \ 2

		Using backBrush As New SolidBrush(aGroupBox.BackColor)
			g.FillRectangle(backBrush, aGroupBox.ClientRectangle)
		End Using
		Using borderPen As New Pen(theDarkBorder)
			g.DrawRectangle(borderPen, 0, borderTop, aGroupBox.Width - 1, aGroupBox.Height - borderTop - 1)
		End Using
		If aGroupBox.Text.Length > 0 Then
			Dim textRect As New Rectangle(6, 0, textSize.Width, textSize.Height)
			Dim textColor As Color = If(aGroupBox.Enabled, aGroupBox.ForeColor, theDarkGrayText)
			TextRenderer.DrawText(g, aGroupBox.Text, aGroupBox.Font, textRect, textColor, aGroupBox.BackColor, TextFormatFlags.SingleLine)
		End If
	End Sub

	' Draws the check box or radio button glyph in theme colors over the light one drawn by Windows.
	Private Shared Sub GlyphButton_Paint(ByVal sender As Object, ByVal e As PaintEventArgs)
		If Not theIsDark Then
			Exit Sub
		End If

		' Only the default glyph position (left, vertically centered) is handled; other positions keep the native glyph.
		Dim checkAlign As ContentAlignment
		If TypeOf sender Is CheckBox Then
			checkAlign = CType(sender, CheckBox).CheckAlign
		Else
			checkAlign = CType(sender, RadioButton).CheckAlign
		End If
		If checkAlign <> ContentAlignment.MiddleLeft Then
			Exit Sub
		End If

		Dim aButton As ButtonBase = CType(sender, ButtonBase)
		Dim g As Graphics = e.Graphics
		Dim glyphSize As Integer = CInt(13 * g.DpiX / 96)
		Dim glyphTop As Integer = (aButton.Height - glyphSize) \ 2
		Dim glyphLeft As Integer = aButton.Padding.Left
		If aButton.RightToLeft = RightToLeft.Yes Then
			glyphLeft = aButton.Width - aButton.Padding.Right - glyphSize
		End If

		Using backBrush As New SolidBrush(aButton.BackColor)
			g.FillRectangle(backBrush, glyphLeft, glyphTop - 1, glyphSize + 2, glyphSize + 2)
		End Using
		Dim glyphRect As New Rectangle(glyphLeft + 1, glyphTop, glyphSize, glyphSize)
		If TypeOf aButton Is CheckBox Then
			ThemeManager.DrawCheckGlyph(g, glyphRect, CType(aButton, CheckBox).CheckState, aButton.Enabled)
		Else
			ThemeManager.DrawRadioGlyph(g, glyphRect, CType(aButton, RadioButton).Checked, aButton.Enabled)
		End If
	End Sub

#End Region

#Region "Private Classes"

	Private Class ThemedState
		Public ReadOnly RestoreActions As New List(Of Action)()
		Public HasExplicitBackColor As Boolean
	End Class

	' Listens to a widget's window messages in dark mode, to change or paint over what Windows draws.
	Private MustInherit Class OverPainter
		Inherits NativeWindow

		Protected Overrides Sub WndProc(ByRef m As Message)
			If theIsDark AndAlso Me.HandleMessage(m) Then
				Exit Sub
			End If
			MyBase.WndProc(m)
			If theIsDark Then
				Me.AfterMessage(m.Msg)
			End If
		End Sub

		' Returns True when the message was fully handled, so Windows must not process it.
		Protected Overridable Function HandleMessage(ByRef m As Message) As Boolean
			Return False
		End Function

		Protected Overridable Sub AfterMessage(ByVal msg As Integer)
		End Sub

	End Class

	' Paints the border of a Panel, Label or RichTextBox in theme colors, because Windows draws it with light system colors.
	' Also paints a disabled RichTextBox, because it ignores BackColor when disabled.
	Private Class BorderPainter
		Inherits OverPainter

		Public Sub New(ByVal aControl As Control, ByVal borderWidth As Integer)
			Me.theControl = aControl
			Me.theBorderWidth = borderWidth
		End Sub

		' Lets Windows paint the non-client area (e.g. scroll bars) without the border strip, so its light border
		'     never shows. Otherwise it flickers each time Windows redraws the border, e.g. while hovering a text box.
		Protected Overrides Sub WndProc(ByRef m As Message)
			If Not theIsDark OrElse m.Msg <> WM_NCPAINT OrElse Me.theBorderWidth = 0 Then
				MyBase.WndProc(m)
				Exit Sub
			End If

			Dim windowRect As RECT
			GetWindowRect(Me.Handle, windowRect)
			Dim insideRegion As IntPtr = CreateRectRgn(windowRect.Left + Me.theBorderWidth, windowRect.Top + Me.theBorderWidth, windowRect.Right - Me.theBorderWidth, windowRect.Bottom - Me.theBorderWidth)
			Dim updateRegion As IntPtr = m.WParam
			Try
				' A wParam of 1 means the whole window; otherwise it is the region to update, in screen coordinates.
				If updateRegion <> New IntPtr(1) Then
					CombineRgn(insideRegion, insideRegion, updateRegion, RGN_AND)
				End If
				m.WParam = insideRegion
				MyBase.WndProc(m)
			Finally
				m.WParam = updateRegion
				DeleteObject(insideRegion)
			End Try
		End Sub

		Protected Overrides Sub AfterMessage(ByVal msg As Integer)
			If msg = WM_NCPAINT AndAlso Me.theBorderWidth > 0 Then
				Me.PaintBorder()
			ElseIf msg = WM_PAINT AndAlso TypeOf Me.theControl Is RichTextBox AndAlso Not Me.theControl.Enabled Then
				Me.PaintDisabledText()
			End If
		End Sub

		Private Sub PaintDisabledText()
			' RichTextBoxEx paints itself (in theme colors) while it shows its cue banner.
			If TypeOf Me.theControl Is RichTextBoxEx Then
				Dim aRichTextBox As RichTextBoxEx = CType(Me.theControl, RichTextBoxEx)
				If aRichTextBox.CueBannerText <> "" AndAlso aRichTextBox.Text = "" Then
					Exit Sub
				End If
			End If

			' Draw from the first visible line so the text matches the scroll position.
			' Formatting is not kept; Windows also draws disabled text in one gray color.
			Dim aRichText As RichTextBox = CType(Me.theControl, RichTextBox)
			Dim firstVisibleCharIndex As Integer = aRichText.GetCharIndexFromPosition(New Point(1, 1))
			Dim firstVisibleLineIndex As Integer = aRichText.GetFirstCharIndexFromLine(aRichText.GetLineFromCharIndex(firstVisibleCharIndex))
			Dim visibleText As String = aRichText.Text
			If firstVisibleLineIndex > 0 AndAlso firstVisibleLineIndex < visibleText.Length Then
				visibleText = visibleText.Substring(firstVisibleLineIndex)
			End If
			Dim flags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPrefix Or TextFormatFlags.TextBoxControl
			If aRichText.WordWrap Then
				flags = flags Or TextFormatFlags.WordBreak
			End If

			Using g As Graphics = Graphics.FromHwnd(Me.Handle)
				Dim bounds As Rectangle = aRichText.ClientRectangle
				Using backBrush As New SolidBrush(aRichText.BackColor)
					g.FillRectangle(backBrush, bounds)
				End Using
				bounds.Inflate(-1, -1)
				TextRenderer.DrawText(g, visibleText, aRichText.Font, bounds, theDarkGrayText, flags)
			End Using
		End Sub

		Private Sub PaintBorder()
			Dim windowDC As IntPtr = GetWindowDC(Me.Handle)
			If windowDC = IntPtr.Zero Then
				Exit Sub
			End If
			Try
				Using g As Graphics = Graphics.FromHdc(windowDC)
					Dim width As Integer = Me.theControl.Width
					Dim height As Integer = Me.theControl.Height
					Using borderPen As New Pen(theDarkBorder)
						g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1)
					End Using
					If Me.theBorderWidth > 1 Then
						Using innerPen As New Pen(Me.theControl.BackColor)
							g.DrawRectangle(innerPen, 1, 1, width - 3, height - 3)
						End Using
					End If
				End Using
			Finally
				ReleaseDC(Me.Handle, windowDC)
			End Try
		End Sub

		Private ReadOnly theControl As Control
		Private ReadOnly theBorderWidth As Integer

	End Class

	' Sets the ListView column header text color, because the dark header theme draws it too dim.
	Private Class ListViewPainter
		Inherits OverPainter

		Public Sub New(ByVal headerHandle As IntPtr)
			Me.theHeaderHandle = headerHandle
		End Sub

		Protected Overrides Function HandleMessage(ByRef m As Message) As Boolean
			' NMHDR starts with hwndFrom, so check the sender before reading the rest.
			If m.Msg = WM_NOTIFY AndAlso Me.theHeaderHandle <> IntPtr.Zero AndAlso Marshal.ReadIntPtr(m.LParam) = Me.theHeaderHandle Then
				Dim header As NMHDR = CType(Marshal.PtrToStructure(m.LParam, GetType(NMHDR)), NMHDR)
				If header.code = NM_CUSTOMDRAW Then
					Dim customDraw As NMCUSTOMDRAW = CType(Marshal.PtrToStructure(m.LParam, GetType(NMCUSTOMDRAW)), NMCUSTOMDRAW)
					If customDraw.dwDrawStage = CDDS_PREPAINT Then
						m.Result = New IntPtr(CDRF_NOTIFYITEMDRAW)
						Return True
					ElseIf customDraw.dwDrawStage = CDDS_ITEMPREPAINT Then
						SetTextColor(customDraw.hdc, ColorTranslator.ToWin32(theDarkFore))
						m.Result = New IntPtr(CDRF_DODEFAULT)
						Return True
					End If
				End If
			End If
			Return False
		End Function

		Private ReadOnly theHeaderHandle As IntPtr

	End Class

	' Paints a ComboBox face in theme colors after Windows paints it, because Windows ignores BackColor there.
	Private Class ComboBoxPainter
		Inherits OverPainter

		Public Sub New(ByVal aComboBox As ComboBox)
			Me.theComboBox = aComboBox
		End Sub

		' A disabled editable ComboBox asks for its text box colors with WM_CTLCOLORSTATIC, which WinForms leaves to Windows.
		Protected Overrides Function HandleMessage(ByRef m As Message) As Boolean
			If m.Msg = WM_CTLCOLORSTATIC Then
				SetTextColor(m.WParam, ColorTranslator.ToWin32(theDarkGrayText))
				SetBkColor(m.WParam, ColorTranslator.ToWin32(theDarkBack))
				If theDarkBackBrush = IntPtr.Zero Then
					theDarkBackBrush = CreateSolidBrush(ColorTranslator.ToWin32(theDarkBack))
				End If
				m.Result = theDarkBackBrush
				Return True
			End If
			Return False
		End Function

		Protected Overrides Sub AfterMessage(ByVal msg As Integer)
			If msg = WM_PAINT Then
				Me.PaintFace()
			ElseIf msg = WM_SIZE Then
				' Windows redraws the resized combo box without WM_PAINT, so ask for one to paint the face again.
				Me.theComboBox.Invalidate(True)
			End If
		End Sub

		Private Sub PaintFace()
			Dim info As New COMBOBOXINFO()
			info.cbSize = Marshal.SizeOf(info)
			If Not GetComboBoxInfo(Me.theComboBox.Handle, info) Then
				Exit Sub
			End If

			Dim bounds As Rectangle = Me.theComboBox.ClientRectangle
			Dim itemRect As Rectangle = Rectangle.FromLTRB(info.rcItem.Left, info.rcItem.Top, info.rcItem.Right, info.rcItem.Bottom)
			Dim buttonRect As Rectangle = Rectangle.FromLTRB(info.rcButton.Left, bounds.Top, bounds.Right, bounds.Bottom)
			Dim isEnabled As Boolean = Me.theComboBox.Enabled
			Dim isEditable As Boolean = Me.theComboBox.DropDownStyle <> ComboBoxStyle.DropDownList
			Dim backColor As Color = If(isEnabled, Me.theComboBox.BackColor, theDarkBack)
			Dim foreColor As Color = If(isEnabled, Me.theComboBox.ForeColor, theDarkGrayText)

			Using g As Graphics = Graphics.FromHwnd(Me.theComboBox.Handle)
				If isEditable Then
					' The text area is a separate edit window that already uses BackColor.
					g.ExcludeClip(itemRect)
				End If
				Using backBrush As New SolidBrush(backColor)
					g.FillRectangle(backBrush, bounds)
				End Using

				If Not isEditable Then
					Dim textRect As Rectangle = itemRect
					Dim textColor As Color = foreColor
					If Me.theComboBox.Focused AndAlso Not Me.theComboBox.DroppedDown Then
						Using highlightBrush As New SolidBrush(theDarkHighlight)
							g.FillRectangle(highlightBrush, textRect)
						End Using
						textColor = Color.White
					End If
					textRect.Inflate(-1, 0)
					TextRenderer.DrawText(g, Me.theComboBox.Text, Me.theComboBox.Font, textRect, textColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
				End If

				Dim centerX As Integer = buttonRect.Left + buttonRect.Width \ 2
				Dim centerY As Integer = buttonRect.Top + buttonRect.Height \ 2
				Dim arrowPoints() As Point = {New Point(centerX - 4, centerY - 2), New Point(centerX + 4, centerY - 2), New Point(centerX, centerY + 2)}
				Using arrowBrush As New SolidBrush(foreColor)
					g.FillPolygon(arrowBrush, arrowPoints)
				End Using

				Dim isActive As Boolean = Me.theComboBox.Focused OrElse Me.theComboBox.DroppedDown
				Using borderPen As New Pen(If(isActive, theDarkAccent, theDarkBorder))
					g.DrawRectangle(borderPen, 0, 0, bounds.Width - 1, bounds.Height - 1)
				End Using
			End Using
		End Sub

		Private ReadOnly theComboBox As ComboBox

	End Class

	Private Class DarkToolStripRenderer
		Inherits ToolStripProfessionalRenderer

		Public Sub New()
			MyBase.New(New DarkColorTable())
			Me.RoundedEdges = False
		End Sub

		Protected Overrides Sub OnRenderItemText(ByVal e As ToolStripItemTextRenderEventArgs)
			e.TextColor = If(e.Item.Enabled, theDarkFore, theDarkGrayText)
			MyBase.OnRenderItemText(e)
		End Sub

		Protected Overrides Sub OnRenderArrow(ByVal e As ToolStripArrowRenderEventArgs)
			e.ArrowColor = If(e.Item Is Nothing OrElse e.Item.Enabled, theDarkFore, theDarkGrayText)
			MyBase.OnRenderArrow(e)
		End Sub

	End Class

	Private Class DarkColorTable
		Inherits ProfessionalColorTable

		Public Sub New()
			Me.UseSystemColors = False
		End Sub

		Public Overrides ReadOnly Property ToolStripGradientBegin() As Color
			Get
				Return theDarkBack
			End Get
		End Property
		Public Overrides ReadOnly Property ToolStripGradientMiddle() As Color
			Get
				Return theDarkBack
			End Get
		End Property
		Public Overrides ReadOnly Property ToolStripGradientEnd() As Color
			Get
				Return theDarkBack
			End Get
		End Property
		Public Overrides ReadOnly Property ToolStripBorder() As Color
			Get
				Return theDarkBorder
			End Get
		End Property
		Public Overrides ReadOnly Property ToolStripDropDownBackground() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ImageMarginGradientBegin() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ImageMarginGradientMiddle() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ImageMarginGradientEnd() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property MenuBorder() As Color
			Get
				Return theDarkBorder
			End Get
		End Property
		Public Overrides ReadOnly Property MenuItemBorder() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property MenuItemSelected() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property MenuItemSelectedGradientBegin() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property MenuItemSelectedGradientEnd() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property MenuItemPressedGradientBegin() As Color
			Get
				Return theDarkHotHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property MenuItemPressedGradientEnd() As Color
			Get
				Return theDarkHotHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonSelectedBorder() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonSelectedHighlight() As Color
			Get
				Return theDarkHotHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonSelectedGradientBegin() As Color
			Get
				Return theDarkHotHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonSelectedGradientMiddle() As Color
			Get
				Return theDarkHotHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonSelectedGradientEnd() As Color
			Get
				Return theDarkHotHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonPressedGradientBegin() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonPressedGradientMiddle() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonPressedGradientEnd() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonCheckedGradientBegin() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonCheckedGradientMiddle() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property ButtonCheckedGradientEnd() As Color
			Get
				Return theDarkSelectionBack
			End Get
		End Property
		Public Overrides ReadOnly Property SeparatorDark() As Color
			Get
				Return theDarkBorder
			End Get
		End Property
		Public Overrides ReadOnly Property SeparatorLight() As Color
			Get
				Return theDarkBack
			End Get
		End Property
		Public Overrides ReadOnly Property GripDark() As Color
			Get
				Return theDarkBorder
			End Get
		End Property
		Public Overrides ReadOnly Property GripLight() As Color
			Get
				Return theDarkBack
			End Get
		End Property
		Public Overrides ReadOnly Property OverflowButtonGradientBegin() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property OverflowButtonGradientMiddle() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property
		Public Overrides ReadOnly Property OverflowButtonGradientEnd() As Color
			Get
				Return theDarkHeaderBack
			End Get
		End Property

	End Class

#End Region

#Region "Native Methods"

	<StructLayout(LayoutKind.Sequential)>
	Private Structure RECT
		Public Left As Integer
		Public Top As Integer
		Public Right As Integer
		Public Bottom As Integer
	End Structure

	<StructLayout(LayoutKind.Sequential)>
	Private Structure NMHDR
		Public hwndFrom As IntPtr
		Public idFrom As IntPtr
		Public code As Integer
	End Structure

	<StructLayout(LayoutKind.Sequential)>
	Private Structure NMCUSTOMDRAW
		Public hdr As NMHDR
		Public dwDrawStage As Integer
		Public hdc As IntPtr
		Public rc As RECT
		Public dwItemSpec As IntPtr
		Public uItemState As Integer
		Public lItemlParam As IntPtr
	End Structure

	<StructLayout(LayoutKind.Sequential)>
	Private Structure COMBOBOXINFO
		Public cbSize As Integer
		Public rcItem As RECT
		Public rcButton As RECT
		Public stateButton As Integer
		Public hwndCombo As IntPtr
		Public hwndItem As IntPtr
		Public hwndList As IntPtr
	End Structure

	Private Const WM_SIZE As Integer = &H5
	Private Const WM_PAINT As Integer = &HF
	Private Const WM_NCPAINT As Integer = &H85
	Private Const WM_SETFONT As Integer = &H30
	Private Const RGN_AND As Integer = 1
	Private Const WM_NOTIFY As Integer = &H4E
	Private Const WM_CTLCOLORSTATIC As Integer = &H138
	Private Const NM_CUSTOMDRAW As Integer = -12
	Private Const CDDS_PREPAINT As Integer = &H1
	Private Const CDDS_ITEMPREPAINT As Integer = &H10001
	Private Const CDRF_DODEFAULT As Integer = &H0
	Private Const CDRF_NOTIFYITEMDRAW As Integer = &H20
	Private Const RDW_INVALIDATE As Integer = &H1
	Private Const RDW_FRAME As Integer = &H400
	Private Const LVM_GETHEADER As Integer = &H101F
	Private Const DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 As Integer = 19
	Private Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20
	Private Const SWP_NOSIZE As Integer = &H1
	Private Const SWP_NOMOVE As Integer = &H2
	Private Const SWP_NOZORDER As Integer = &H4
	Private Const SWP_NOACTIVATE As Integer = &H10
	Private Const SWP_FRAMECHANGED As Integer = &H20

	<DllImport("uxtheme.dll", CharSet:=CharSet.Unicode)>
	Private Shared Function SetWindowTheme(ByVal hWnd As IntPtr, ByVal pszSubAppName As String, ByVal pszSubIdList As String) As Integer
	End Function

	<DllImport("dwmapi.dll")>
	Private Shared Function DwmSetWindowAttribute(ByVal hwnd As IntPtr, ByVal attr As Integer, ByRef attrValue As Integer, ByVal attrSize As Integer) As Integer
	End Function

	<DllImport("user32.dll")>
	Private Shared Function SetWindowPos(ByVal hWnd As IntPtr, ByVal hWndInsertAfter As IntPtr, ByVal x As Integer, ByVal y As Integer, ByVal cx As Integer, ByVal cy As Integer, ByVal uFlags As Integer) As Boolean
	End Function

	<DllImport("user32.dll")>
	Private Shared Function SendMessage(ByVal hWnd As IntPtr, ByVal msg As Integer, ByVal wParam As IntPtr, ByVal lParam As IntPtr) As IntPtr
	End Function

	<DllImport("user32.dll")>
	Private Shared Function RedrawWindow(ByVal hWnd As IntPtr, ByVal lprcUpdate As IntPtr, ByVal hrgnUpdate As IntPtr, ByVal flags As Integer) As Boolean
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function SetTextColor(ByVal hdc As IntPtr, ByVal crColor As Integer) As Integer
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function SetBkColor(ByVal hdc As IntPtr, ByVal crColor As Integer) As Integer
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function CreateSolidBrush(ByVal crColor As Integer) As IntPtr
	End Function

	<DllImport("user32.dll")>
	Private Shared Function GetWindowDC(ByVal hWnd As IntPtr) As IntPtr
	End Function

	<DllImport("user32.dll")>
	Private Shared Function ReleaseDC(ByVal hWnd As IntPtr, ByVal hDC As IntPtr) As Integer
	End Function

	<DllImport("user32.dll", CharSet:=CharSet.Unicode)>
	Private Shared Function FindWindowEx(ByVal hwndParent As IntPtr, ByVal hwndChildAfter As IntPtr, ByVal lpszClass As String, ByVal lpszWindow As String) As IntPtr
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function DeleteObject(ByVal hObject As IntPtr) As Boolean
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function CreateRectRgn(ByVal left As Integer, ByVal top As Integer, ByVal right As Integer, ByVal bottom As Integer) As IntPtr
	End Function

	<DllImport("gdi32.dll")>
	Private Shared Function CombineRgn(ByVal hrgnDest As IntPtr, ByVal hrgnSrc1 As IntPtr, ByVal hrgnSrc2 As IntPtr, ByVal fnCombineMode As Integer) As Integer
	End Function

	<DllImport("user32.dll")>
	Private Shared Function GetWindowRect(ByVal hWnd As IntPtr, ByRef lpRect As RECT) As Boolean
	End Function

	<DllImport("user32.dll")>
	Private Shared Function GetComboBoxInfo(ByVal hWnd As IntPtr, ByRef pcbi As COMBOBOXINFO) As Boolean
	End Function

#End Region

#Region "Data"

	Private Shared ReadOnly theDarkBack As Color = Color.FromArgb(32, 32, 32)
	Private Shared ReadOnly theDarkInputBack As Color = Color.FromArgb(45, 45, 45)
	Private Shared ReadOnly theDarkHeaderBack As Color = Color.FromArgb(43, 43, 43)
	Private Shared ReadOnly theDarkHotHeaderBack As Color = Color.FromArgb(60, 60, 60)
	Private Shared ReadOnly theDarkSelectionBack As Color = Color.FromArgb(70, 70, 70)
	Private Shared ReadOnly theDarkButtonBack As Color = Color.FromArgb(51, 51, 51)
	Private Shared ReadOnly theDarkButtonHotBack As Color = Color.FromArgb(66, 66, 66)
	Private Shared ReadOnly theDarkButtonPressedBack As Color = Color.FromArgb(80, 80, 80)
	Private Shared ReadOnly theDarkButtonBorder As Color = Color.FromArgb(90, 90, 90)
	Private Shared ReadOnly theDarkBorder As Color = Color.FromArgb(75, 75, 75)
	Private Shared ReadOnly theDarkGlyphBorder As Color = Color.FromArgb(140, 140, 140)
	Private Shared ReadOnly theDarkFore As Color = Color.FromArgb(230, 230, 230)
	Private Shared ReadOnly theDarkGrayText As Color = Color.FromArgb(140, 140, 140)
	Private Shared ReadOnly theDarkHighlight As Color = Color.FromArgb(0, 90, 158)
	Private Shared ReadOnly theDarkAccent As Color = Color.FromArgb(0, 120, 215)
	Private Shared ReadOnly theDarkLink As Color = Color.FromArgb(110, 170, 255)
	Private Shared ReadOnly theDarkActiveLink As Color = Color.FromArgb(255, 120, 120)
	Private Shared ReadOnly theDarkGreenLink As Color = Color.FromArgb(110, 200, 110)

	Private Shared theIsInitialized As Boolean
	Private Shared theIsDark As Boolean
	Private Shared theOriginalToolStripRenderer As ToolStripRenderer
	Private Shared ReadOnly theThemedStates As New ConditionalWeakTable(Of Object, ThemedState)()
	Private Shared ReadOnly theHookedControls As New ConditionalWeakTable(Of Object, Object)()
	Private Shared ReadOnly thePainters As New ConditionalWeakTable(Of Control, OverPainter)()
	Private Shared theDarkBackBrush As IntPtr

#End Region

End Class
