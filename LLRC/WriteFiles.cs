using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Win32;

namespace LLRC
{
    class WriteFiles
    {
        public const string LLR_SCRIPT_NAME = "QCDMscript.r";
        public string mRProgramPath;

        protected const string HEADER_LINE = "Instrument_Category, Dataset_ID, Instrument, Dataset, XIC_WideFrac, MS1_TIC_Change_Q2, MS1_TIC_Q2, MS1_Density_Q1, MS1_Density_Q2, MS2_Density_Q1, DS_2A, DS_2B, MS1_2B, P_2A, P_2B, P_2C, SMAQC_Job, Quameter_Job, XIC_FWHM_Q1, XIC_FWHM_Q2, XIC_FWHM_Q3, XIC_Height_Q2, XIC_Height_Q3, XIC_Height_Q4, RT_Duration, RT_TIC_Q1, RT_TIC_Q2, RT_TIC_Q3, RT_TIC_Q4, RT_MS_Q1, RT_MS_Q2, RT_MS_Q3, RT_MS_Q4, RT_MSMS_Q1, RT_MSMS_Q2, RT_MSMS_Q3, RT_MSMS_Q4, MS1_TIC_Change_Q3, MS1_TIC_Change_Q4, MS1_TIC_Q3, MS1_TIC_Q4, MS1_Count, MS1_Freq_Max, MS1_Density_Q3, MS2_Count, MS2_Freq_Max, MS2_Density_Q2, MS2_Density_Q3, MS2_PrecZ_1, MS2_PrecZ_2, MS2_PrecZ_3, MS2_PrecZ_4, MS2_PrecZ_5, MS2_PrecZ_more, MS2_PrecZ_likely_1, MS2_PrecZ_likely_multi, Quameter_Last_Affected, C_1A, C_1B, C_2A, C_2B, C_3A, C_3B, C_4A, C_4B, C_4C, DS_1A, DS_1B, DS_3A, DS_3B, IS_1A, IS_1B, IS_2, IS_3A, IS_3B, IS_3C, MS1_1, MS1_2A, MS1_3A, MS1_3B, MS1_5A, MS1_5B, MS1_5C, MS1_5D, MS2_1, MS2_2, MS2_3, MS2_4A, MS2_4B, MS2_4C, MS2_4D, P_1A, P_1B, P_3, Smaqc_Last_Affected, PSM_Source_Job, year";

        /// <summary>
        /// Constructor
        /// </summary>
        public WriteFiles()
        {
            mRProgramPath = GetRPathFromWindowsRegistry();
        }

        /// <summary>
        /// Appends the metrics for the given dataset to the string builder
        /// </summary>
        /// <param name="instrumentGroup"></param>
        /// <param name="metricsOneDataset"></param>
        /// <param name="requiredValues"></param>
        /// <param name="showWarning"></param>
        /// <param name="sb"></param>
        protected bool AppendToCsv(string instrumentGroup, List<string> metricsOneDataset, Dictionary<int, string> requiredValues, bool showWarning, ref StringBuilder sb)
        /// <returns>True if the metrics are valid, otherwise false</returns>
        {
            var datasetID = LLRCWrapper.GetDatasetIdForMetricRow(metricsOneDataset);

            if (RowIsMissingValues(datasetID, metricsOneDataset, requiredValues, showWarning))
            {
                return false;
            }

            sb.Append(instrumentGroup + ",");

            // Append the remaining values
            for (var j = 1; j < metricsOneDataset.Count; j++)
            {
                sb.Append(metricsOneDataset[j] + ",");
            }

            // Append the current year
            sb.AppendLine(DateTime.Now.Year.ToString());

            return true;
        }


        /// <summary>
        /// Factory method to create a generic list given several values
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <returns></returns>
        protected List<T> CreateList<T>(params T[] values)
        {
            return new List<T>(values);
        }

        /// <summary>
        /// Deletes old files so they don't interfere with new ones
        /// </summary>
        /// <param name="workingDirPath"></param>
        public void DeleteFiles(string workingDirPath)
        {
            var filesToDelete = CreateList("TestingDataset.csv", "data.csv");

            foreach (var fileName in filesToDelete)
            {
                var targetFile = new FileInfo(Path.Combine(workingDirPath, fileName));
                if (targetFile.Exists)
                    targetFile.Delete();
            }
        }

        /// <summary>
        /// Examines the data in row to determine if any of the columns in requiredValues is not numeric
        /// </summary>
        /// <param name="datasetID"></param>
        /// <param name="row"></param>
        /// <param name="requiredValues"></param>
        /// <param name="showWarning"></param>
        /// <returns>True if one or more columns is missing a value, false if no problems</returns>
        protected bool RowIsMissingValues(int datasetID, List<string> row, Dictionary<int, string> requiredValues, bool showWarning)
        {
            foreach (var item in requiredValues)
            {
                if (string.IsNullOrWhiteSpace(row[item.Key]) || row[item.Key] == "NA")
                {
                    if (showWarning)
                        Console.WriteLine("Warning: Dataset " + datasetID + " has a missing value for column " + item.Value);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Writes the .R file to run the formula
        /// </summary>
        /// <param name="workingDirPath"></param>
        public void WriteRFile(string workingDirPath)
        {
            var folderPathUnix = workingDirPath.Replace(@"\", "/");
            if (!folderPathUnix.EndsWith("/"))
                folderPathUnix += '/';

            var contents = "require(QCDM)" + "\n" +
            "outDataName <- " + '"' + folderPathUnix + LLRCWrapper.RDATA_FILE_ALLDATA + '"' + "\n" +
            "outputFolder <- " + '"' + folderPathUnix + '"' + "\n" +
            "ncdataFilename <- " + '"' + folderPathUnix + "data.csv" + '"' + "\n" +
            "noncuratedPrediction(ncdataFilename=ncdataFilename, modelsFile=paste(outputFolder,\"" + LLRCWrapper.RDATA_FILE_MODELS + "\",sep=\"\"), dataFilename=outDataName,outputFolder=outputFolder)";

            File.WriteAllText(Path.Combine(workingDirPath, LLR_SCRIPT_NAME), contents);
        }

        /// <summary>
        /// Writes the batch file to run the .R file
        /// </summary>
        /// <param name="directoryPath"></param>
        public void WriteBatch(string directoryPath)
        {
            var contents = '"' + mRProgramPath + '"' + " CMD BATCH --vanilla --slave " + '"' + Path.Combine(directoryPath, LLR_SCRIPT_NAME) + '"';
            File.WriteAllText(Path.Combine(directoryPath, "RunR.bat"), contents);
        }

        /// <summary>
        /// Writes the data from the database into a .csv file to be used in the R program
        /// </summary>
        /// <param name="metricsByDataset"></param>
        /// <param name="workingDirPath"></param>
        /// <returns>The list of valid dataset IDs</returns>
        public SortedSet<int> WriteCsv(List<List<string>> metricsByDataset, string workingDirPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine(HEADER_LINE);

            var validDatasetIDs = new SortedSet<int>();
            var showWarning = true;
            var warnCount = 0;
            var instrumentWarnCount = 0;

            foreach (var metricsOneDataset in metricsByDataset)
            {
                // Checks the instrument Category and checks to make sure the appropriate columns are present to calculate the results
                // If a column is missing the dataset will be skipped

                var instrumentGroup = metricsOneDataset[DatabaseMang.MetricColumnIndex.InstrumentGroup];
                var datasetID = LLRCWrapper.GetDatasetIdForMetricRow(metricsOneDataset);
                var validInstrumentGroup = false;

                if (datasetID <= 0)
                {
                    continue;
                }

                var requiredValues = new Dictionary<int, string>();

                var validMetrics = false;

                if (instrumentGroup.Equals("LTQ") || instrumentGroup.Equals("LTQ-ETD") || instrumentGroup.Equals("LTQ-Prep") || instrumentGroup.Equals("VelosPro"))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_Density_Q1, "MS2_4B");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.P_2C, "P_2C");

                    validMetrics = AppendToCsv("LTQ_IonTrap", metricsOneDataset, requiredValues, showWarning, ref sb);
                }

                if (instrumentGroup.Equals("Exactive"))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Q2, "MS1_TIC_Q2");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");

                    validMetrics = AppendToCsv("Exactive", metricsOneDataset, requiredValues, showWarning, ref sb);
                }

                if (instrumentGroup.Equals("LTQ-FT") || instrumentGroup.Equals("Orbitrap"))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_Height_Q4, "XIC_Height_Q4");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Change_Q2, "MS1_TIC_Change_Q2");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q2, "MS1_Density_Q2");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.DS_1A, "DS_1A");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2A, "DS_2A");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.IS_1A, "IS_1A");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.IS_3A, "IS_3A");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_1, "MS2_1");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_4A, "MS2_4A");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_4B, "MS2_4B");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.P_2B, "P_2B");

                    validMetrics = AppendToCsv("Orbitrap", metricsOneDataset, requiredValues, showWarning, ref sb);
                }

                if (instrumentGroup.Equals("VelosOrbi") || instrumentGroup.Equals("QExactive") || instrumentGroup.Equals("Lumos") || instrumentGroup.Equals("QEHFX"))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_4A, "MS2_4A");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_4B, "MS2_4B");
                    requiredValues.Add(DatabaseMang.MetricColumnIndex.P_2B, "P_2B");

                    validMetrics = AppendToCsv("VOrbitrap", metricsOneDataset, requiredValues, showWarning, ref sb);
                }

                if (validMetrics)
                {
                    validDatasetIDs.Add(datasetID);
                }
                else
                {
                    if (!validInstrumentGroup)
                    {
                        instrumentWarnCount += 1;
                        if (instrumentWarnCount <= 10)
                            Console.WriteLine("Unsupported instrument group \"" + instrumentGroup + "\" for DatasetID " + datasetID);
                    }
                    else
                    {
                        warnCount += 1;
                        if (warnCount == 10)
                            showWarning = false;
                    }
                }
            }

            File.WriteAllText(Path.Combine(workingDirPath, "data.csv"), sb.ToString());

            if (warnCount > 10)
            {
                Console.WriteLine(" ... " + (warnCount - 10) + " additional missing value warnings not shown");
            }

            if (instrumentWarnCount > 10)
            {
                Console.WriteLine(" ... " + (instrumentWarnCount - 10) + " additional instrument warnings not shown");
            }

            return validDatasetIDs;
        }

        /// <summary>
        /// Gets the location of where the R is installed
        /// </summary>
        /// <returns>Full path to R.exe</returns>
        public string GetRPathFromWindowsRegistry()
        {
            const string R_CORE_KEY_NAME = @"SOFTWARE\R-core";

            var regRCore = Registry.LocalMachine.OpenSubKey(R_CORE_KEY_NAME);
            if (regRCore == null)
            {
                throw new ApplicationException("Registry key is not found: " + R_CORE_KEY_NAME);
            }
            var is64Bit = Environment.Is64BitProcess;
            var sRSubKey = is64Bit ? "R64" : "R";
            var regR = regRCore.OpenSubKey(sRSubKey);
            if (regR == null)
            {
                throw new ApplicationException("Registry key is not found: " + R_CORE_KEY_NAME + @"\" + sRSubKey);
            }

            var installPath = (string)regR.GetValue("InstallPath");
            var bin = Path.Combine(installPath, "bin");
            bin = Path.Combine(bin, "R.exe");

            return bin;
        }
    }
}
