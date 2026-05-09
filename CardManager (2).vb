Imports System
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Collections.Generic
Imports Microsoft.Win32

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub
End Module

Module WinSCard
    Public Const SCARD_SCOPE_SYSTEM As UInteger = 2
    Public Const SCARD_SHARE_SHARED As UInteger = 2
    Public Const SCARD_PROTOCOL_T0 As UInteger = 1
    Public Const SCARD_PROTOCOL_T1 As UInteger = 2
    Public Const SCARD_PROTOCOL_UNDEFINED As UInteger = 0
    Public Const SCARD_LEAVE_CARD As UInteger = 0
    Public Const SCARD_STATE_PRESENT As UInteger = &H20
    Public Const SCARD_STATE_CHANGED As UInteger = &H2
    Public Const SCARD_S_SUCCESS As Integer = 0
    Public Const SCARD_E_TIMEOUT As Integer = &H8010000A

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Public Structure SCARD_READERSTATE
        <MarshalAs(UnmanagedType.LPTStr)>
        Public szReader As String
        Public pvUserData As IntPtr
        Public dwCurrentState As UInteger
        Public dwEventState As UInteger
        Public cbAtr As UInteger
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=36)>
        Public rgbAtr() As Byte
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Public Structure SCARD_IO_REQUEST
        Public dwProtocol As UInteger
        Public cbPciLength As UInteger
    End Structure

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardEstablishContext(dwScope As UInteger, pvReserved1 As IntPtr, pvReserved2 As IntPtr, ByRef phContext As IntPtr) As Integer
    End Function

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardReleaseContext(hContext As IntPtr) As Integer
    End Function

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardListReaders(hContext As IntPtr, mszGroups As String, mszReaders As String, ByRef pcchReaders As UInteger) As Integer
    End Function

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardGetStatusChange(hContext As IntPtr, dwTimeout As UInteger, <[In], Out> rgReaderStates() As SCARD_READERSTATE, cReaders As UInteger) As Integer
    End Function

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardConnect(hContext As IntPtr, szReader As String, dwShareMode As UInteger, dwPreferredProtocols As UInteger, ByRef phCard As IntPtr, ByRef pdwActiveProtocol As UInteger) As Integer
    End Function

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardDisconnect(hCard As IntPtr, dwDisposition As UInteger) As Integer
    End Function

    <DllImport("winscard.dll", CharSet:=CharSet.Auto)>
    Public Function SCardTransmit(hCard As IntPtr, ByRef pioSendPci As SCARD_IO_REQUEST, pbSendBuffer() As Byte, cbSendLength As UInteger, ByRef pioRecvPci As SCARD_IO_REQUEST, pbRecvBuffer() As Byte, ByRef pcbRecvLength As UInteger) As Integer
    End Function

    Public Function GetCardUID(hCard As IntPtr, protocol As UInteger) As String
        Dim sendBuf() As Byte = {&HFF, &HCA, &H0, &H0, &H0}
        Dim recvBuf(258) As Byte
        Dim recvLen As UInteger = 259
        Dim sendPci As SCARD_IO_REQUEST
        Dim recvPci As SCARD_IO_REQUEST
        sendPci.dwProtocol = protocol
        sendPci.cbPciLength = CUInt(Marshal.SizeOf(sendPci))
        Dim rv As Integer = SCardTransmit(hCard, sendPci, sendBuf, CUInt(sendBuf.Length), recvPci, recvBuf, recvLen)
        If rv <> SCARD_S_SUCCESS OrElse recvLen < 3 Then Return Nothing
        Dim uidLen As Integer = CInt(recvLen) - 2
        If uidLen <= 0 Then Return Nothing
        Dim sb As New StringBuilder()
        For i As Integer = 0 To uidLen - 1
            sb.Append(recvBuf(i).ToString("X2"))
        Next
        Return sb.ToString()
    End Function

    Public Function ListReaders(hContext As IntPtr) As List(Of String)
        Dim result As New List(Of String)
        Dim size As UInteger = 0
        Dim rv As Integer = SCardListReaders(hContext, Nothing, Nothing, size)
        If rv <> SCARD_S_SUCCESS AndAlso rv <> &H8010002E Then Return result
        If size = 0 Then Return result
        Dim buf As String = New String(Chr(0), CInt(size))
        rv = SCardListReaders(hContext, Nothing, buf, size)
        If rv <> SCARD_S_SUCCESS Then Return result
        Dim parts() As String = buf.Split(Chr(0))
        For Each p As String In parts
            If p.Length > 0 Then result.Add(p)
        Next
        Return result
    End Function
End Module

Public Class HotkeyManager
    Implements IDisposable

    Private Shared _nextId As Integer = 1
    Private _handle As IntPtr
    Private _registeredIds As New List(Of Integer)

    Public Const MOD_ALT As UInteger = &H1
    Public Const MOD_CONTROL As UInteger = &H2

    <DllImport("user32.dll")>
    Private Shared Function RegisterHotKey(hWnd As IntPtr, id As Integer, fsModifiers As UInteger, vk As UInteger) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function UnregisterHotKey(hWnd As IntPtr, id As Integer) As Boolean
    End Function

    Public Sub New(windowHandle As IntPtr)
        _handle = windowHandle
    End Sub

    Public Function Register(modifiers As UInteger, vk As Keys) As Integer
        Dim id As Integer = Interlocked.Increment(_nextId)
        If RegisterHotKey(_handle, id, modifiers, CUInt(vk)) Then
            _registeredIds.Add(id)
            Return id
        End If
        Return -1
    End Function

    Public Sub UnregisterAll()
        For Each id As Integer In _registeredIds
            UnregisterHotKey(_handle, id)
        Next
        _registeredIds.Clear()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        UnregisterAll()
    End Sub
End Class

Public Class MainForm
    Inherits Form

    Private ReadOnly _exeDir As String = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)
    Private ReadOnly _cardIniPath As String
    Private ReadOnly _profilesDir As String

    Private _nfcThread As Thread
    Private _nfcRunning As Boolean = False
    Private _scContext As IntPtr = IntPtr.Zero
    Private _lastUID As String = ""
    Private _lastUIDTime As DateTime = DateTime.MinValue

    Private _watcher As FileSystemWatcher
    Private _pendingNfcUID As String = ""
    Private _waitingForNewCard As Boolean = False

    Private _swapMode As Boolean = False
    Private _swapStep As Integer = 0
    Private _swapSourceUID As String = ""

    Private _linkMode As Boolean = False

    Private _httpListener As HttpListener
    Private _httpThread As Thread
    Private _httpRunning As Boolean = False
    Private Const WEBHOOK_PORT As Integer = 7979

    Private _hotkeys As HotkeyManager
    Private _hotkeyNext As Integer = -1
    Private _hotkeyPrev As Integer = -1
    Private _profileList As New List(Of String)
    Private _currentProfileIndex As Integer = -1

    Private WithEvents lstProfiles As ListBox
    Private lblStatus As Label
    Private lblCurrentCard As Label
    Private lblNfcStatus As Label
    Private lblWebhook As Label
    Private btnSwitch As Button
    Private btnDelete As Button
    Private btnRefresh As Button
    Private btnSwap As Button
    Private btnLink As Button
    Private lblHotkeys As Label
    Private pnlHeader As Panel
    Private pnlInfoBox As Panel

    Private Const WM_HOTKEY As Integer = &H312

    Public Sub New()
        _cardIniPath = Path.Combine(_exeDir, "card.ini")
        _profilesDir = Path.Combine(_exeDir, "CardProfiles")
        InitializeComponent()
        EnsureProfilesDir()
        RefreshProfileList()
        StartNfcWatcher()
        StartWebhookListener()
        RegisterHotkeys()
        SetupFileWatcher()
        UpdateCurrentCardLabel()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "WMMT6 Card Manager – Project Asakura"
        Me.Size = New Size(620, 740)
        Me.MinimumSize = New Size(520, 640)
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.BackColor = Color.FromArgb(18, 18, 30)
        Me.ForeColor = Color.FromArgb(220, 220, 255)
        Me.Font = New Font("Segoe UI", 9)

        pnlHeader = New Panel()
        pnlHeader.Dock = DockStyle.Top
        pnlHeader.Height = 64
        pnlHeader.BackColor = Color.FromArgb(30, 24, 50)

        Dim lblTitle As New Label()
        lblTitle.Text = "WMMT6  ·  Card Profile Manager"
        lblTitle.Font = New Font("Segoe UI Semibold", 14, FontStyle.Bold)
        lblTitle.ForeColor = Color.FromArgb(255, 220, 80)
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(12, 10)

        Dim lblSub As New Label()
        lblSub.Text = "Project Asakura  /  OpenBanapass"
        lblSub.Font = New Font("Segoe UI", 8)
        lblSub.ForeColor = Color.FromArgb(160, 150, 200)
        lblSub.AutoSize = True
        lblSub.Location = New Point(14, 38)

        pnlHeader.Controls.Add(lblTitle)
        pnlHeader.Controls.Add(lblSub)

        lblNfcStatus = New Label()
        lblNfcStatus.Dock = DockStyle.Top
        lblNfcStatus.Height = 26
        lblNfcStatus.TextAlign = ContentAlignment.MiddleCenter
        lblNfcStatus.BackColor = Color.FromArgb(40, 35, 60)
        lblNfcStatus.ForeColor = Color.FromArgb(120, 220, 120)
        lblNfcStatus.Text = "⦿  Scanning for USB NFC reader (PC/SC)…"
        lblNfcStatus.Font = New Font("Segoe UI", 8.5)

        lblWebhook = New Label()
        lblWebhook.Name = "lblWebhook"
        lblWebhook.Dock = DockStyle.Top
        lblWebhook.Height = 24
        lblWebhook.TextAlign = ContentAlignment.MiddleCenter
        lblWebhook.BackColor = Color.FromArgb(18, 38, 22)
        lblWebhook.ForeColor = Color.FromArgb(100, 210, 130)
        lblWebhook.Text = "  ⦾  Webhook starting…"
        lblWebhook.Font = New Font("Segoe UI", 8)

        lblCurrentCard = New Label()
        lblCurrentCard.Dock = DockStyle.Top
        lblCurrentCard.Height = 40
        lblCurrentCard.TextAlign = ContentAlignment.MiddleCenter
        lblCurrentCard.BackColor = Color.FromArgb(24, 20, 42)
        lblCurrentCard.ForeColor = Color.FromArgb(255, 200, 80)
        lblCurrentCard.Font = New Font("Segoe UI", 9.5, FontStyle.Bold)
        lblCurrentCard.Text = "Active card: (none)"
        lblCurrentCard.Padding = New Padding(8, 0, 8, 0)

        Dim lblProfiles As New Label()
        lblProfiles.Text = "Card Profiles  (CardProfiles\)"
        lblProfiles.Font = New Font("Segoe UI Semibold", 8.5)
        lblProfiles.ForeColor = Color.FromArgb(180, 170, 230)
        lblProfiles.AutoSize = True

        lstProfiles = New ListBox()
        lstProfiles.BackColor = Color.FromArgb(26, 22, 44)
        lstProfiles.ForeColor = Color.FromArgb(220, 220, 255)
        lstProfiles.BorderStyle = BorderStyle.FixedSingle
        lstProfiles.Font = New Font("Consolas", 9)
        lstProfiles.SelectionMode = SelectionMode.One
        lstProfiles.IntegralHeight = False
        AddHandler lstProfiles.DoubleClick, AddressOf lstProfiles_DoubleClick

        btnSwitch = MakeButton("▶  Use Selected", Color.FromArgb(50, 120, 200))
        AddHandler btnSwitch.Click, AddressOf btnSwitch_Click

        btnDelete = MakeButton("✕  Delete Selected", Color.FromArgb(160, 50, 50))
        AddHandler btnDelete.Click, AddressOf btnDelete_Click

        btnRefresh = MakeButton("↺  Refresh List", Color.FromArgb(55, 55, 80))
        AddHandler btnRefresh.Click, AddressOf btnRefresh_Click

        btnSwap = MakeButton("⇄  Transfer Card UID", Color.FromArgb(80, 60, 130))
        AddHandler btnSwap.Click, AddressOf btnSwap_Click

        btnLink = MakeButton("⊕  Link to Card", Color.FromArgb(40, 100, 80))
        AddHandler btnLink.Click, AddressOf btnLink_Click

        pnlInfoBox = New Panel()
        pnlInfoBox.BackColor = Color.FromArgb(22, 20, 40)
        pnlInfoBox.BorderStyle = BorderStyle.FixedSingle
        pnlInfoBox.Padding = New Padding(10, 8, 10, 8)

        Dim lblInfoTitle As New Label()
        lblInfoTitle.Text = "ℹ  Feature Guide"
        lblInfoTitle.Font = New Font("Segoe UI Semibold", 8.5, FontStyle.Bold)
        lblInfoTitle.ForeColor = Color.FromArgb(200, 190, 255)
        lblInfoTitle.AutoSize = True
        lblInfoTitle.Location = New Point(10, 8)

        Dim lblInfoBody As New Label()
        lblInfoBody.Text =
            "⇄  Transfer Card UID — Tap OLD card (must exist), then NEW card (must not exist)." & vbCrLf &
            "    Renames the profile to the new UID. Old entry is removed." & vbCrLf & vbCrLf &
            "⊕  Link to Card — Links an unlinked card.ini to a physical NFC card." & vbCrLf &
            "    Card must not already exist in the system." & vbCrLf & vbCrLf &
            "⦾  Android Webhook — POST http://YOUR-PC-IP:7979/nfc" & vbCrLf &
            "    Body: {""uid"":""AABBCCDD""}   Use NFC Tools app on Android."
        lblInfoBody.Font = New Font("Segoe UI", 7.8)
        lblInfoBody.ForeColor = Color.FromArgb(155, 150, 195)
        lblInfoBody.Location = New Point(10, 28)
        lblInfoBody.Size = New Size(560, 100)

        pnlInfoBox.Controls.Add(lblInfoTitle)
        pnlInfoBox.Controls.Add(lblInfoBody)

        lblHotkeys = New Label()
        lblHotkeys.Text = "Hotkeys:  Ctrl+Alt+Right → next profile     Ctrl+Alt+Left → previous profile"
        lblHotkeys.Font = New Font("Segoe UI", 7.5)
        lblHotkeys.ForeColor = Color.FromArgb(120, 115, 165)
        lblHotkeys.TextAlign = ContentAlignment.MiddleCenter
        lblHotkeys.Dock = DockStyle.Bottom
        lblHotkeys.Height = 22
        lblHotkeys.BackColor = Color.FromArgb(20, 17, 35)

        lblStatus = New Label()
        lblStatus.Dock = DockStyle.Bottom
        lblStatus.Height = 24
        lblStatus.TextAlign = ContentAlignment.MiddleLeft
        lblStatus.BackColor = Color.FromArgb(16, 14, 28)
        lblStatus.ForeColor = Color.FromArgb(140, 200, 140)
        lblStatus.Font = New Font("Segoe UI", 8)
        lblStatus.Text = "  Ready."
        lblStatus.Padding = New Padding(6, 0, 0, 0)

        Dim tbl As New TableLayoutPanel()
        tbl.Dock = DockStyle.Fill
        tbl.BackColor = Color.Transparent
        tbl.Padding = New Padding(12)
        tbl.RowCount = 4
        tbl.ColumnCount = 2
        tbl.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        tbl.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 152))
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
        tbl.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 138))
        tbl.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))

        tbl.Controls.Add(lblProfiles, 0, 0)
        tbl.SetColumnSpan(lblProfiles, 2)
        tbl.Controls.Add(lstProfiles, 0, 1)

        Dim btnPanel As New FlowLayoutPanel()
        btnPanel.FlowDirection = FlowDirection.TopDown
        btnPanel.Dock = DockStyle.Fill
        btnPanel.BackColor = Color.Transparent
        For Each b As Button In {btnSwitch, btnDelete, btnRefresh, btnSwap, btnLink}
            b.Width = 142
            btnPanel.Controls.Add(b)
        Next
        tbl.Controls.Add(btnPanel, 1, 1)

        pnlInfoBox.Dock = DockStyle.Fill
        tbl.Controls.Add(pnlInfoBox, 0, 2)
        tbl.SetColumnSpan(pnlInfoBox, 2)

        tbl.Controls.Add(lblHotkeys, 0, 3)
        tbl.SetColumnSpan(lblHotkeys, 2)

        Controls.Add(tbl)
        Controls.Add(lblCurrentCard)
        Controls.Add(lblWebhook)
        Controls.Add(lblNfcStatus)
        Controls.Add(pnlHeader)
        Controls.Add(lblStatus)
    End Sub

    Private Function MakeButton(text As String, bg As Color) As Button
        Dim b As New Button()
        b.Text = text
        b.BackColor = bg
        b.ForeColor = Color.White
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 120)
        b.FlatAppearance.BorderSize = 1
        b.Height = 34
        b.Margin = New Padding(0, 0, 0, 6)
        b.Font = New Font("Segoe UI", 8.5)
        b.Cursor = Cursors.Hand
        Return b
    End Function

    Private Sub EnsureProfilesDir()
        If Not Directory.Exists(_profilesDir) Then
            Directory.CreateDirectory(_profilesDir)
            SetStatus("Created CardProfiles folder.")
        End If
    End Sub

    Private Sub PressCKey()
        Try
            SendKeys.SendWait("c")
        Catch
        End Try
    End Sub

    Private Sub RefreshProfileList()
        _profileList.Clear()
        lstProfiles.Items.Clear()
        If Not Directory.Exists(_profilesDir) Then Return
        Dim files() As String = Directory.GetFiles(_profilesDir, "*.ini")
        Array.Sort(files)
        For Each f As String In files
            _profileList.Add(f)
            lstProfiles.Items.Add(Path.GetFileNameWithoutExtension(f))
        Next
        UpdateCurrentSelection()
    End Sub

    Private Sub UpdateCurrentCardLabel()
        If File.Exists(_cardIniPath) Then
            Dim active As String = GetCurrentProfileName()
            If active IsNot Nothing Then
                lblCurrentCard.Text = $"Active card:  {active}"
                lblCurrentCard.ForeColor = Color.FromArgb(255, 200, 80)
                If btnLink IsNot Nothing Then btnLink.Enabled = False
            Else
                lblCurrentCard.Text = "Active card:  ⚠ unlinked card.ini  (use ⊕ Link to Card)"
                lblCurrentCard.ForeColor = Color.FromArgb(255, 140, 80)
                If btnLink IsNot Nothing Then btnLink.Enabled = True
            End If
        Else
            lblCurrentCard.Text = "Active card:  (no card.ini – start the game first)"
            lblCurrentCard.ForeColor = Color.FromArgb(170, 170, 170)
            If btnLink IsNot Nothing Then btnLink.Enabled = False
        End If
    End Sub

    Private Function GetCurrentProfileName() As String
        If Not File.Exists(_cardIniPath) Then Return Nothing
        Dim ac As String = ReadIniValue(_cardIniPath, "card", "accessCode")
        If String.IsNullOrEmpty(ac) Then Return Nothing
        For Each f As String In _profileList
            Dim pac As String = ReadIniValue(f, "card", "accessCode")
            If String.Equals(ac, pac, StringComparison.OrdinalIgnoreCase) Then
                Return Path.GetFileNameWithoutExtension(f)
            End If
        Next
        Return Nothing
    End Function

    Private Sub UpdateCurrentSelection()
        Dim current As String = GetCurrentProfileName()
        If current Is Nothing Then
            _currentProfileIndex = -1
            Return
        End If
        For i As Integer = 0 To _profileList.Count - 1
            If String.Equals(Path.GetFileNameWithoutExtension(_profileList(i)), current, StringComparison.OrdinalIgnoreCase) Then
                _currentProfileIndex = i
                lstProfiles.SelectedIndex = i
                Return
            End If
        Next
        _currentProfileIndex = -1
    End Sub

    Private Sub ActivateProfile(profilePath As String)
        If Not File.Exists(profilePath) Then
            SetStatus("Profile file not found: " & Path.GetFileName(profilePath))
            Return
        End If
        Try
            File.Copy(profilePath, _cardIniPath, overwrite:=True)
            Dim pName As String = Path.GetFileNameWithoutExtension(profilePath)
            SetStatus($"✓  Switched to profile: {pName}")
            UpdateCurrentCardLabel()
            UpdateCurrentSelection()
            PressCKey()
        Catch ex As Exception
            SetStatus("Error activating profile: " & ex.Message)
        End Try
    End Sub

    Private Sub HandleNewNfcCard(uid As String)
        _pendingNfcUID = uid
        _waitingForNewCard = True
        Try
            If File.Exists(_cardIniPath) Then File.Delete(_cardIniPath)
        Catch ex As Exception
            SetStatus("Could not delete card.ini: " & ex.Message)
            _waitingForNewCard = False
            Return
        End Try
        SetStatus($"New card [{uid}] – deleted card.ini. Press C in-game to register…")
        SendKeys.Send("c")
    End Sub

    Private Sub SetupFileWatcher()
        _watcher = New FileSystemWatcher(_exeDir)
        _watcher.Filter = "card.ini"
        _watcher.NotifyFilter = NotifyFilters.FileName Or NotifyFilters.LastWrite
        _watcher.EnableRaisingEvents = True
        AddHandler _watcher.Created, AddressOf OnCardIniCreated
        AddHandler _watcher.Changed, AddressOf OnCardIniCreated
    End Sub

    Private Sub OnCardIniCreated(sender As Object, e As FileSystemEventArgs)
        If Not _waitingForNewCard Then Return
        Thread.Sleep(500)
        Dim uid As String = _pendingNfcUID
        If String.IsNullOrEmpty(uid) Then Return
        Dim destPath As String = Path.Combine(_profilesDir, uid & ".ini")
        Try
            File.Copy(_cardIniPath, destPath, overwrite:=True)
            _waitingForNewCard = False
            _pendingNfcUID = ""
            If Me.InvokeRequired Then
                Me.Invoke(Sub()
                              RefreshProfileList()
                              UpdateCurrentCardLabel()
                              SetStatus($"✓  New profile saved: {uid}")
                          End Sub)
            Else
                RefreshProfileList()
                UpdateCurrentCardLabel()
                SetStatus($"✓  New profile saved: {uid}")
            End If
        Catch ex As Exception
            If Me.InvokeRequired Then
                Me.Invoke(Sub() SetStatus("Error saving new profile: " & ex.Message))
            End If
        End Try
    End Sub

    Private Sub StartNfcWatcher()
        _nfcRunning = True
        _nfcThread = New Thread(AddressOf NfcLoop)
        _nfcThread.IsBackground = True
        _nfcThread.Name = "NfcPoller"
        _nfcThread.Start()
    End Sub

    Private Sub NfcLoop()
        Dim rv As Integer = WinSCard.SCardEstablishContext(WinSCard.SCARD_SCOPE_SYSTEM, IntPtr.Zero, IntPtr.Zero, _scContext)
        If rv <> WinSCard.SCARD_S_SUCCESS Then
            SetNfcStatus("⚠  PC/SC context failed (0x" & rv.ToString("X8") & ")")
            Return
        End If
        While _nfcRunning
            Dim readers As List(Of String) = WinSCard.ListReaders(_scContext)
            If readers.Count = 0 Then
                SetNfcStatus("⦿  No USB NFC reader detected – plug in ACR122U…")
                Thread.Sleep(2000)
                Continue While
            End If
            SetNfcStatus("⦿  Reader: " & readers(0))
            Dim states(readers.Count - 1) As WinSCard.SCARD_READERSTATE
            For i As Integer = 0 To readers.Count - 1
                states(i).szReader = readers(i)
                states(i).dwCurrentState = WinSCard.SCARD_STATE_PRESENT
            Next
            rv = WinSCard.SCardGetStatusChange(_scContext, 1500, states, CUInt(states.Length))
            If rv = WinSCard.SCARD_E_TIMEOUT Then Continue While
            If rv <> WinSCard.SCARD_S_SUCCESS Then
                Thread.Sleep(1000)
                Continue While
            End If
            For Each st As WinSCard.SCARD_READERSTATE In states
                If (st.dwEventState And WinSCard.SCARD_STATE_PRESENT) <> 0 AndAlso
                   (st.dwEventState And WinSCard.SCARD_STATE_CHANGED) <> 0 Then
                    Dim uid As String = ReadCardUID(st.szReader)
                    If uid IsNot Nothing Then OnNfcCardTapped(uid)
                End If
            Next
        End While
        WinSCard.SCardReleaseContext(_scContext)
    End Sub

    Private Function ReadCardUID(readerName As String) As String
        Dim hCard As IntPtr = IntPtr.Zero
        Dim protocol As UInteger = 0
        Dim rv As Integer = WinSCard.SCardConnect(_scContext, readerName,
                                                   WinSCard.SCARD_SHARE_SHARED,
                                                   WinSCard.SCARD_PROTOCOL_T0 Or WinSCard.SCARD_PROTOCOL_T1,
                                                   hCard, protocol)
        If rv <> WinSCard.SCARD_S_SUCCESS Then
            protocol = WinSCard.SCARD_PROTOCOL_UNDEFINED
            rv = WinSCard.SCardConnect(_scContext, readerName, WinSCard.SCARD_SHARE_SHARED,
                                       WinSCard.SCARD_PROTOCOL_UNDEFINED, hCard, protocol)
        End If
        If rv <> WinSCard.SCARD_S_SUCCESS Then Return Nothing
        Try
            Return WinSCard.GetCardUID(hCard, protocol)
        Finally
            WinSCard.SCardDisconnect(hCard, WinSCard.SCARD_LEAVE_CARD)
        End Try
    End Function

    Private Sub OnNfcCardTapped(uid As String)
        If uid = _lastUID AndAlso (DateTime.Now - _lastUIDTime).TotalSeconds < 3 Then Return
        _lastUID = uid
        _lastUIDTime = DateTime.Now
        Dim profilePath As String = Path.Combine(_profilesDir, uid & ".ini")
        If Me.InvokeRequired Then
            Me.Invoke(Sub() ProcessCardTap(uid, profilePath))
        Else
            ProcessCardTap(uid, profilePath)
        End If
    End Sub

    Private Sub ProcessCardTap(uid As String, profilePath As String)
        If _linkMode Then
            _linkMode = False
            If File.Exists(profilePath) Then
                SetStatus($"⚠  Card [{uid}] already has a profile. Link cancelled.")
                UpdateNfcModeIndicator()
                Return
            End If
            If Not File.Exists(_cardIniPath) Then
                SetStatus("No card.ini found to link.")
                UpdateNfcModeIndicator()
                Return
            End If
            Try
                File.Copy(_cardIniPath, profilePath, overwrite:=False)
                RefreshProfileList()
                UpdateCurrentCardLabel()
                SetStatus($"✓  card.ini linked to card [{uid}]")
            Catch ex As Exception
                SetStatus("Link failed: " & ex.Message)
            End Try
            UpdateNfcModeIndicator()
            Return
        End If

        If _swapMode Then
            If _swapStep = 1 Then
                If Not File.Exists(profilePath) Then
                    SetStatus($"⚠  Card [{uid}] has no profile. Tap an EXISTING card first.")
                    Return
                End If
                _swapSourceUID = uid
                _swapStep = 2
                SetStatus($"⇄  Source locked: [{uid}]  —  Now tap the NEW card…")
                UpdateNfcModeIndicator()
                Return
            ElseIf _swapStep = 2 Then
                If uid = _swapSourceUID Then
                    SetStatus("⚠  Same card tapped twice. Tap a different NEW card.")
                    Return
                End If
                If File.Exists(profilePath) Then
                    SetStatus($"⚠  Card [{uid}] already has a profile. Transfer cancelled.")
                    CancelSwapMode()
                    Return
                End If
                Dim sourcePath As String = Path.Combine(_profilesDir, _swapSourceUID & ".ini")
                Try
                    File.Move(sourcePath, profilePath)
                    RefreshProfileList()
                    UpdateCurrentCardLabel()
                    SetStatus($"✓  Transferred [{_swapSourceUID}]  →  [{uid}]")
                Catch ex As Exception
                    SetStatus("Transfer failed: " & ex.Message)
                End Try
                CancelSwapMode()
                Return
            End If
        End If

        If File.Exists(profilePath) Then
            ActivateProfile(profilePath)
        Else
            SetStatus($"Unknown card [{uid}] – registering new profile…")
            HandleNewNfcCard(uid)
        End If
    End Sub

    Private Sub CancelSwapMode()
        _swapMode = False
        _swapStep = 0
        _swapSourceUID = ""
        UpdateNfcModeIndicator()
    End Sub

    Private Sub UpdateNfcModeIndicator()
        If _swapMode Then
            Dim stepTxt As String = If(_swapStep = 1, "tap OLD card…", "tap NEW card…")
            lblNfcStatus.BackColor = Color.FromArgb(80, 50, 20)
            lblNfcStatus.ForeColor = Color.FromArgb(255, 200, 80)
            lblNfcStatus.Text = $"⇄  TRANSFER MODE  —  Step {_swapStep}/2: {stepTxt}"
        ElseIf _linkMode Then
            lblNfcStatus.BackColor = Color.FromArgb(20, 60, 40)
            lblNfcStatus.ForeColor = Color.FromArgb(100, 240, 160)
            lblNfcStatus.Text = "⊕  LINK MODE  —  Tap the NFC card to link…"
        Else
            lblNfcStatus.BackColor = Color.FromArgb(40, 35, 60)
            lblNfcStatus.ForeColor = Color.FromArgb(120, 220, 120)
            lblNfcStatus.Text = "⦿  Ready – tap a card or use hotkeys"
        End If
    End Sub

    Private Sub StartWebhookListener()
        If Not HttpListener.IsSupported Then
            SetWebhookStatus("⚠  HttpListener not supported on this OS.")
            Return
        End If
        Try
            _httpListener = New HttpListener()
            _httpListener.Prefixes.Add($"http://+:{WEBHOOK_PORT}/nfc/")
            _httpListener.Start()
            _httpRunning = True
            _httpThread = New Thread(AddressOf WebhookLoop)
            _httpThread.IsBackground = True
            _httpThread.Name = "WebhookListener"
            _httpThread.Start()
            SetWebhookStatus($"⦾  Webhook ready — POST http://YOUR-PC-IP:{WEBHOOK_PORT}/nfc")
        Catch ex As HttpListenerException When ex.ErrorCode = 5
            SetWebhookStatus("⚠  Webhook needs URL reservation — run once as Admin, or see setup guide.")
        Catch ex As Exception
            SetWebhookStatus($"⚠  Webhook failed: {ex.Message}")
        End Try
    End Sub

    Private Sub WebhookLoop()
        While _httpRunning
            Try
                Dim ctx As HttpListenerContext = _httpListener.GetContext()
                ThreadPool.QueueUserWorkItem(Sub(o) HandleWebhookRequest(CType(o, HttpListenerContext)), ctx)
            Catch ex As HttpListenerException
                If _httpRunning Then SetWebhookStatus("⚠  Webhook error: " & ex.Message)
            Catch ex As ObjectDisposedException
            End Try
        End While
    End Sub

    Private Sub HandleWebhookRequest(ctx As HttpListenerContext)
        Dim resp As HttpListenerResponse = ctx.Response
        resp.Headers.Add("Access-Control-Allow-Origin", "*")
        Try
            If ctx.Request.HttpMethod = "OPTIONS" Then
                resp.StatusCode = 204
                resp.Close()
                Return
            End If
            If ctx.Request.HttpMethod <> "POST" Then
                resp.StatusCode = 405
                resp.Close()
                Return
            End If

            Dim body As String = ""
            Using reader As New StreamReader(ctx.Request.InputStream, Encoding.UTF8)
                body = reader.ReadToEnd().Trim()
            End Using

            Dim uid As String = ExtractUIDFromBody(body)

            If String.IsNullOrEmpty(uid) OrElse uid.Length < 4 Then
                resp.StatusCode = 400
                Dim errBytes() As Byte = Encoding.UTF8.GetBytes("{""error"":""could not parse UID""}")
                resp.ContentType = "application/json"
                resp.OutputStream.Write(errBytes, 0, errBytes.Length)
                resp.Close()
                Return
            End If

            uid = uid.ToUpperInvariant()
            resp.StatusCode = 200
            resp.ContentType = "application/json"
            Dim okBytes() As Byte = Encoding.UTF8.GetBytes($"{{""status"":""ok"",""uid"":""{uid}""}}")
            resp.OutputStream.Write(okBytes, 0, okBytes.Length)
            resp.Close()

            SetWebhookStatus($"⦾  Received UID via Android: {uid}")
            OnNfcCardTapped(uid)
        Catch ex As Exception
            Try
                resp.StatusCode = 500
                resp.Close()
            Catch
            End Try
        End Try
    End Sub

    Private Function ExtractUIDFromBody(body As String) As String
        If String.IsNullOrWhiteSpace(body) Then Return Nothing
        Dim jsonKeys() As String = {"""uid""", """UID""", """cardId""", """serial""", """id""", """tagId"""}
        For Each key As String In jsonKeys
            Dim idx As Integer = body.IndexOf(key, StringComparison.OrdinalIgnoreCase)
            If idx >= 0 Then
                Dim colonIdx As Integer = body.IndexOf(":"c, idx)
                If colonIdx >= 0 Then
                    Dim quoteStart As Integer = body.IndexOf(""""c, colonIdx)
                    If quoteStart >= 0 Then
                        Dim quoteEnd As Integer = body.IndexOf(""""c, quoteStart + 1)
                        If quoteEnd > quoteStart Then
                            Dim raw As String = body.Substring(quoteStart + 1, quoteEnd - quoteStart - 1)
                            Return raw.Replace(":", "").Replace("-", "").Replace(" ", "")
                        End If
                    End If
                End If
            End If
        Next
        Dim clean As String = body.Replace(":", "").Replace("-", "").Replace(" ", "")
        If clean.Length >= 4 AndAlso clean.Length <= 32 Then
            Dim isHex As Boolean = True
            For Each c As Char In clean
                If Not ((c >= "0"c AndAlso c <= "9"c) OrElse
                        (c >= "A"c AndAlso c <= "F"c) OrElse
                        (c >= "a"c AndAlso c <= "f"c)) Then
                    isHex = False
                    Exit For
                End If
            Next
            If isHex Then Return clean
        End If
        Return Nothing
    End Function

    Private Sub StopWebhookListener()
        _httpRunning = False
        Try
            If _httpListener IsNot Nothing Then
                _httpListener.Stop()
                _httpListener.Close()
            End If
        Catch
        End Try
    End Sub

    Private Sub SetWebhookStatus(msg As String)
        If lblWebhook Is Nothing Then Return
        If lblWebhook.InvokeRequired Then
            lblWebhook.Invoke(Sub() lblWebhook.Text = "  " & msg)
        Else
            lblWebhook.Text = "  " & msg
        End If
    End Sub

    Private Sub RegisterHotkeys()
        _hotkeys = New HotkeyManager(Me.Handle)
        _hotkeyNext = _hotkeys.Register(HotkeyManager.MOD_CONTROL Or HotkeyManager.MOD_ALT, Keys.Right)
        _hotkeyPrev = _hotkeys.Register(HotkeyManager.MOD_CONTROL Or HotkeyManager.MOD_ALT, Keys.Left)
        If _hotkeyNext = -1 OrElse _hotkeyPrev = -1 Then
            SetStatus("Warning: Could not register global hotkeys (already in use?)")
        End If
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_HOTKEY Then
            Dim id As Integer = m.WParam.ToInt32()
            If id = _hotkeyNext Then SwitchToNext()
            If id = _hotkeyPrev Then SwitchToPrev()
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub SwitchToNext()
        If _profileList.Count = 0 Then Return
        _currentProfileIndex = (_currentProfileIndex + 1) Mod _profileList.Count
        lstProfiles.SelectedIndex = _currentProfileIndex
        ActivateProfile(_profileList(_currentProfileIndex))
    End Sub

    Private Sub SwitchToPrev()
        If _profileList.Count = 0 Then Return
        _currentProfileIndex = ((_currentProfileIndex - 1) + _profileList.Count) Mod _profileList.Count
        lstProfiles.SelectedIndex = _currentProfileIndex
        ActivateProfile(_profileList(_currentProfileIndex))
    End Sub

    Private Sub btnSwitch_Click(sender As Object, e As EventArgs)
        If lstProfiles.SelectedIndex < 0 Then
            SetStatus("Select a profile first.")
            Return
        End If
        ActivateProfile(_profileList(lstProfiles.SelectedIndex))
    End Sub

    Private Sub btnDelete_Click(sender As Object, e As EventArgs)
        If lstProfiles.SelectedIndex < 0 Then
            SetStatus("Select a profile to delete.")
            Return
        End If
        Dim profilePath As String = _profileList(lstProfiles.SelectedIndex)
        Dim pName As String = Path.GetFileNameWithoutExtension(profilePath)
        Dim r As DialogResult = MessageBox.Show($"Delete profile ""{pName}""?", "Confirm Delete",
                                                 MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If r = DialogResult.Yes Then
            Try
                File.Delete(profilePath)
                RefreshProfileList()
                UpdateCurrentCardLabel()
                SetStatus($"Deleted profile: {pName}")
            Catch ex As Exception
                SetStatus("Delete failed: " & ex.Message)
            End Try
        End If
    End Sub

    Private Sub btnRefresh_Click(sender As Object, e As EventArgs)
        RefreshProfileList()
        UpdateCurrentCardLabel()
        SetStatus("Profile list refreshed.")
    End Sub

    Private Sub btnSwap_Click(sender As Object, e As EventArgs)
        If _swapMode Then
            CancelSwapMode()
            SetStatus("Transfer cancelled.")
            Return
        End If
        If _profileList.Count = 0 Then
            SetStatus("No profiles exist to transfer.")
            Return
        End If
        _swapMode = True
        _swapStep = 1
        _swapSourceUID = ""
        _linkMode = False
        SetStatus("⇄  Transfer mode — tap the OLD physical card first…")
        UpdateNfcModeIndicator()
    End Sub

    Private Sub btnLink_Click(sender As Object, e As EventArgs)
        If _linkMode Then
            _linkMode = False
            SetStatus("Link cancelled.")
            UpdateNfcModeIndicator()
            Return
        End If
        If Not File.Exists(_cardIniPath) Then
            SetStatus("No card.ini found. Start the game first.")
            Return
        End If
        If GetCurrentProfileName() IsNot Nothing Then
            SetStatus("Current card.ini is already linked to a profile.")
            Return
        End If
        _linkMode = True
        _swapMode = False
        CancelSwapMode()
        SetStatus("⊕  Link mode — tap the NFC card to assign to this card.ini…")
        UpdateNfcModeIndicator()
    End Sub

    Private Sub lstProfiles_DoubleClick(sender As Object, e As EventArgs)
        btnSwitch_Click(sender, e)
    End Sub

    Private Function ReadIniValue(filePath As String, section As String, key As String) As String
        If Not File.Exists(filePath) Then Return Nothing
        Dim inSection As Boolean = False
        For Each line As String In File.ReadAllLines(filePath)
            Dim trimmed As String = line.Trim()
            If trimmed.StartsWith("[") Then
                inSection = String.Equals(trimmed, $"[{section}]", StringComparison.OrdinalIgnoreCase)
            ElseIf inSection Then
                Dim idx As Integer = trimmed.IndexOf("="c)
                If idx > 0 Then
                    Dim k As String = trimmed.Substring(0, idx).Trim()
                    If String.Equals(k, key, StringComparison.OrdinalIgnoreCase) Then
                        Return trimmed.Substring(idx + 1).Trim()
                    End If
                End If
            End If
        Next
        Return Nothing
    End Function

    Private Sub SetStatus(msg As String)
        If lblStatus.InvokeRequired Then
            lblStatus.Invoke(Sub() lblStatus.Text = "  " & msg)
        Else
            lblStatus.Text = "  " & msg
        End If
    End Sub

    Private Sub SetNfcStatus(msg As String)
        If lblNfcStatus.InvokeRequired Then
            lblNfcStatus.Invoke(Sub() lblNfcStatus.Text = msg)
        Else
            lblNfcStatus.Text = msg
        End If
    End Sub

    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        _nfcRunning = False
        StopWebhookListener()
        If _watcher IsNot Nothing Then _watcher.Dispose()
        If _hotkeys IsNot Nothing Then _hotkeys.Dispose()
        MyBase.OnFormClosed(e)
    End Sub
End Class