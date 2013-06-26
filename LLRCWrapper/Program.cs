using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using System.Diagnostics;

namespace QCDMWrapper
{
    class Program
    {
        PRISM.DataBase.clsExecuteDatabaseSP _mExecuteSp;
        public static string Title = "Instrument_Category,Instrument,Acq_Time_Start,Dataset_ID,Dataset,Acq_Length,Dataset_Type,Curated_Quality,XIC_WideFrac,XIC_FWHM_Q1,XIC_FWHM_Q2,XIC_FWHM_Q3,XIC_Height_Q2,XIC_Height_Q3,XIC_Height_Q4,RT_Duration,RT_TIC_Q1,RT_TIC_Q2,RT_TIC_Q3,RT_TIC_Q4,RT_MS_Q1,RT_MS_Q2,RT_MS_Q3,RT_MS_Q4,RT_MSMS_Q1,RT_MSMS_Q2,RT_MSMS_Q3,RT_MSMS_Q4,MS1_TIC_Change_Q2,MS1_TIC_Change_Q3,MS1_TIC_Change_Q4,MS1_TIC_Q2,MS1_TIC_Q3,MS1_TIC_Q4,MS1_Count,MS1_Freq_Max,MS1_Density_Q1,MS1_Density_Q2,MS1_Density_Q3,MS2_Count,MS2_Freq_Max,MS2_Density_Q1,MS2_Density_Q2,MS2_Density_Q3,MS2_PrecZ_1,MS2_PrecZ_2,MS2_PrecZ_3,MS2_PrecZ_4,MS2_PrecZ_5,MS2_PrecZ_more,MS2_PrecZ_likely_1,MS2_PrecZ_likely_multi,SMAQC_Job,C_1A,C_1B,C_2A,C_2B,C_3A,C_3B,C_4A,C_4B,C_4C,DS_1A,DS_1B,DS_2A,DS_2B,DS_3A,DS_3B,IS_1A,IS_1B,IS_2,IS_3A,IS_3B,IS_3C,MS1_1,MS1_2A,MS1_2B,MS1_3A,MS1_3B,MS1_5A,MS1_5B,MS1_5C,MS1_5D,MS2_1,MS2_2,MS2_3,MS2_4A,MS2_4B,MS2_4C,MS2_4D,P_1A,P_1B,P_2A,P_2B,P_2C,P_3,Year,Month";
        public static string Connection = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";
        public static string Storedpro = "StoreQCDMResults";
        public static string Fileloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\";
        public static string LlrCloc = "QCDMscript.r";
        public static string Rloc = GetRPathFromWindowsRegistry();

        static int Main(string[] args)
        {
            try
            {
                var db = new DatabaseMang();
                db.Open();

                //Gets the arg that was input on console or asks for an input if none was given//
                string input;
                if(args.Length == 0)
                {
                    Console.WriteLine("Please enter a single Data ID ,a group seperated by commas, or a range seperated by - : ");
                    input = Console.ReadLine();
                }
                else
                {
                    if (args[0].Equals("-o") && args.Length > 1)
                    {
                        Fileloc = args[1];
                        input = args[2];
                        if (!Fileloc.EndsWith(@"/"))
                        {
                            Fileloc = Fileloc + @"/";
                        }
                    }
                    else
                    {
                        input = args[0];
                        if (args.Length > 1)
                        {
                            if (args[1].Equals("-o"))
                            {
                                Fileloc = args[2];
                                if (!Fileloc.EndsWith(@"/"))
                                {
                                    Fileloc = Fileloc + @"/";
                                }
                            }
                            else
                            {
                                Console.WriteLine("Your request is unkown");
                            }
                        }
                    }
                }

                //Figures out if you entered a single ID or a group of ID's//
                int size = 0;
                var list = new List<string>();
                bool skip = false;

                if (input != null && input.Contains(","))
                {
                    list = new List<string>(input.Split(','));
                    size = list.Count;
                    skip = true;
                }

                if (input != null && input.Contains("-"))
                {
                    list = new List<string>(input.Split('-'));
                    int num1;
                    int.TryParse(list[0], out num1);
                    int num2;
                    int.TryParse(list[1], out num2);
                    while (num1+1 < num2)
                    {
                        num2--;
                        list.Insert(1, num2.ToString(CultureInfo.InvariantCulture));
                    }
                    skip = true;
                    size = list.Count;
                }

                if (skip == false)
                {
                    list = new List<string> {input};
                    size = 1;
                }

                //SQL query to get data for each Dataset//
                var values = db.GetData(size, list);


                //Deletes files if they exist so no ones dont get confused//
                if (File.Exists(Fileloc + "TestingDataset.csv"))
                {
                    File.Delete(Fileloc + "TestingDataset.csv");
                }
                if (File.Exists(Fileloc + "data.csv"))
                {
                    File.Delete(Fileloc + "data.csv");
                }

                //Writes the data to a csv file//
                if (values.Count == 0)
                {
                    Console.WriteLine("No Datasets were found");
                    return 1;
                }

                WriteCsv(values, Title, size);

                //Creates the script for the R program and the batch file to run the script program//
                string ffileloc = Fileloc.Replace(@"\", "/");
                File.WriteAllText(ffileloc + LlrCloc, "require(QCDM)" + "\n" +
                "outDataName <- " + '"' + ffileloc + @"allData_v3.Rdata" + '"' + "\n" +
                "outputFolder <- " + '"' + ffileloc + '"' + "\n" +
                "ncdataFilename <- " + '"' + ffileloc + @"data.csv" + '"' + "\n" +
                "noncuratedPrediction(ncdataFilename=ncdataFilename, modelsFile=paste(outputFolder,\"Models_paper.Rdata\",sep=\"\"), dataFilename=outDataName,outputFolder=outputFolder)");
            
                File.WriteAllText(Fileloc + "RunR.bat", '"' + Rloc + '"' + " CMD BATCH --vanilla --slave " + '"' + Fileloc + LlrCloc + '"');

                //Runs the batch program//
                var p = new Process {StartInfo = {FileName = Fileloc + "RunR.bat"}};
                p.Start();

                //Checks to see if the files have been made//
                while (File.Exists(Fileloc + "TestingDataset.csv") == false)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                int error = 0;
                for (int i = 0; i < size; i++)
                {
                    //Gets the QCDM value from the csv file//
                    string smaqc = values[i][53];
                    string quametric = values[i][7];
                    string dataset = values[i][4];
                    string dataId = list[i];
                    string qcdmvalue = GetQcdm(i,dataId,error);

                    if (qcdmvalue.Equals("NA"))
                    {
                        Console.WriteLine(dataId + " did not have enough information to get the QCDM");
                        error++;
                    }
                    else
                    {
                        //Creates the xml//
                        string xml = ConvertQcdmtoXml(qcdmvalue, smaqc, quametric, dataset);

                        //Puts information on Database//
                        int data;
                        int.TryParse(dataId, out data);
                        var pro = new Program();
                        bool tf = pro.PostQcdmResultsToDb(data, xml, Connection, Storedpro);
                        if (tf)
                        {
                            Console.WriteLine(dataId + " was successfully Posted to DB");
                        }
                        else
                        {
                            Console.WriteLine("Something went wrong with the PostQCDMResultsToDB function");
                            return 1;
                        }
                    }
                }

                //Closes Connection and Waits for response to close//
                db.Close();
                return 0;
            }
                 
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }

        }

        public static void WriteCsv(List<List<string>> csv, String t, int size)
        {
            var sb = new StringBuilder();
            sb.AppendLine(t);

            for (int i = 0; i < size; i++)
            {
                string instru = csv[i][0];
                if (instru.Equals("LTQ") || instru.Equals("LTQ-ETD") || instru.Equals("LTQ-Prep") || instru.Equals("VelosPro"))
                {
                    if (csv[i][8] == "NA" || csv[i][41] == "NA" || csv[i][94] == "NA")
                    {
                        Console.WriteLine(csv[i][3] + " Has a missing value");
                    }
                    else
                    {
                        sb.Append("LTQ_IonTrap,");
                        for (int j = 1; j < 52; j++)
                        {
                            if (j < 5 || j >= 8)
                            {
                                sb.Append(csv[i][j] + ",");
                            }
                            if (j >= 5 && j < 8)
                            {
                                sb.Append("NA,");
                            }
                        }
                        for (int j = 53; j < 97; j++)
                        {
                            sb.Append(csv[i][j] + ",");
                        }
                        sb.AppendLine("2013,6");
                    }
                }
                if (instru.Equals("Exactive") || instru.Equals("QExactive"))
                {
                    if (csv[i][31] == "NA" || csv[i][36] == "NA")
                    {
                        Console.WriteLine(csv[i][3] + " Has a missing value");
                    }
                    else
                    {
                        sb.Append("Exactive,");
                        for (int j = 1; j < 52; j++)
                        {
                            if (j < 5 || j >= 8)
                            {
                                sb.Append(csv[i][j] + ",");
                            }
                            if (j >= 5 && j < 8)
                            {
                                sb.Append("NA,");
                            }
                        }
                        for (int j = 53; j < 97; j++)
                        {
                            sb.Append(csv[i][j] + ",");
                        }
                        sb.AppendLine("2013,6");
                    }
                }
                if (instru.Equals("LTQ-FT") || instru.Equals("Orbitrap"))
                {
                    if (csv[i][8] == "NA" || csv[i][28] == "NA" || csv[i][31] == "NA" || csv[i][36] == "NA" || csv[i][37] == "NA" || csv[i][64] == "NA" || csv[i][93] == "NA" || csv[i][92] == "NA" || csv[i][65] == "NA")
                    {
                        Console.WriteLine(csv[i][3] + " Has a missing value");
                    }
                    else
                    {
                        sb.Append("Orbitrap,");
                        for (int j = 1; j < 52; j++)
                        {
                            if (j < 5 || j >= 8)
                            {
                                sb.Append(csv[i][j] + ",");
                            }
                            if (j >= 5 && j < 8)
                            {
                                sb.Append("NA,");
                            }
                        }
                        for (int j = 53; j < 97; j++)
                        {
                            sb.Append(csv[i][j] + ",");
                        }
                        sb.AppendLine("2013,6");
                    }
                }
                if (instru.Equals("VelosOrbi"))
                {
                    if (csv[i][8] == "NA" || csv[i][41] == "NA" || csv[i][76] == "NA" || csv[i][36] == "NA" || csv[i][93] == "NA" || csv[i][92] == "NA" || csv[i][65] == "NA")
                    {
                        Console.WriteLine(csv[i][3] + " Has a missing value");
                    }
                    else
                    {
                        sb.Append("VOrbitrap,");
                        for (int j = 1; j < 52; j++)
                        {
                            if (j < 5 || j >= 8)
                            {
                                sb.Append(csv[i][j] + ",");
                            }
                            if (j >= 5 && j < 8)
                            {
                                sb.Append("NA,");
                            }
                        }
                        for (int j = 53; j < 97; j++)
                        {
                            sb.Append(csv[i][j] + ",");
                        }
                        sb.AppendLine("2013,6");
                    }
                }
            }
                
            File.WriteAllText(Fileloc + "data.csv", sb.ToString());
        }

        public static string GetQcdm(int i, string id, int e)
       {
            i++;
            i = i - e;
            string[] lines = File.ReadAllLines(Fileloc + "TestingDataset.csv");
            if (lines.Length == 1)
            {
                Console.WriteLine("Nothing was calculated");
                return "NA";
            }
            try
            {
                string info = lines[i];
                var curline = new List<string>(info.Split(','));

                //Test to see if the R program was able to get results//
                if (curline[3] == id)
                {
                    int spot = curline.Count - 1;
                    string qcdm = curline[spot];
                    return qcdm;
                }
                return "NA";
            }catch(Exception ex)
            {
                Console.WriteLine(ex);
                return "NA";
            }
       }

        public bool PostQcdmResultsToDb(int intDatasetId, string sXmlResults, string sConnectionString, string sStoredProcedure)
        {

            const int maxRetryCount = 3;
            const int secBetweenRetries = 20;

            int intStartIndex = 0;
            int intResult = 0;

            string sXMLResultsClean = null;

            System.Data.SqlClient.SqlCommand objCommand;

            bool blnSuccess = false;

            try
            {
                Console.WriteLine("Posting QCDM Results to the database (using Dataset ID " + intDatasetId.ToString() + ")");
            
                // We need to remove the encoding line from sXMLResults before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-8" standalone="yes"?>

                intStartIndex = sXmlResults.IndexOf("?>");
                if (intStartIndex > 0)
                {
                    sXMLResultsClean = sXmlResults.Substring(intStartIndex + 2).Trim();
                }
                else
                {
                    sXMLResultsClean = sXmlResults;
                }

                // Call stored procedure sStoredProcedure using connection string sConnectionString
                
                objCommand = new System.Data.SqlClient.SqlCommand();

                {
                    objCommand.CommandType = System.Data.CommandType.StoredProcedure;
                    objCommand.CommandText = sStoredProcedure;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@Return", System.Data.SqlDbType.Int));
                    objCommand.Parameters["@Return"].Direction = System.Data.ParameterDirection.ReturnValue;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@DatasetID", System.Data.SqlDbType.Int));
                    objCommand.Parameters["@DatasetID"].Direction = System.Data.ParameterDirection.Input;
                    objCommand.Parameters["@DatasetID"].Value = intDatasetId;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@ResultsXML", System.Data.SqlDbType.Xml));
                    objCommand.Parameters["@ResultsXML"].Direction = System.Data.ParameterDirection.Input;
                    objCommand.Parameters["@ResultsXML"].Value = sXMLResultsClean;
                }

                _mExecuteSp = new PRISM.DataBase.clsExecuteDatabaseSP(sConnectionString);
                AttachExecuteSpEvents();

                intResult = _mExecuteSp.ExecuteSP(objCommand, maxRetryCount, secBetweenRetries);

                if (intResult == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    blnSuccess = true;
                }
                else
                {
                    string error1 = "Error storing Quameter Results in database, " + sStoredProcedure + " returned " + intResult.ToString();
                    Console.WriteLine(error1);
                    blnSuccess = false;
                }

            }
            catch (System.Exception ex)
            {
                string error2 = "Exception storing Quameter Results in database" + ex;
                Console.WriteLine(error2);
                blnSuccess = false;
            }
            finally
            {
                DetachExecuteSpEvents();
                _mExecuteSp = null;
            }

            return blnSuccess;
        }

        private static string ConvertQcdmtoXml(string qcdmvalue, string smaqc, string quameter, string dataset)
        {
            var sbXml = new System.Text.StringBuilder();
            string sXmlResults;

            try
            {
                sbXml.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
                sbXml.Append("<QCDM_Results>");

                sbXml.Append("<Dataset>" + dataset + "</Dataset>");
                if (smaqc == "NA")
                {
                    smaqc = "0";
                }
                sbXml.Append("<SMAQC_Job>" + smaqc + "</SMAQC_Job>");
                sbXml.Append("<Quameter_Job>" + quameter + "</Quameter_Job>");

                sbXml.Append("<Measurements>");
                sbXml.Append("<Measurement Name=\"" + "QCDM" + "\">" + qcdmvalue + "</Measurement>");
                sbXml.Append("</Measurements>");

                sbXml.Append("</QCDM_Results>");

                sXmlResults = sbXml.ToString();

            }
            catch (Exception ex)
            {
                var error1 = "Error converting Quameter results to XML" + ex;
                Console.WriteLine(error1);
                return "";
            }

            return sXmlResults;

        }

        private void AttachExecuteSpEvents()
        {
            try
            {
                _mExecuteSp.DBErrorEvent += new PRISM.DataBase.clsExecuteDatabaseSP.DBErrorEventEventHandler(mExecuteSP_DBErrorEvent);
            }
            catch
            {
                // Ignore errors here
            }
        }

        private void DetachExecuteSpEvents()
        {
            try
            {
                if (_mExecuteSp != null)
                {
                    _mExecuteSp.DBErrorEvent -= mExecuteSP_DBErrorEvent;
                }
            }
            catch
            {
                // Ignore errors here
            }
        }

        private static void mExecuteSP_DBErrorEvent(string message)
        {
            Console.WriteLine("Stored procedure execution error: " + message);
        }

        private static string GetRPathFromWindowsRegistry()
        {
            const string RCORE_SUBKEY = @"SOFTWARE\R-core";

            Microsoft.Win32.RegistryKey regRCore = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RCORE_SUBKEY);
            if (regRCore == null)
            {
                throw new System.ApplicationException("Registry key is not found: " + RCORE_SUBKEY);
            }
            bool is64Bit = System.Environment.Is64BitProcess;
            string sRSubKey = is64Bit ? "R64" : "R";
            Microsoft.Win32.RegistryKey regR = regRCore.OpenSubKey(sRSubKey);
            if (regR == null)
            {
                throw new System.ApplicationException("Registry key is not found: " + RCORE_SUBKEY + @"\" + sRSubKey);
            }
            System.Version currentVersion = new System.Version((string)regR.GetValue("Current Version"));
            string installPath = (string)regR.GetValue("InstallPath");
            string bin = Path.Combine(installPath, "bin");
            bin = Path.Combine(bin, "R.exe");

            return bin;
        }
    }
}
