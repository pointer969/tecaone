Option Explicit On
Option Strict Off

Imports System.Globalization
Imports System.Text
Imports System.Xml
Imports SBODI_Server
Imports log4net
Imports Oracle.ManagedDataAccess.Client

Module TECADIS
    Public LastErrorCode As Integer
    Public LastErrorDescription As String
    Public ReadOnly DisObject As New Node
    Public SsId As String
    Public logger As ILog
    Public oraconn As New OracleConnection
    Public oraconntrx As New OracleConnection
    Public Ofilial As String
    Public newCGC As String
    Public NextCardCode As String
    Public JETrsId As String

    Sub Main()
        logger = LogManager.GetLogger("logmng")
        logger.Info("Starting TECA DI Server....")
        logger.Info("Version 1.0.0")

        Ofilial = My.Settings.oFilial
        Console.WriteLine("### ###  ##   #   ###   ##  #  ##      ")
        Console.WriteLine("#  #   #     # #    #  #   # # # #     ")
        Console.WriteLine("#  ##  #     ###  ###   #  ### ##      ")
        Console.WriteLine("#  #   #     # #  #      # # # #       ")
        Console.WriteLine("#  ###  ##   # #  ###  ##  # # #       ")
        Console.WriteLine("##################################")
        Console.WriteLine("Release: 201902070858")
        Console.WriteLine("Base:" & Ofilial)
        '# Release Control:
        ' 201901042145 - Fix Get BP Fiscal Id infor.
        ' 201901042155 - Added BPLID on SO
        ' 201901042207 - Fix eMail list to get only first
        ' 201901042218 - Fix Address for Contact List
        ' 201901042302 - Fix Single Email issue
        ' 201901042309 - Fix SO calling Update Cloud Scope
        ' 201901042349 - Define BP Series by Settings
        ' 201901072130 - Remove special character from Name
        ' 201901092307 - JE with Paid values instead total amount
        ' 201901101945 - Total amount without decimals
        ' 201901102045 - Fix signals
        ' 201901162102 - Reduce Memo String of JE
        ' 201901181628 - Fix Memo Issue
        ' 201901221922 - Double checking function to avoid interface confirmation w/o checking on SAP.
        ' 201901232126 - Debug values for SO.
        ' 201901232214 - Fix Validation
        ' 201902070711 - Add Key Control
        ' 201902070827 - Fix SO Total Amount
        ' 201902070845 - Fix duplicate issue
        ' 201902070858 - SO Total amount not detailed

        oraconn.ConnectionString = "User Id=" & My.Settings.ocuser & ";Password=" & My.Settings.ocpwd & ";Data Source=" & Ofilial & ";DBA Privilege=" & My.Settings.ocdblvl & ";"
        oraconntrx.ConnectionString = "User Id=" & My.Settings.ocuser & ";Password=" & My.Settings.ocpwd & ";Data Source=" & Ofilial & ";DBA Privilege=" & My.Settings.ocdblvl & ";"

        If ConectDIS() Then
            logger.Info("Login with success!")
            Mainscope()
        Else
            Console.Write("Connect to DIS fail!")
        End If
    End Sub
    Sub Mainscope()
        Try
            logger.Info("Connecting to Database...")
            '1. Connectar ao BD
            oraconn.Open()
            logger.Info("Connected!")
            '2.1 Verificar se a View existe no esquema para ser consultada
            'MsgLog("Verificar se a View existe no esquema para ser consultada")
            Dim cmdSchema As New OracleCommand With {
                    .Connection = oraconn,
                    .CommandText = "select count(*) as cnt from all_objects where object_name ='" & My.Settings.ocmainsrc & "'",
                    .CommandType = CommandType.Text
                }
            Dim drSchema As OracleDataReader = cmdSchema.ExecuteReader()
            drSchema.Read()
            Dim cCheck = drSchema.Item("cnt")


            If cCheck > 0 Then
                '2.2 Verificar se há algo para baixar
                'MsgLog("Verificar se há algo para baixar")
                Dim cmd As New OracleCommand With {
                                .Connection = oraconn,
                                .CommandText = "select count(*) as cnt from " & My.Settings.ocmainsrc,
                                .CommandType = CommandType.Text
                            }
                Dim dr As OracleDataReader = cmd.ExecuteReader()
                dr.Read()
                Dim qVals = dr.Item("cnt")

                If qVals > 0 Then
                    logger.Info("Rows to be processed: " + qVals.ToString())
                    GetValues()
                Else
                    logger.Info("No Itens to be processed!")
                End If
            Else
                logger.Info("The object " & My.Settings.ocmainsrc & " (View) does not exists on database, its impossible to keep going on, operation aborted.")
            End If
            oraconn.Close()

        Catch ex As OracleException
            Select Case ex.Number
                Case 1
                    logger.Error("Data already exists.")
                Case 12560
                    logger.Error("Database unreachable!")
                Case Else
                    logger.Error("TECADIS - Database Error: " + ex.Message.ToString())
            End Select
        Catch ex As Exception
            logger.Error(ex.Message.ToString())
        Finally
            oraconn.Dispose()
        End Try
    End Sub
    Sub GetValues()
        Try
            '2.3 Baixa os valores
            Dim cmd As New OracleCommand With {
                .Connection = oraconn,
                .CommandText = "select
                                    nvl(TO_CHAR(DTH_REGISTRO,'YYYYMMDD'),'') DTH_REGISTRO,  
                                    NUM_DOCTO_CLIENTE, 
                                    replace(NOM_DOCTO_CLIENTE,'&','e') as NOM_DOCTO_CLIENTE,
                                    ltrim(rtrim(NUM_DA)) NUM_DA,
                                    nvl(NUM_SEQ_DA,0) NUM_SEQ_DA,
                                    nvl(NUM_DV_DA,0) NUM_DV_DA,
                                    ltrim(rtrim(TIP_NATUREZA_PESSOA)) TIP_NATUREZA_PESSOA,
                                    ltrim(rtrim(NOM_BAIRRO)) NOM_BAIRRO,
                                    ltrim(rtrim(NUM_CEP)) NUM_CEP,
                                    ltrim(rtrim(NOM_CIDADE)) NOM_CIDADE,
                                    ltrim(rtrim(END_LOGRADOURO)) END_LOGRADOURO,
                                    ltrim(rtrim(END_NUM)) END_NUM,
                                    ltrim(rtrim(END_COMPLEMENTO)) END_COMPLEMENTO,
                                    ltrim(rtrim(SIG_UNIDADE_FEDERACAO)) SIG_UNIDADE_FEDERACAO,
                                    ltrim(rtrim(NUM_TELEFONE)) NUM_TELEFONE,
                                    ltrim(rtrim(NOM_END_EMAIL)) NOM_END_EMAIL,    
                                    ltrim(rtrim(COD_ATIVIDADE_ECONOMICA)) COD_ATIVIDADE_ECONOMICA,
                                    ltrim(rtrim(COD_MUNICIPIO_IBGE))  COD_MUNICIPIO_IBGE,
                                    ltrim(rtrim(NUM_INSCR_MUNICIPAL)) NUM_INSCR_MUNICIPAL,
                                    ltrim(rtrim(NUM_INSCR_ESTADUAL)) NUM_INSCR_ESTADUAL,
                                    ltrim(rtrim(SIG_PAIS)) SIG_PAIS,
                                    ltrim(rtrim(TIP_OPERACAO)) TIP_OPERACAO,
                                    ltrim(rtrim(TIP_STATUS_PAGTO)) TIP_STATUS_PAGTO,
                                    ltrim(rtrim(TIP_DOCUMENTO)) TIP_DOCUMENTO,
                                    ltrim(rtrim(COD_BANCO))  COD_BANCO,
                                    ltrim(rtrim(TO_CHAR(DAT_EMISSAO,'YYYYMMDD'))) DAT_EMISSAO,
                                    ltrim(rtrim(TO_CHAR(DAT_PAGTO,'YYYYMMDD'))) DAT_PAGTO,
                                    ltrim(rtrim(TO_CHAR(DAT_VENCTO,'YYYYMMDD'))) DAT_VENCTO,
                                    nvl(VAL_TARIFA,0) VAL_TARIFA,
                                    nvl(VAL_ATAERO,0) VAL_ATAERO,
                                    nvl(VAL_RETENCAO,0) VAL_RETENCAO,
                                    nvl(VAL_DEVIDO,0) VAL_DEVIDO,
                                    nvl(VAL_PAGO,0) VAL_PAGO,
                                    nvl(VAL_TOTAL_DA,0) VAL_TOTAL_DA,
                                    ltrim(rtrim(DSC_SERVICO)) DSC_SERVICO,
                                    nvl(NUM_QUANTIDADE,0) NUM_QUANTIDADE,
                                    nvl(VAL_SERVICO,0) VAL_SERVICO,
                                    ltrim(rtrim(TXT_ORIGEM_DA)) TXT_ORIGEM_DA,
                                    ltrim(rtrim(DAT_RECEBIMENTO_CARGA)) DAT_RECEBIMENTO_CARGA,
                                    ltrim(rtrim(DTH_INICIO_SERVICO)) DTH_INICIO_SERVICO,
                                    ltrim(rtrim(DTH_FIM_SERVICO)) DTH_FIM_SERVICO,
                                    ltrim(rtrim(DAT_ENTREGA_CARGA)) DAT_ENTREGA_CARGA,                
                                    TRIM(TO_CHAR(dth_registro,'ddhh24miss')||rownum) KYY
                        from teca." & My.Settings.ocmainsrc,
                .CommandType = CommandType.Text
            }
            Dim dr As OracleDataReader = cmd.ExecuteReader()
            Try
                If dr.HasRows Then
                    Dim cnt As Integer = 0
                    Console.WriteLine("Processing Rows...")
                    logger.Info("Data binding.")
                    While dr.Read()
                        Dim DA As New DocArrecad With {
                            .Filial = Ofilial.Substring(0, 1),
                            .DTH_REGISTRO = ConvertDBNull(dr.GetString(0)),
                            .NUM_DOCTO_CLIENTE = dr.GetString(1),
                            .NOM_DOCTO_CLIENTE = ConvertDBNull(dr.GetString(2)),
                            .NUM_DA = dr.GetString(3),
                            .NUM_SEQ_DA = dr.GetValue(4),
                            .NUM_DV_DA = dr.GetValue(5),
                            .TIP_NATUREZA_PESSOA = ConvertDBNull(dr.GetValue(6)),
                            .NOM_BAIRRO = ConvertDBNull(dr.GetValue(7)),
                            .NUM_CEP = ConvertDBNull(dr.GetValue(8)),
                            .NOM_CIDADE = ConvertDBNull(dr.GetValue(9)),
                            .END_LOGRADOURO = ConvertDBNull(dr.GetValue(10)),
                            .END_NUMERO = ConvertDBNull(dr.GetValue(11)),'END_NUM
                            .END_COMPLEMENTO = ConvertDBNull(dr.GetValue(12)),
                            .SIG_UNIDADE_FEDERACAO = ConvertDBNull(dr.GetValue(13)),
                            .NUM_TELEFONE = ConvertDBNull(dr.GetValue(14)),
                            .NOM_END_EMAIL = ConvertDBNull(dr.GetValue(15)),
                            .COD_ATIVIDADE_ECONOMICA = ConvertDBNull(dr.GetValue(16)),
                            .COD_MUNICIPIO_IBGE = ConvertDBNull(dr.GetValue(17)),
                            .NUM_INSCR_MUNICIPAL = ConvertDBNull(dr.GetValue(18)),
                            .NUM_INSCR_ESTADUAL = ConvertDBNull(dr.GetValue(19)),
                            .SIG_PAIS = ConvertDBNull(dr.GetValue(20)),
                            .TIP_OPERACAO = ConvertDBNull(dr.GetValue(21)),
                            .TIP_STATUS_PAGTO = ConvertDBNull(dr.GetValue(22)),
                            .TIP_DOCUMENTO = ConvertDBNull(dr.GetValue(23)),
                            .COD_BANCO = ConvertDBNull(dr.GetValue(24)),
                            .DAT_EMISSAO = ConvertDBNull(dr.GetValue(25)),
                            .DAT_PAGTO = ConvertDBNull(dr.GetValue(26)),
                            .DAT_VENCTO = ConvertDBNull(dr.GetValue(27)),
                            .VAL_TARIFA = ConvertDBNull(dr.GetValue(28)),
                            .VAL_ATAERO = ConvertDBNull(dr.GetValue(29)),
                            .VAL_RETENCAO = ConvertDBNull(dr.GetValue(30)),
                            .VAL_DEVIDO = ConvertDBNull(dr.GetValue(31)),
                            .VAL_PAGO = ConvertDBNull(dr.GetValue(32)),
                            .VAL_TOTAL_DA = ConvertDBNull(dr.GetValue(33)),
                            .DSC_SERVICO = ConvertDBNull(dr.GetValue(34)),
                            .NUM_QUANTIDADE = ConvertDBNull(dr.GetValue(35)),
                            .VAL_SERVICO = ConvertDBNull(dr.GetValue(36)),
                            .TXT_ORIGEM_DA = ConvertDBNull(dr.GetValue(37)),
                            .DAT_RECEBIMENTO_CARGA = ConvertDBNull(dr.GetValue(38)),
                            .DTH_INICIO_SERVICO = ConvertDBNull(dr.GetValue(39)),
                            .DTH_FIM_SERVICO = ConvertDBNull(dr.GetValue(40)),
                            .DAT_ENTREGA_CARGA = ConvertDBNull(dr.GetValue(41)),
                            .KKY = ConvertDBNull(dr.GetValue(42))
                        }
                        cnt = cnt + 1


                        '###################################
                        ' Rule to AddBP just when it does exists already
                        ' 04/01/2019 - Rule added
                        '###################################
                        GetBPbyTaxId(DA)
                        If NextCardCode = String.Empty Then
                            AddBP(DA)
                        End If

                        '###################################
                        ' AddJE
                        ' 19/12/2018 - New Logic According Operation Type
                        ' E - Estorno : TBD
                        ' Q - Quitação :  Use AddJE only
                        ' F - A faturar(correntista) : Use AddSO (Opened) only
                        ' G - Estorno correntista : TBD
                        '###################################

                        Select Case (DA.TIP_OPERACAO)
                            Case "E"
                                logger.Info("[ESTORNO] Scenario to be defined.")
                            Case "Q"
                                logger.Info("##########[QUITAÇÃO]##########")
                                GetDAData(DA, "JE")
                                If JETrsId = String.Empty Or JETrsId = "0" Then
                                    AddJE(DA)
                                End If
                                '# Double Check Action for SAP Confirmation
                                Console.Write("Double Verification for JE!")
                                Console.Write("Verifyng GetDAData for JE again!")
                                logger.Info("Verifyng GetDAData for JE again")
                                GetDAData(DA, "JE")
                                logger.Info("JETrsId=>" & JETrsId)
                                If JETrsId <> String.Empty Or JETrsId <> "0" Then
                                    updateDAonCloud(DA)
                                Else
                                    logger.Info("AddJE Failed. No data in SAP found.")
                                    logger.Info("DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & "  Check on it!")
                                    Console.Write("AddJE Failed. No data in SAP found.")
                                End If
                                Console.Write("!!!!!!!!!!!!!!!!!!!!!!!!!!!")
                                '###########################################
                                logger.Info("##############################")
                            Case "F"
                                logger.Info("##########[A FATURAR(CORRENTISTA)]##########")
                                GetDAData(DA, "SO")
                                If JETrsId = String.Empty Or JETrsId = "0" Then
                                    AddSO(DA)
                                End If
                                '# Double Check Action for SAP Confirmation
                                Console.Write("Double Verification for SO!")
                                Console.Write("Verifyng GetDAData for SO again!")
                                logger.Info("Verifyng GetDAData for SO again")
                                logger.Info("JETrsId=>" & JETrsId)
                                GetDAData(DA, "SO")
                                If JETrsId <> String.Empty Or JETrsId <> "0" Then
                                    updateDAonCloud(DA)
                                Else
                                    logger.Info("AddSO Failed. No data in SAP found.")
                                    logger.Info("DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & "  Check on it!")
                                    Console.Write("AddSO Failed. No data in SAP found.")
                                End If
                                Console.Write("!!!!!!!!!!!!!!!!!!!!!!!!!!!")
                                '###########################################
                                logger.Info("##############################")
                            Case "G"
                                logger.Info("[ESTORNO CORRENTISTA] Scenario to be defined.")
                            Case Else
                                logger.Info("Operation Type not categorized. DA.TIP_OPERACAO=" & DA.TIP_OPERACAO)
                        End Select

                        Console.Write("Item:" & cnt.ToString & " / ")
                        Console.WriteLine("DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & "  OK!")
                        logger.Info("DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & "  OK!")
                    End While
                    Console.Write("Cycle End")
                Else
                    logger.Info("No data to process!")
                End If
            Catch oe As OracleException
                logger.Error("Oracle Error:" & oe.Message.ToString)
            Finally
                dr.Close()
                oraconn.Close()
            End Try
        Catch ex As Exception
            logger.Error("GetValues Error:" & ex.Message.ToString())
        End Try
    End Sub
    Sub GetBPbyTaxId(ByRef DA As DocArrecad)
        logger.Info("GetBPbyTaxId():")
        Console.WriteLine("GetBPbyTaxId...")
        Dim returnValue As String = vbEmpty
        Dim xGBP As String

        '# Fiscal Code Scope
        newCGC = ConvertCGC(DA.NUM_DOCTO_CLIENTE, DA.TIP_NATUREZA_PESSOA)
        Dim qSQL As String
        If DA.TIP_NATUREZA_PESSOA = "J" Then
            qSQL = "SELECT DISTINCT CardCode FROM CRD7 WHERE  TaxId0 = '" & newCGC & "'"

        Else
            qSQL = "SELECT DISTINCT CardCode FROM CRD7 WHERE  TaxId4 = '" & newCGC & "'"
        End If
        '#####################

        xGBP = "<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Header><SessionID>" & SsId &
                     "</SessionID></env:Header><env:Body><dis:ExecuteSQL xmlns:dis=""http://www.sap.com/SBO/DIS"">" &
                     "<DoQuery>" &
                       qSQL &
                     "</DoQuery>" &
                     "</dis:ExecuteSQL></env:Body></env:Envelope>"

        Dim sb As New StringBuilder
        sb.Append("<?xml version=""1.0"" ?>").Append(xGBP)
        Dim soapMessage = sb.ToString()

        returnValue = DisObject.Interact(soapMessage)
        If (0 = LastErrorCode) Then
            Dim xmlDocument As New XmlDocument
            xmlDocument.LoadXml(returnValue)
            Dim reponseData = xmlDocument.SelectSingleNode("//*[local-name()='CardCode']")
            If InStr(xmlDocument.InnerXml, "<env:Fault>") Then
                Console.WriteLine("GetBPbyTaxIdReponse Erro")
                logger.Info("GetBPbyTaxIdResponse Error")
            Else
                If Not IsNothing(reponseData) Then
                    returnValue = reponseData.InnerText
                    Console.WriteLine("GetBPbyTaxIdReponse Result: {0}", returnValue)
                    logger.Info("GetBPbyTaxIdResponse Result:" & returnValue)
                    NextCardCode = returnValue
                End If
            End If
        Else
            logger.Info("GetBPbyTaxIdesponse No Result")
        End If

    End Sub
    Sub GetDAData(ByRef DA As DocArrecad, ByVal vTipo As String)
        logger.Info("GetDAData():")
        Console.WriteLine("GetDAData...")
        Dim returnValue As String = vbEmpty
        Dim xGDA As String

        xGDA = "<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Header><SessionID>" & SsId &
                     "</SessionID></env:Header><env:Body><dis:ExecuteSQL xmlns:dis=""http://www.sap.com/SBO/DIS"">" &
                     "  <DoQuery>"
        If vTipo = "JE" Then
            xGDA = xGDA & "      SELECT TransId FROM OJDT WHERE U_DA='" & DA.NUM_DA & "' AND U_DASEQ='" & DA.NUM_SEQ_DA & "' AND U_DADV ='" & DA.NUM_DV_DA & "' AND U_DATipo ='" & DA.TIP_DOCUMENTO & "' AND U_DARec = '" & DA.KKY & "' "
        Else
            xGDA = xGDA & "      SELECT DocEntry as TransId FROM ORDR WHERE U_U_DA='" & DA.NUM_DA & "' AND U_U_DASEQ='" & DA.NUM_SEQ_DA & "' AND U_U_DADV ='" & DA.NUM_DV_DA & "' AND U_U_DATipo ='" & DA.TIP_DOCUMENTO & "' AND U_U_DARec = '" & DA.KKY & "' "
        End If
        xGDA = xGDA & "  </DoQuery>" &
                     "</dis:ExecuteSQL></env:Body></env:Envelope>"

        logger.Info("GetDAData():xGDA:" & xGDA)
        Dim sb As New StringBuilder
        sb.Append("<?xml version=""1.0"" ?>").Append(xGDA)
        Dim soapMessage = sb.ToString()

        returnValue = DisObject.Interact(soapMessage)
        If (0 = LastErrorCode) Then
            Dim xmlDocument As New XmlDocument
            xmlDocument.LoadXml(returnValue)
            Dim reponseData = xmlDocument.SelectSingleNode("//*[local-name()='TransId']")
            If InStr(xmlDocument.InnerXml, "<env:Fault>") Then
                Console.WriteLine("GetDAData Erro")
                logger.Info("GetDAData Error:" & xmlDocument.InnerText)
            Else
                If Not IsNothing(reponseData) Then
                    returnValue = reponseData.InnerText
                    Console.WriteLine("GetDAData Result: {0}", returnValue)
                    logger.Info("GetDAData Result:" & returnValue)
                    JETrsId = returnValue
                End If
            End If
        Else
            logger.Info("GetDAData No Result")
        End If

    End Sub
    Sub AddJE(ByRef DA As DocArrecad)
        logger.Info("AddJE()")
        Dim returnValue As String = vbEmpty
        Dim xAddJE As String
        Try

            '# DueDate
            Dim dDueDate As String
            If (DA.DAT_VENCTO = "99991231") Then
                dDueDate = DA.DAT_PAGTO
            Else
                dDueDate = DA.DAT_VENCTO
            End If
            '#####################
            '# Memo Services
            '# Just Allow until 20 characters
            Dim sMEmo As String
            If (DA.DSC_SERVICO.Length > 20) Then
                sMEmo = DA.DSC_SERVICO.Substring(0, 20)
            Else
                sMEmo = DA.DSC_SERVICO
            End If
            '#####################

            xAddJE = "<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Header><SessionID>" & SsId &
                     "</SessionID></env:Header><env:Body><dis:AddObject xmlns:dis=""http://www.sap.com/SBO/DIS"">" &
                     "<BOM xmlns="""">" &
                     "<BO>" &
                     "<AdmInfo>" &
                     "<Object>oJournalEntries</Object>" &
                     "</AdmInfo>" &
                     "<JournalEntries>" &
                     "  <row>" &
                     "      <U_DA>" & DA.NUM_DA & "</U_DA>" &
                     "      <U_DASEQ>" & DA.NUM_SEQ_DA & "</U_DASEQ>" &
                     "      <U_DADV>" & DA.NUM_DV_DA & "</U_DADV>" &
                     "      <U_DATipo>" & DA.TIP_DOCUMENTO & "</U_DATipo>" &
                     "      <U_DARec>" & DA.KKY & "</U_DARec>" &
                     "      <ReferenceDate>" & DA.DTH_REGISTRO & "</ReferenceDate>" &
                     "      <DueDate>" & dDueDate & "</DueDate>" &
                     "      <Memo>DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & "|" & sMEmo & "</Memo>" &
                     "  </row>" &
                     "</JournalEntries>" &
                     "<JournalEntries_Lines>" &
                     "  <row>" &
                     "      <Line_ID>0</Line_ID>" &
                     "      <AccountCode>1.01.03.01.01</AccountCode>" &
                     "      <Debit>0</Debit>" &
                     "      <Credit>" & Replace(DA.VAL_PAGO, ",", ".") & "</Credit>" &
                     "      <DueDate>" & dDueDate & "</DueDate>" &
                     "      <ShortName>" & NextCardCode & "</ShortName>" &
                     "      <ContraAccount>1.01.01.02.01</ContraAccount>" &
                     "      <TaxDate>" & DA.DAT_EMISSAO & "</TaxDate>" &
                     "      <BPLID>" & DA.Filial & "</BPLID>" &
                     "  </row>" &
                     "  <row>" &
                     "      <Line_ID>1</Line_ID>" &
                     "      <AccountCode>1.01.01.02.01</AccountCode>" &
                     "      <Debit>" & Replace(DA.VAL_PAGO, ",", ".") & "</Debit>" &
                     "      <Credit>0</Credit>" &
                     "      <DueDate>" & dDueDate & "</DueDate>" &
                     "      <ShortName>1.01.01.02.01</ShortName>" &
                     "      <ContraAccount>" & NextCardCode & "</ContraAccount>" &
                     "      <TaxDate>" & DA.DAT_EMISSAO & "</TaxDate>" &
                     "      <BPLID>" & DA.Filial & "</BPLID>" &
                     "  </row>" &
                     "</JournalEntries_Lines>" &
                     "</BO>" &
                     "</BOM>" &
                     "</dis:AddObject></env:Body></env:Envelope>"

            Dim sb As New StringBuilder
            sb.Append("<?xml version=""1.0"" ?>").Append(xAddJE)
            Dim soapMessage = sb.ToString()

            returnValue = DisObject.Interact(soapMessage)
            If (0 = LastErrorCode) Then
                Dim xmlDocument As New XmlDocument
                xmlDocument.LoadXml(returnValue)
                Dim reponseData = xmlDocument.SelectSingleNode("//*[local-name()='Text']")
                If InStr(xmlDocument.InnerXml, "<env:Fault>") Then
                    returnValue = reponseData.InnerText
                    Console.WriteLine("JEAddResponse Error: {0}", returnValue)
                    logger.Info("JEAddResponse Error:" & returnValue)
                Else
                    If Not IsNothing(reponseData) Then
                        returnValue = reponseData.InnerText
                        Console.WriteLine("JEAddResponse Result: {0}", returnValue)
                        logger.Info("JEAddResponse Result:" & returnValue)
                    End If
                    'updateDAonCloud(DA)
                End If
            Else
                logger.Info("JEAddResponse No Result")
            End If
        Catch ex As Exception
            logger.Error("AddJE Error:" & ex.Message.ToString())
        End Try
    End Sub
    Sub AddSO(ByRef DA As DocArrecad)
        logger.Info("AddSO():")
        Dim returnValue As String = vbEmpty
        Dim xAddSO As String
        Try

            '# Valor Total / Qtde / Val.Unitario
            Dim vTotal As Decimal, qNum As Integer, vServ As Decimal
            If (DA.NUM_QUANTIDADE = 0 Or DA.VAL_SERVICO = 0) Then
                If (DA.VAL_PAGO <> 0) Then
                    vTotal = DA.VAL_PAGO
                ElseIf (DA.VAL_TARIFA <> 0) Then
                    vTotal = DA.VAL_TARIFA
                Else
                    vTotal = DA.VAL_TOTAL_DA
                End If
                qNum = 1
                vServ = vTotal
            Else
                vTotal = DA.NUM_QUANTIDADE * DA.VAL_SERVICO
                qNum = DA.NUM_QUANTIDADE
                vServ = DA.VAL_SERVICO
            End If
            logger.Info("AddSO++++++++")
            logger.Info("Key:" & DA.KKY)
            logger.Info("vTotal:" & vTotal.ToString)
            logger.Info("qNum:" & qNum.ToString)
            logger.Info("vServ:" & vServ.ToString)
            '#####################

            xAddSO = "<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Header><SessionID>" & SsId &
                     "</SessionID></env:Header><env:Body><dis:AddObject xmlns:dis=""http://www.sap.com/SBO/DIS"">" &
                     "<BOM xmlns="""">" &
                     "<BO>" &
                     "<AdmInfo>" &
                     "<Object>oOrders</Object>" &
                     "</AdmInfo>" &
                     "<Documents>" &
                     "  <row>" &
                     "      <U_U_DA>" & DA.NUM_DA & "</U_U_DA>" &
                     "      <U_U_DASEQ>" & DA.NUM_SEQ_DA & "</U_U_DASEQ>" &
                     "      <U_U_DADV>" & DA.NUM_DV_DA & "</U_U_DADV>" &
                     "      <U_U_DATipo>" & DA.TIP_DOCUMENTO & "</U_U_DATipo>" &
                     "      <U_U_DARec>" & DA.KKY & "</U_U_DARec>" &
                     "      <CardCode>" & NextCardCode & "</CardCode>" &
                     "      <Comments>Criado via I/F TECAPlus em " & Now().ToString & "</Comments>" &
                     "      <DiscountPercent>0</DiscountPercent>" &
                     "      <DocCurrency>R$</DocCurrency>" &
                     "      <DocDate>" & DA.DAT_EMISSAO & "</DocDate>" &
                     "      <DocDueDate>" & DA.DAT_VENCTO & "</DocDueDate>" &
                     "      <DocTotal>" & Replace(vTotal, ",", ".") & "</DocTotal>" &
                     "      <TaxDate>" & DA.DAT_PAGTO & "</TaxDate>" &
                     "      <BPL_IDAssignedToInvoice>" & DA.Filial & "</BPL_IDAssignedToInvoice>" &
                     "  </row>" &
                     "</Documents>" &
                     "<Document_Lines>" &
                     "  <row>" &
                     "      <ItemCode>ST00001PL</ItemCode>" &
                     "      <Quantity>" & qNum & "</Quantity>" &
                     "      <UnitPrice>" & Replace(vServ, ",", ".") & "</UnitPrice>" &
                     "      <FreeText>" & DA.DSC_SERVICO & "</FreeText>" &
                     "  </row>" &
                     "</Document_Lines>" &
                     "</BO>" &
                     "</BOM>" &
                     "</dis:AddObject></env:Body></env:Envelope>"

            Dim sb As New StringBuilder
            sb.Append("<?xml version=""1.0"" ?>").Append(xAddSO)
            Dim soapMessage = sb.ToString()

            returnValue = DisObject.Interact(soapMessage)
            If (0 = LastErrorCode) Then
                Dim xmlDocument As New XmlDocument
                xmlDocument.LoadXml(returnValue)
                Dim reponseData = xmlDocument.SelectSingleNode("//*[local-name()='Text']")
                If Not IsNothing(reponseData) Then
                    returnValue = reponseData.InnerText
                    Console.WriteLine("SOAddResponse Result: {0}", returnValue)
                    logger.Info("SOAddResponse Result:" & returnValue)
                End If
            Else
                logger.Info("SOAddResponse No Result")
            End If
        Catch ex As Exception
            logger.Error("AddSO Error:" & ex.Message.ToString())
        End Try
    End Sub
    Sub AddBP(ByRef DA As DocArrecad)
        logger.Info("AddBP():")
        Dim returnValue As String = vbEmpty
        Dim xAddPB As String
        Try

            '# ContactPerson Scope
            Dim ContactPerson As String
            If DA.NOM_END_EMAIL <> "" Then
                ContactPerson = DA.NOM_END_EMAIL.Substring(0, InStr(DA.NOM_END_EMAIL, "@", CompareMethod.Text) - 1)
            Else
                ContactPerson = ""
            End If
            '#####################

            '# Country Scope
            Dim Country As String
            If DA.SIG_PAIS <> "" Then
                Country = DA.SIG_PAIS.Substring(0, 2)
            Else
                Country = DA.SIG_PAIS
            End If
            '#####################

            '# State Scope
            Dim State As String
            If DA.SIG_UNIDADE_FEDERACAO <> "" Then
                State = DA.SIG_UNIDADE_FEDERACAO.Substring(0, 2)
            Else
                State = DA.SIG_UNIDADE_FEDERACAO
            End If
            '#####################

            '# Name Scope
            Dim xName As String
            If DA.NOM_END_EMAIL <> "" Then
                xName = DA.NOM_END_EMAIL.Substring(0, InStr(DA.NOM_END_EMAIL, "@", CompareMethod.Text) - 1)
            Else
                xName = ""
            End If
            '#####################

            '# Fiscal Code Scope
            newCGC = ConvertCGC(DA.NUM_DOCTO_CLIENTE, DA.TIP_NATUREZA_PESSOA)
            Dim nCPF As String, nCNPJ As String
            If DA.TIP_NATUREZA_PESSOA = "J" Then
                nCNPJ = newCGC
                nCPF = ""
            Else
                nCNPJ = ""
                nCPF = newCGC
            End If
            '#####################

            '# Email
            Dim eMail As String
            If InStr(DA.NOM_END_EMAIL, ",", CompareMethod.Text) > 0 Then
                eMail = DA.NOM_END_EMAIL.Substring(0, InStr(DA.NOM_END_EMAIL, ",", CompareMethod.Text) - 1)
            Else
                eMail = DA.NOM_END_EMAIL
            End If
            '#####################


            xAddPB = "<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Header><SessionID>" & SsId &
                     "</SessionID></env:Header><env:Body><dis:AddObject xmlns:dis=""http://www.sap.com/SBO/DIS"">" &
                     "<BOM xmlns="""">" &
                     "<BO>" &
                     "<AdmInfo>" &
                     "<Object>oBusinessPartners</Object>" &
                     "</AdmInfo>" &
                     "<BusinessPartners>" &
                     "  <row>" &
                     "      <CardName>" & RemoveAcentos(DA.NOM_DOCTO_CLIENTE) & "</CardName>" &
                     "      <Series>" & My.Settings.sapBPSeries & "</Series>" &
                     "      <CardType>C</CardType>" &
                     "      <Phone1>" & DA.NUM_TELEFONE & "</Phone1>" &
                     "      <ContactPerson>" & ContactPerson & "</ContactPerson>" &
                     "      <MailAddress>" & eMail & "</MailAddress>" &
                     "      <Notes>Criado via I/F TECAPlus em " & Now().ToString & ".</Notes>" &
                     "  </row>" &
                     "</BusinessPartners>" &
                     "<BPAddresses>" &
                     "  <row>" &
                     "      <AddressName>COBRANÇA</AddressName>" &
                     "      <Street>" & RemoveAcentos(DA.END_LOGRADOURO) & "</Street>" &
                     "      <Block>" & RemoveAcentos(DA.NOM_BAIRRO) & "</Block>" &
                     "      <ZipCode>" & DA.NUM_CEP & "</ZipCode>" &
                     "      <City>" & RemoveAcentos(DA.NOM_CIDADE) & "</City>" &
                     "      <Country>" & Country & "</Country>" &
                     "      <State>" & State & "</State>" &
                     "      <AddressType>B</AddressType>" &
                     "      <StreetNo>" & DA.END_NUMERO & "</StreetNo>" &
                     "  </row>" &
                     "  <row>" &
                     "      <AddressName>ENTREGA</AddressName>" &
                     "      <Street>" & RemoveAcentos(DA.END_LOGRADOURO) & "</Street>" &
                     "      <Block>" & RemoveAcentos(DA.NOM_BAIRRO) & "</Block>" &
                     "      <ZipCode>" & DA.NUM_CEP & "</ZipCode>" &
                     "      <City>" & RemoveAcentos(DA.NOM_CIDADE) & "</City>" &
                     "      <Country>" & Country & "</Country>" &
                     "      <State>" & State & "</State>" &
                     "      <AddressType>S</AddressType>" &
                     "      <StreetNo>" & DA.END_NUMERO & "</StreetNo>" &
                     "  </row>" &
                     "</BPAddresses>" &
                     "<ContactEmployees>" &
                     "  <row>" &
                     "      <Name>" & xName & "</Name>" &
                     "      <E_Mail>" & eMail & "</E_Mail>" &
                     "      <Address>ENTREGA</Address>" &
                     "  </row>" &
                     "</ContactEmployees>" &
                      "<BPFiscalTaxID>" &
                     "  <row>" &
                     "      <Address>COBRANÇA</Address>" &
                     "      <TaxId0>" & nCNPJ & "</TaxId0>" &
                     "      <TaxId1></TaxId1>" &
                     "      <TaxId4>" & nCPF & "</TaxId4>" &
                     "  </row>" &
                     "  <row>" &
                     "      <Address>ENTREGA</Address>" &
                     "      <TaxId0>" & nCNPJ & "</TaxId0>" &
                     "      <TaxId1>Isento</TaxId1>" &
                     "      <TaxId4>" & nCPF & "</TaxId4>" &
                     "  </row>" &
                     "</BPFiscalTaxID>" &
                     "</BO>" &
                     "</BOM>" &
                     "</dis:AddObject></env:Body></env:Envelope>"

            Dim sb As New StringBuilder
            sb.Append("<?xml version=""1.0"" ?>").Append(xAddPB)
            Dim soapMessage = sb.ToString()

            returnValue = DisObject.Interact(soapMessage)
            If (0 = LastErrorCode) Then
                Dim xmlDocument As New XmlDocument
                xmlDocument.LoadXml(returnValue)
                Dim reponseData = xmlDocument.SelectSingleNode("//*[local-name()='Text']")
                Dim reponseItem = xmlDocument.SelectSingleNode("//*[local-name()='Value']")
                If Not IsNothing(reponseData) Then

                    returnValue = reponseData.InnerText
                    Console.WriteLine("BPAddResponse Result: {0}", returnValue)
                    logger.Info("BPAddResponse Result:" & returnValue)
                    logger.Info("Debug info:" & soapMessage)
                    NextCardCode = returnValue

                End If
            Else
                logger.Info("BPAddResponse No Result")
            End If
        Catch ex As Exception
            logger.Error("AddBP Error:" & ex.Message.ToString())
        End Try
    End Sub
    Sub updateDAonCloud(ByRef DA As DocArrecad)
        logger.Info("updateDAonCloud():")

        Dim Sql As String

        Dim ocmd As New OracleCommand
        ocmd.CommandType = CommandType.Text
        ocmd.BindByName = True

        If Not oraconntrx.State Then
            oraconntrx.Close()
        End If
        oraconntrx.Open()
        ocmd.Connection = oraconntrx

        Sql = "update teca.INT_DA_CONCESSIONARIA set "
        Sql = Sql & " DTH_PROCESSAMENTO=current_date where NUM_DA='" & DA.NUM_DA & "' and NUM_DV_DA= '" & DA.NUM_DV_DA & "' and NUM_SEQ_DA= '" & DA.NUM_SEQ_DA & "' and TIP_DOCUMENTO= '" & DA.TIP_DOCUMENTO & "'"
        logger.Info("DA Sql=" & Sql)
        Try
            ocmd.CommandText = Sql
            ocmd.ExecuteNonQuery()

            Console.WriteLine("DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & " Saved on Database!")
            logger.Info("DA" & DA.TIP_DOCUMENTO & DA.NUM_DA & DA.NUM_SEQ_DA & "-" & DA.NUM_DV_DA & "  Saved on Database!")
        Catch ex As Exception
            logger.Error("updateDAonCloud Error:" & ex.Message.ToString())
        End Try
    End Sub
    Function ConectDIS() As Integer
        Dim returnValue As String = vbEmpty

        Dim enveloppeLogon As String = "<env:Envelope xmlns:env=""http://schemas.xmlsoap.org/soap/envelope/""><env:Body><dis:Login xmlns:dis=""http://www.sap.com/SBO/DIS""><DatabaseServer>{0}</DatabaseServer><DatabaseName>{1}</DatabaseName><DatabaseType>{2}</DatabaseType><CompanyUsername>{3}</CompanyUsername><CompanyPassword>{4}</CompanyPassword><Language>{5}</Language><LicenseServer>{6}</LicenseServer></dis:Login></env:Body></env:Envelope>"

        Dim xmlString = String.Format(CultureInfo.InvariantCulture,
                enveloppeLogon,
                My.Settings.sapserver,
                My.Settings.ocdatabase,
                My.Settings.sapdbtype,
                My.Settings.sapappuser,
                My.Settings.sapapppwd,
                My.Settings.sapapplang,
                My.Settings.saplicensesrv)
        Dim sb As New StringBuilder
        sb.Append("<?xml version=""1.0"" ?>").Append(xmlString)
        Dim soapMessage = sb.ToString()

        returnValue = DisObject.Interact(soapMessage)
        If (0 = LastErrorCode) Then
            Dim xmlDocument As New XmlDocument
            xmlDocument.LoadXml(returnValue)
            Dim sessionNode = xmlDocument.SelectSingleNode("//*[local-name()='SessionID']")
            If Not IsNothing(sessionNode) Then
                returnValue = sessionNode.InnerText
                SsId = returnValue
                Console.WriteLine("Connected to DIS. SessionId: {0}", returnValue)
                Return 1
            Else
                Return 0
            End If
        Else
            Return 0
        End If
    End Function
    Function ConvertDBNull(ByRef cVal) As String
        If IsDBNull(cVal) Then
            Return String.Empty
        Else
            Return cVal
        End If
    End Function
    Function ConvertCGC(ByVal vCGC As String, ByVal vTypeJF As String) As String
        logger.Info("ConvertCGC()")
        logger.Info("vCGC:" & vCGC)
        logger.Info("vTypeJF:" & vTypeJF)
        Try
            If vTypeJF = "F" Then
                'Case CPF
                Const cpfmask As String = "000\.000\.000\-00"
                ConvertCGC = Convert.ToInt64(vCGC).ToString(cpfmask)
                logger.Info("ConvertCGC:" & ConvertCGC)
            ElseIf vTypeJF = "J" Then
                'Case CMPJ
                Const cnpjmask As String = "00\.000\.000\/0000\-00"
                ConvertCGC = Convert.ToInt64(vCGC).ToString(cnpjmask)
                logger.Info("ConvertCGC:" & ConvertCGC)
            Else
                Return vCGC
            End If
        Catch ex As Exception
            Return vCGC
        End Try
    End Function
    Public Function RemoveAcentos(ByVal texto As String) As String
        Dim charFrom As String = "ŠŒŽšœžŸ¥µÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝßàáâãäåæçèéêëìíîïðñòóôõöøùúûüýÿ"
        Dim charTo As String = "SOZsozYYuAAAAAAACEEEEIIIIDNOOOOOOUUUUYsaaaaaaaceeeeiiiionoooooouuuuyy"
        For i As Integer = 0 To charFrom.Length - 1
            texto = Replace(texto, charFrom(i), charTo(i))
        Next
        Return texto
    End Function
End Module