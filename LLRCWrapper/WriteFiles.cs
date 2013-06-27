using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace QCDMWrapper
{
    class WriteFiles
    {
        public static string LlrCloc = "QCDMscript.r";
        public static string Rloc = GetRPathFromWindowsRegistry();

        //Deletes old files so they dont interfer with new ones
        public void DeleteFiles(string fileloc)
        {
            if (File.Exists(fileloc + "TestingDataset.csv"))
            {
                File.Delete(fileloc + "TestingDataset.csv");
            }
            if (File.Exists(fileloc + "data.csv"))
            {
                File.Delete(fileloc + "data.csv");
            }
        }

        //Writes the .R file to run the formula
        public void WriteRFile(string fileloc)
        {
            string ffileloc = fileloc.Replace(@"\", "/");
            File.WriteAllText(ffileloc + LlrCloc, "require(QCDM)" + "\n" +
            "outDataName <- " + '"' + ffileloc + @"allData_v3.Rdata" + '"' + "\n" +
            "outputFolder <- " + '"' + ffileloc + '"' + "\n" +
            "ncdataFilename <- " + '"' + ffileloc + @"data.csv" + '"' + "\n" +
            "noncuratedPrediction(ncdataFilename=ncdataFilename, modelsFile=paste(outputFolder,\"Models_paper.Rdata\",sep=\"\"), dataFilename=outDataName,outputFolder=outputFolder)");
        }

        //Writes the batch file to run the .R file
        public void WriteBatch(string fileloc)
        {
            File.WriteAllText(fileloc + "RunR.bat", '"' + Rloc + '"' + " CMD BATCH --vanilla --slave " + '"' + fileloc + LlrCloc + '"');
        }

        //Writes the data from the database into a .csv file to be used in the R program
        public void WriteCsv(List<List<string>> csv, String t, int size, string fileloc)
        {
            var sb = new StringBuilder();
            sb.AppendLine(t);
            const int Dataset = 3;
            const int XIC_WideFrac = 8;
            const int MS1_TIC_Change_Q2 = 28;
            const int MS1_TIC_Q2 = 31;
            const int MS1_Density_Q1 = 36;
            const int MS1_Density_Q2 = 37;
            const int MS2_Density_Q1 = 41;
            const int DS_2A = 64;
            const int DS_2B = 65;
            const int MS1_2B = 76;
            const int P_2A = 92;
            const int P_2B = 93;
            const int P_2C = 94;

            for (int i = 0; i < size; i++)
            {
                string instru = csv[i][0];
                if (instru.Equals("LTQ") || instru.Equals("LTQ-ETD") || instru.Equals("LTQ-Prep") || instru.Equals("VelosPro"))
                {
                    if (csv[i][XIC_WideFrac] == "NA" || csv[i][MS2_Density_Q1] == "NA" || csv[i][P_2C] == "NA")
                    {
                        Console.WriteLine(csv[i][Dataset] + " Has a missing value");
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
                    if (csv[i][MS1_TIC_Q2] == "NA" || csv[i][MS1_Density_Q1] == "NA")
                    {
                        Console.WriteLine(csv[i][Dataset] + " Has a missing value");
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
                    if (csv[i][XIC_WideFrac] == "NA" || csv[i][MS1_TIC_Change_Q2] == "NA" || csv[i][MS1_TIC_Q2] == "NA" || 
                        csv[i][MS1_Density_Q1] == "NA" || csv[i][MS1_Density_Q2] == "NA" || csv[i][DS_2A] == "NA" ||
                        csv[i][P_2B] == "NA" || csv[i][P_2A] == "NA" || csv[i][DS_2B] == "NA")
                    {
                        Console.WriteLine(csv[i][Dataset] + " Has a missing value");
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
                    if (csv[i][XIC_WideFrac] == "NA" || csv[i][MS2_Density_Q1] == "NA" || csv[i][MS1_2B] == "NA" ||
                        csv[i][MS1_Density_Q1] == "NA" || csv[i][P_2B] == "NA" || csv[i][P_2A] == "NA" || csv[i][DS_2B] == "NA")
                    {
                        Console.WriteLine(csv[i][Dataset] + " Has a missing value");
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
            File.WriteAllText(fileloc + "data.csv", sb.ToString());
        }

        //Gets the location of where the R is installed
        public static string GetRPathFromWindowsRegistry()
        {
            const string RCORE_SUBKEY = @"SOFTWARE\R-core";

            Microsoft.Win32.RegistryKey regRCore = Registry.LocalMachine.OpenSubKey(RCORE_SUBKEY);
            if (regRCore == null)
            {
                throw new System.ApplicationException("Registry key is not found: " + RCORE_SUBKEY);
            }
            bool is64Bit = Environment.Is64BitProcess;
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
