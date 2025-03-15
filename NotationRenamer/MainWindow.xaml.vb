Imports System.Data
Imports System.IO
Imports System.Windows.Forms
Imports Microsoft.Data.SqlClient

Class MainWindow
    Private myConfig As New Utilities_NetCore.clsConfig
    Private db_Connection As New SqlConnection
    Private savePathDefault As String
    Private toBeFormattedDefaultPath As String

    Private Sub WindowLoaded() Handles Me.Loaded, btn_Reset.Click
#If DEBUG Then
        'three directories above exe
        Dim projectDir As String = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\.."))
        Dim connectionString As String = Environment.GetEnvironmentVariable("ConnectionStringDebug")
#Else
        'one directory above exe
        Dim projectDir As String = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."))
        Dim connectionString As String = Environment.GetEnvironmentVariable("ConnectionStringRelease")
#End If
        Dim configFile As String = Path.Combine(projectDir, "appsettings.json")
        myConfig.configFile = configFile

        Dim savePathDefault As String = Path.Combine(myConfig.getConfig("savePathRootDefault"), Date.Today.Year)
        Dim toBeFormattedDefaultPath As String = myConfig.getConfig("toBeFormattedPathDefault")

        If connectionString Is Nothing Then
            MessageBox.Show("Unable to read connection string", "Connection String Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Environment.Exit(-1)
        Else
            db_Connection = Utilities_NetCore.Connection(connectionString)
        End If

        'set initial states
        dp_StartDate.SelectedDate = Nothing
        dp_EndDate.SelectedDate = Nothing
        tb_RenamePath.Text = toBeFormattedDefaultPath.Replace("/", "\")
        tb_SavePath.Text = savePathDefault.Replace("/", "\")
        btn_Rename.IsEnabled = False

        'hide query
        dg_GameData.ItemsSource = Nothing
        dg_GameData.Visibility = Visibility.Hidden
    End Sub

    Private Sub RefreshDataGrid() Handles btn_Refresh.Click
        'validation
        Dim validationFailReason As String = ""
        If validationFailReason = "" Then
            If dp_StartDate.SelectedDate Is Nothing Then
                validationFailReason = "Missing Start Date"
            End If
        End If

        If validationFailReason = "" Then
            If dp_EndDate.SelectedDate Is Nothing Then
                validationFailReason = "Missing End Date"
            End If
        End If

        If validationFailReason = "" Then
            If dp_StartDate.SelectedDate > dp_EndDate.SelectedDate Then
                validationFailReason = "Start Date After End Date"
            End If
        End If

        If validationFailReason = "" Then
            If Not Path.Exists(tb_RenamePath.Text) Then
                validationFailReason = "To Be Formatted Path Does Not Exist"
            End If
        End If

        If validationFailReason = "" Then
            If Not Path.Exists(tb_SavePath.Text) Then
                validationFailReason = "Save Path Does Not Exist"
            End If
        End If

        Dim files As String() = Directory.GetFiles(tb_RenamePath.Text)
        If validationFailReason = "" Then
            If files.Length = 0 Then
                validationFailReason = "No Files Located In To Be Formatted Path"
            End If
        End If

        If validationFailReason <> "" Then
            'validation failed
            MessageBox.Show(validationFailReason, "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Else
            'refresh query to get a preview
            Dim query_text As String =
    "
SELECT
g.GameID,
g.SiteGameID,
wp.LastName AS WhiteLast,
bp.LastName AS BlackLast,
CONVERT(varchar(10), g.GameDate, 101) AS DatePlayed,
g.RoundNum,
CASE g.Result WHEN 0.5 THEN '0.5-0.5' WHEN 1 THEN '1-0' WHEN 0 THEN '0-1' END AS Result,
CONVERT(varchar(8), g.GameDate, 112) + '.' + CONVERT(varchar(2), g.RoundNum) + SPACE(1) + wp.LastName + '-' + bp.LastName + SPACE(1) + '(' + (CASE g.Result WHEN 0.5 THEN '0.5-0.5' WHEN 1 THEN '1-0' WHEN 0 THEN '0-1' END) + ')' AS 'File Name'

FROM ChessWarehouse.lake.Games g
JOIN ChessWarehouse.dim.Sources s ON g.SourceID = s.SourceID
JOIN ChessWarehouse.dim.Players wp ON g.WhitePlayerID = wp.PlayerID
JOIN ChessWarehouse.dim.Players bp ON g.BlackPlayerID = bp.PlayerID

WHERE s.SourceName = 'Personal'
AND g.GameDate BETWEEN @startDate AND @endDate

ORDER BY g.SiteGameID
"
            Using command As New SqlCommand
                command.Connection = db_Connection
                command.CommandType = Data.CommandType.Text
                command.CommandText = query_text
                command.Parameters.AddWithValue("@startDate", dp_StartDate.SelectedDate)
                command.Parameters.AddWithValue("@endDate", dp_EndDate.SelectedDate)

                Dim dataTable As New DataTable()
                Dim adapter As New SqlDataAdapter(command)
                adapter.Fill(dataTable)

                dg_GameData.ItemsSource = dataTable.DefaultView
            End Using

            dg_GameData.Visibility = Visibility.Visible

            If dg_GameData.Items.Count <> files.Length Then
                validationFailReason = "Number of Games Does Not Match Number of Files At To Be Formatted Path"
            End If

            If validationFailReason <> "" Then
                MessageBox.Show(validationFailReason, "Validation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Else
                btn_Rename.IsEnabled = True
            End If
        End If
    End Sub

    Private Sub RenameNotationCopies() Handles btn_Rename.Click
        'confirm the filenames are valid
        Dim isValidated As Boolean = True
        For Each game In dg_GameData.Items
            'the only way a file name would be invalid would be if the round number is missing, which would make the query return a null value
            If IsDBNull(game.Row.ItemArray(7)) Then
                isValidated = False
                Exit For
            End If
        Next

        If isValidated Then
            Try
                Dim ctr As Integer = 0
                Dim files As String() = Directory.GetFiles(tb_RenamePath.Text)  'assumption is this returns a sorted String() value
                For Each game In dg_GameData.Items
                    Dim newName = game.Row.ItemArray(7) & Path.GetExtension(files(ctr))
                    File.Move(files(ctr), Path.Combine(tb_SavePath.Text, newName))
                    ctr += 1
                Next
                btn_Rename.IsEnabled = False
                MessageBox.Show("Rename complete", "Result", MessageBoxButtons.OK, MessageBoxIcon.Information)

            Catch ex As Exception
                MessageBox.Show(ex.Message, "Result", MessageBoxButtons.OK, MessageBoxIcon.Error)

            End Try
        Else
            btn_Rename.IsEnabled = False
            MessageBox.Show("Invalid file name(s) present!", "Result", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
    End Sub

    Private Sub WindowClosed() Handles Me.Closed
        Try
            db_Connection.Close()
        Catch ex As Exception

        End Try
    End Sub
End Class
