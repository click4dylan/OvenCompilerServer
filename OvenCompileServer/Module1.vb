Imports System.Text.RegularExpressions
Imports System.Net.Mail
Imports System.IO.Packaging
Imports System.IO.Compression


Module Module1
    Public IsBusyCompiling As Boolean
    Public MapFile, MapFilePlusExtension, RadFile, Product, GameDir, Email, CompileLog, zipPath As String
    Public CSGargs As String = ""
    Public BSPargs As String = ""
    Public VISargs As String = ""
    Public RADargs As String = ""
    Public CurrentDirectory As String = IO.Directory.GetCurrentDirectory & "\"
    Sub Main()
        Console.Title = "BSP Oven Compiler Server"
        Do
            If Not IsBusyCompiling Then
                If MapIsWaiting() Then
                    If ParseINIFile() Then
                        IsBusyCompiling = True
                        CompileMap()
                        'todo COMPILE
                    End If
                End If
            End If
            Threading.Thread.Sleep(1000)
        Loop
    End Sub
    Sub SendEmail(ByVal compilererror As Boolean, ByVal JustNotifyThatItStarted As Boolean, ByVal watererror As Boolean, ByVal customerror As Boolean, ByVal custommessage As String)
        Console.WriteLine("Sending Email to " & Email)
        Dim smtpServer As New SmtpClient()
        Dim mail As New MailMessage()
        Dim oAttch As Net.Mail.Attachment
        smtpServer.UseDefaultCredentials = False
        smtpServer.Credentials = New Net.NetworkCredential("bspovencompiler@outlook.com", "asdf123123")
        smtpServer.Port = 587
        smtpServer.Host = "smtp.live.com"
        smtpServer.EnableSsl = True
        mail = New MailMessage()
        mail.From = New MailAddress("bspovencompiler@outlook.com")
        mail.To.Add(Email)
        If JustNotifyThatItStarted Then
            mail.Subject = "BSP Oven Compile Started"
            mail.Body = "This message is to notify you that we received your map and it is now being compiled. You will receive another message when it is finished."
        ElseIf compilererror Then
            mail.Subject = "BSP Oven Compile ERROR"
            mail.Body = CompileLog
        ElseIf watererror Then
            mail.Subject = "BSP Oven Compile Water ERROR"
            mail.Body = "This message is to notify you that one or more of your water models FAILED to compile!"
        ElseIf customerror Then
            mail.Subject = "BSP Oven Compile ERROR"
            mail.Body = custommessage
        Else
            mail.Subject = "BSP Oven Compile Finished"
            mail.Body = CompileLog
        End If
        If Not compilererror Then
            If IO.File.Exists(zipPath) Then
                Try
                    oAttch = New Net.Mail.Attachment(zipPath)
                    mail.Attachments.Add(oAttch)
                Catch ex As Exception
                    Console.WriteLine("Failed to attach map to email! Reason: " & ex.Message)
                    SendEmail(False, False, False, True, "Failed to attach map to email! Reason: " & ex.Message)
                End Try
            End If
        End If
        Try
            smtpServer.Send(mail)
        Catch ex As Exception
            Console.WriteLine("FAILED TO SEND EMAIL!")
            Console.WriteLine(ex.Message)
        End Try
        Try : oAttch.Dispose() : Catch : End Try
        If Not JustNotifyThatItStarted Then
            DeleteAllFiles(CurrentDirectory & MapFile)
            Delete(zipPath)
        End If
    End Sub
    Sub ZipUpBSP()
        zipPath = CurrentDirectory & MapFile & ".zip"
        If IO.File.Exists(zipPath) Then
            Try
                IO.File.Delete(zipPath)
            Catch : End Try
        End If

        Dim zip As Package = ZipPackage.Open(zipPath, IO.FileMode.Create, IO.FileAccess.ReadWrite)
        AddToArchive(zip, CurrentDirectory & MapFile & ".bsp")
        Dim modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.mdl")
        For Each file In modelfiles
            AddToArchive(zip, file)
        Next
        zip.Close()
    End Sub
    Sub UncompressFile(ByVal path As String)
        Dim x As New ICSharpCode.SharpZipLib.Zip.FastZip
        x.RestoreAttributesOnExtract = True
        Try
            x.ExtractZip(path, CurrentDirectory, "-\.db$")
            Delete(path)
        Catch ex As Exception
            Console.WriteLine("Failed on UncompressFile! Reason: " & ex.Message)
            SendEmail(False, False, False, True, "Your map upload failed the unzipping process. Reason: " & ex.Message)
        End Try

    End Sub

    Private Sub AddToArchive(ByVal zip As Package, ByVal fileToAdd As String)
        'taken from http://www.codeproject.com/Articles/28107/Zip-Files-Easy

        'Replace spaces with an underscore (_) 
        Dim uriFileName As String = fileToAdd.Replace(" ", "_")

        'A Uri always starts with a forward slash "/" 
        Dim zipUri As String = String.Concat("/", IO.Path.GetFileName(uriFileName))

        Dim partUri As New Uri(zipUri, UriKind.Relative)
        Dim contentType As String = Net.Mime.MediaTypeNames.Application.Zip

        'The PackagePart contains the information: 
        ' Where to extract the file when it's extracted (partUri) 
        ' The type of content stream (MIME type):  (contentType) 
        ' The type of compression:  (CompressionOption.Normal)   
        Dim pkgPart As PackagePart = zip.CreatePart(partUri, contentType, CompressionOption.Normal)

        'Read all of the bytes from the file to add to the zip file 
        Dim bites As Byte()
        Try
            bites = IO.File.ReadAllBytes(fileToAdd)
            'Compress and write the bytes to the zip file 
            pkgPart.GetStream().Write(bites, 0, bites.Length)
        Catch ex As Exception
            Console.WriteLine("Failed on AddToArchive, reason: " & ex.Message)
            SendEmail(False, False, False, True, "Your map failed to be compressed into a ZIP! Reason: " & ex.Message)
        End Try




    End Sub

    Function CompileMap()
        SendEmail(False, True, False, False, Nothing)
        Dim extraarguments As String = ""
        Dim HasErrored As Boolean = False
        Try
            IO.File.Delete(CurrentDirectory & MapFile & ".map.rdy")
        Catch
            IsBusyCompiling = False
            Return False
        End Try
        Dim compilerprefix As String
        If Product = "bond" Then
            compilerprefix = "b"
        ElseIf Product = "source" Then
            compilerprefix = "v"
        ElseIf Product = "goldsrc" Then
            compilerprefix = "hl"
        Else
            compilerprefix = "b"
        End If
        Dim dotmapzip As String = CurrentDirectory & MapFilePlusExtension & ".zip"
        If IO.File.Exists(dotmapzip) Then
            UncompressFile(dotmapzip)
        End If
        Delete(CurrentDirectory & "[Content_Types].xml")
        Delete(dotmapzip)
        If IO.File.Exists(MapFile & ".err") Then
            Delete(MapFile & ".err")
        End If
        If Product = "goldsrc" Then
            Dim csginfo As New ProcessStartInfo(CurrentDirectory & compilerprefix & "csg.exe", CSGargs & " " & ControlChars.Quote & MapFile & ControlChars.Quote)
            csginfo.WindowStyle = ProcessWindowStyle.Minimized
            csginfo.UseShellExecute = False
            csginfo.WorkingDirectory = CurrentDirectory
            Dim csg
            Try
                csg = Process.Start(csginfo)
                While Not csg.HasExited
                    Threading.Thread.Sleep(500)
                End While
            Catch ex As Exception
                Console.Write(ex.Message)
            End Try
        End If
        Dim compileerror() As String = IO.Directory.GetFiles(CurrentDirectory, "*.err")
        If Not compileerror.Length > 0 Then
            Dim bspinfo As New ProcessStartInfo(compilerprefix & "bsp.exe", BSPargs & " " & ControlChars.Quote & MapFile & ControlChars.Quote)
            bspinfo.WindowStyle = ProcessWindowStyle.Minimized
            bspinfo.UseShellExecute = False
            bspinfo.WorkingDirectory = CurrentDirectory
            Dim bsp As Process = Process.Start(bspinfo)
            While Not bsp.HasExited
                Threading.Thread.Sleep(500)
            End While
        Else
            HasErrored = True
        End If
        Dim studiomdlinfo
        Dim mdlconvertinfo
        If Not HasErrored Then
            If Product = "bond" Then
                Dim files = IO.Directory.GetFiles(CurrentDirectory, "*.qc")
                For Each qc In files
                    studiomdlinfo = New ProcessStartInfo("studiomdl.exe", ControlChars.Quote & qc & ControlChars.Quote)
                    studiomdlinfo.WorkingDirectory = CurrentDirectory
                    studiomdlinfo.WindowStyle = ProcessWindowStyle.Minimized
                    studiomdlinfo.UseShellExecute = False
                    Dim studiomdl As Process = Process.Start(studiomdlinfo)
                    While Not studiomdl.HasExited()
                        Threading.Thread.Sleep(100)
                    End While
                Next
                Dim watercompileerror = IO.Directory.GetFiles(CurrentDirectory, "*.err")
                If watercompileerror.Length > 0 Then
                    SendEmail(False, False, True, False, Nothing)
                End If
                Dim watermodels = IO.Directory.GetFiles(CurrentDirectory, "*.mdl")
                For Each file In watermodels
                    mdlconvertinfo = New ProcessStartInfo("mdlconvert.exe", "-convertv11tov14 " & file & " " & file)
                    mdlconvertinfo.WorkingDirectory = CurrentDirectory
                    mdlconvertinfo.WindowStyle = ProcessWindowStyle.Minimized
                    mdlconvertinfo.UseShellExecute = False
                    Dim mdlconvert As Process = Process.Start(mdlconvertinfo)
                    While Not mdlconvert.HasExited()
                        Threading.Thread.Sleep(100)
                    End While
                Next
            End If
        End If

        compileerror = IO.Directory.GetFiles(CurrentDirectory, "*.err")
        If Not compileerror.Length > 0 Then
            If Not VISargs.Contains("-threads") Then
                extraarguments = " -threads 8 "
            End If
            If Not VISargs.Contains("-low") Then
                extraarguments = extraarguments & " -low "
            End If
            Dim visinfo As New ProcessStartInfo(compilerprefix & "vis.exe", extraarguments & VISargs & " " & ControlChars.Quote & MapFile & ControlChars.Quote)
            visinfo.WindowStyle = ProcessWindowStyle.Minimized
            visinfo.WorkingDirectory = CurrentDirectory
            visinfo.UseShellExecute = False
            Dim vis As Process = Process.Start(visinfo)
            While Not vis.HasExited
                Threading.Thread.Sleep(500)
            End While
        Else
            HasErrored = True
        End If

        compileerror = IO.Directory.GetFiles(CurrentDirectory, "*.err")
        If Not compileerror.Length > 0 Then
            If Not RADargs.Contains("-threads") Then
                extraarguments = " -threads 1 "
            End If
            If Not RADargs.Contains("-low") Then
                extraarguments = extraarguments & " -low "
            End If
            Dim radfileargs As String = ""
            If Not RadFile = "" Then
                radfileargs = "-lights " & RadFile & " "
                'radfileargs = "-lights " & ControlChars.Quote & CurrentDirectory & RadFile & ControlChars.Quote & " "
            End If
            Dim radinfo As New ProcessStartInfo(compilerprefix & "rad.exe", extraarguments & RADargs & " " & radfileargs & ControlChars.Quote & MapFile & ControlChars.Quote)
            radinfo.WindowStyle = ProcessWindowStyle.Minimized
            radinfo.UseShellExecute = False
            radinfo.WorkingDirectory = CurrentDirectory
            Dim rad As Process = Process.Start(radinfo)
            While Not rad.HasExited
                Threading.Thread.Sleep(500)
            End While
        Else
            HasErrored = True
        End If

        compileerror = IO.Directory.GetFiles(CurrentDirectory, "*.err")
        If compileerror.Length > 0 Then
            HasErrored = True
        End If

        Try
            If IO.File.Exists(CurrentDirectory & MapFile & ".log") Then
                CompileLog = IO.File.ReadAllText(CurrentDirectory & MapFile & ".log")
            End If
        Catch
        End Try

        IsBusyCompiling = False

        If HasErrored Then
            SendEmail(True, False, False, False, Nothing)
        Else
            ZipUpBSP()
            SendEmail(False, False, False, False, Nothing)
        End If

        Return True
    End Function
    Sub DeleteAllFiles(ByVal file As String)
        Try
            Dim fileonly As String = file.Replace(".ini", "")
            Delete(file)
            Delete(fileonly & ".map.ini")
            Delete(fileonly & ".map")
            Delete(fileonly & ".rad")
            Delete(fileonly & ".map.rad")
            Delete(fileonly & ".rdy")
            Delete(fileonly & ".p0")
            Delete(fileonly & ".p1")
            Delete(fileonly & ".p2")
            Delete(fileonly & ".p3")
            Delete(fileonly & ".p4")
            Delete(fileonly & ".prt")
            Delete(fileonly & ".lin")
            Delete(fileonly & ".log")
            Delete(fileonly & ".pts")
            Delete(fileonly & ".lbsp")
            Delete(fileonly & ".bsp")
            Delete(fileonly & ".qc")
            Delete(fileonly & ".qc.err")
            Delete(fileonly & ".qc.log")
            Delete(fileonly & ".mdl.log")
            Delete(fileonly & ".mdl")
            Delete(fileonly & ".err")
            Delete(fileonly & ".b0")
            Delete(fileonly & ".b1")
            Delete(fileonly & ".b2")
            Delete(fileonly & ".b3")
            Delete(fileonly & ".hsz")
            Delete(fileonly & ".pln")
            Delete(fileonly & ".wa_")
        Catch
        End Try
        Dim modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.qc")
        For Each file In modelfiles
            Delete(file)
        Next
        modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.qc.err")
        For Each file In modelfiles
            Delete(file)
        Next
        modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.qc.log")
        For Each file In modelfiles
            Delete(file)
        Next
        modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.mdl")
        For Each file In modelfiles
            Delete(file)
        Next
        modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.mdl.log")
        For Each file In modelfiles
            Delete(file)
        Next
        modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.mdl.err")
        For Each file In modelfiles
            Delete(file)
        Next
        modelfiles = IO.Directory.GetFiles(CurrentDirectory, "*.smd")
        For Each file In modelfiles
            Delete(file)
        Next
    End Sub
    Sub Delete(ByVal file As String)
        Try
            If IO.File.Exists(file) Then
                IO.File.Delete(file)
            End If
        Catch
        End Try
    End Sub
    Function ParseINIFile()
        Dim files() As String = IO.Directory.GetFiles(CurrentDirectory, "*.ini")
        If files.Length > 0 Then
            Dim file As String = files(0)
            Dim filecontents As String = ""
            Try
                filecontents = IO.File.ReadAllText(file)
                If Not filecontents.Contains("[map]") Then
                    DeleteAllFiles(file)
                    IsBusyCompiling = False
                    Return False
                Else
                    filecontents = filecontents.Replace("[map]" & vbLf, "")
                    filecontents = filecontents.Substring(0, filecontents.Length - 1)
                End If
            Catch
                IsBusyCompiling = False
                DeleteAllFiles(file)
                Return False
            End Try

            Dim a = filecontents.Split(vbLf)
            Dim line As Integer = 0
            While line < a.Length
                Dim b = a(line).Trim
                Dim newsplit = b.Split("=")
                Dim arg = newsplit(0).Trim
                Dim value = newsplit(1).Trim
                Select Case arg
                    Case "mapfile"
                        MapFile = value.ToLower.Replace(".map", "")
                        MapFile = MapFile.ToLower.Replace(".vmf", "")
                        MapFilePlusExtension = value
                    Case "radfile"
                        RadFile = value
                    Case "product"
                        Product = value
                    Case "gamedir"
                        GameDir = value
                    Case "csg"
                        CSGargs = value
                    Case "bsp"
                        BSPargs = value
                    Case "vis"
                        VISargs = value
                    Case "rad"
                        RADargs = value
                    Case "mailto"
                        Email = value
                End Select
                line += 1
            End While
        End If
        Return True
    End Function
    Function MapIsWaiting()
        Dim files() As String = IO.Directory.GetFiles(CurrentDirectory, "*.rdy")
        If files.Length > 0 Then
            Return True
        End If
        Return False
    End Function
End Module
