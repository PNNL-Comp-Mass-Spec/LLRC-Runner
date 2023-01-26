using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PRISM;

namespace LLRC
{
    internal class WriteFiles : EventNotifier
    {
        public const string LLR_SCRIPT_NAME = "QCDMscript.r";

        /// <summary>
        /// Keys in this dictionary are metric names, values are the column index in the .csv file created by this class
        /// </summary>
        public Dictionary<DatabaseManager.MetricColumns, int> ColumnIndexMap { get; }

        /// <summary>
        /// This property tracks any errors related to finding the directory with R.exe
        /// </summary>
        public string ErrorMessage { get; }

        public string RProgramPath { get; }

        private const string HEADER_LINE = "Instrument_Category, Dataset_ID, Instrument, Dataset, XIC_WideFrac, MS1_TIC_Change_Q2, MS1_TIC_Q2, MS1_Density_Q1, MS1_Density_Q2, MS2_Density_Q1, DS_2A, DS_2B, MS1_2B, P_2A, P_2B, P_2C, SMAQC_Job, Quameter_Job, XIC_FWHM_Q1, XIC_FWHM_Q2, XIC_FWHM_Q3, XIC_Height_Q2, XIC_Height_Q3, XIC_Height_Q4, RT_Duration, RT_TIC_Q1, RT_TIC_Q2, RT_TIC_Q3, RT_TIC_Q4, RT_MS_Q1, RT_MS_Q2, RT_MS_Q3, RT_MS_Q4, RT_MSMS_Q1, RT_MSMS_Q2, RT_MSMS_Q3, RT_MSMS_Q4, MS1_TIC_Change_Q3, MS1_TIC_Change_Q4, MS1_TIC_Q3, MS1_TIC_Q4, MS1_Count, MS1_Freq_Max, MS1_Density_Q3, MS2_Count, MS2_Freq_Max, MS2_Density_Q2, MS2_Density_Q3, MS2_PrecZ_1, MS2_PrecZ_2, MS2_PrecZ_3, MS2_PrecZ_4, MS2_PrecZ_5, MS2_PrecZ_more, MS2_PrecZ_likely_1, MS2_PrecZ_likely_multi, Quameter_Last_Affected, C_1A, C_1B, C_2A, C_2B, C_3A, C_3B, C_4A, C_4B, C_4C, DS_1A, DS_1B, DS_3A, DS_3B, IS_1A, IS_1B, IS_2, IS_3A, IS_3B, IS_3C, MS1_1, MS1_2A, MS1_3A, MS1_3B, MS1_5A, MS1_5B, MS1_5C, MS1_5D, MS2_1, MS2_2, MS2_3, MS2_4A, MS2_4B, MS2_4C, MS2_4D, P_1A, P_1B, P_3, Smaqc_Last_Affected, PSM_Source_Job, year";

        /// <summary>
        /// Constructor
        /// </summary>
        public WriteFiles()
        {
            RProgramPath = GetRPathFromWindowsRegistry(out var errorMessage);
            ErrorMessage = errorMessage;

            ColumnIndexMap = new Dictionary<DatabaseManager.MetricColumns, int>
            {
                { DatabaseManager.MetricColumns.Instrument_Group, 0 },
                { DatabaseManager.MetricColumns.Dataset_ID, 1 },
                { DatabaseManager.MetricColumns.Instrument, 2 },
                { DatabaseManager.MetricColumns.Dataset, 3 },
                { DatabaseManager.MetricColumns.XIC_Wide_Frac, 4 },
                { DatabaseManager.MetricColumns.MS1_TIC_Change_Q2, 5 },
                { DatabaseManager.MetricColumns.MS1_TIC_Q2, 6 },
                { DatabaseManager.MetricColumns.MS1_Density_Q1, 7 },
                { DatabaseManager.MetricColumns.MS1_Density_Q2, 8 },
                { DatabaseManager.MetricColumns.MS2_Density_Q1, 9 },
                { DatabaseManager.MetricColumns.DS_2A, 10 },
                { DatabaseManager.MetricColumns.DS_2B, 11 },
                { DatabaseManager.MetricColumns.MS1_2B, 12 },
                { DatabaseManager.MetricColumns.P_2A, 13 },
                { DatabaseManager.MetricColumns.P_2B, 14 },
                { DatabaseManager.MetricColumns.P_2C, 15 },
                { DatabaseManager.MetricColumns.SMAQC_Job, 16 },
                { DatabaseManager.MetricColumns.Quameter_Job, 17 },
                { DatabaseManager.MetricColumns.XIC_FWHM_Q1, 18 },
                { DatabaseManager.MetricColumns.XIC_FWHM_Q2, 19 },
                { DatabaseManager.MetricColumns.XIC_FWHM_Q3, 20 },
                { DatabaseManager.MetricColumns.XIC_Height_Q2, 21 },
                { DatabaseManager.MetricColumns.XIC_Height_Q3, 22 },
                { DatabaseManager.MetricColumns.XIC_Height_Q4, 23 },
                { DatabaseManager.MetricColumns.RT_Duration, 24 },
                { DatabaseManager.MetricColumns.RT_TIC_Q1, 25 },
                { DatabaseManager.MetricColumns.RT_TIC_Q2, 26 },
                { DatabaseManager.MetricColumns.RT_TIC_Q3, 27 },
                { DatabaseManager.MetricColumns.RT_TIC_Q4, 28 },
                { DatabaseManager.MetricColumns.RT_MS_Q1, 29 },
                { DatabaseManager.MetricColumns.RT_MS_Q2, 30 },
                { DatabaseManager.MetricColumns.RT_MS_Q3, 31 },
                { DatabaseManager.MetricColumns.RT_MS_Q4, 32 },
                { DatabaseManager.MetricColumns.RT_MSMS_Q1, 33 },
                { DatabaseManager.MetricColumns.RT_MSMS_Q2, 34 },
                { DatabaseManager.MetricColumns.RT_MSMS_Q3, 35 },
                { DatabaseManager.MetricColumns.RT_MSMS_Q4, 36 },
                { DatabaseManager.MetricColumns.MS1_TIC_Change_Q3, 37 },
                { DatabaseManager.MetricColumns.MS1_TIC_Change_Q4, 38 },
                { DatabaseManager.MetricColumns.MS1_TIC_Q3, 39 },
                { DatabaseManager.MetricColumns.MS1_TIC_Q4, 40 },
                { DatabaseManager.MetricColumns.MS1_Count, 41 },
                { DatabaseManager.MetricColumns.MS1_Freq_Max, 42 },
                { DatabaseManager.MetricColumns.MS1_Density_Q3, 43 },
                { DatabaseManager.MetricColumns.MS2_Count, 44 },
                { DatabaseManager.MetricColumns.MS2_Freq_Max, 45 },
                { DatabaseManager.MetricColumns.MS2_Density_Q2, 46 },
                { DatabaseManager.MetricColumns.MS2_Density_Q3, 47 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_1, 48 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_2, 49 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_3, 50 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_4, 51 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_5, 52 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_more, 53 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_likely_1, 54 },
                { DatabaseManager.MetricColumns.MS2_Prec_Z_likely_multi, 55 },
                { DatabaseManager.MetricColumns.Quameter_Last_Affected, 56 },
                { DatabaseManager.MetricColumns.C_1A, 57 },
                { DatabaseManager.MetricColumns.C_1B, 58 },
                { DatabaseManager.MetricColumns.C_2A, 59 },
                { DatabaseManager.MetricColumns.C_2B, 60 },
                { DatabaseManager.MetricColumns.C_3A, 61 },
                { DatabaseManager.MetricColumns.C_3B, 62 },
                { DatabaseManager.MetricColumns.C_4A, 63 },
                { DatabaseManager.MetricColumns.C_4B, 64 },
                { DatabaseManager.MetricColumns.C_4C, 65 },
                { DatabaseManager.MetricColumns.DS_1A, 66 },
                { DatabaseManager.MetricColumns.DS_1B, 67 },
                { DatabaseManager.MetricColumns.DS_3A, 68 },
                { DatabaseManager.MetricColumns.DS_3B, 69 },
                { DatabaseManager.MetricColumns.IS_1A, 70 },
                { DatabaseManager.MetricColumns.IS_1B, 71 },
                { DatabaseManager.MetricColumns.IS_2, 72 },
                { DatabaseManager.MetricColumns.IS_3A, 73 },
                { DatabaseManager.MetricColumns.IS_3B, 74 },
                { DatabaseManager.MetricColumns.IS_3C, 75 },
                { DatabaseManager.MetricColumns.MS1_1, 76 },
                { DatabaseManager.MetricColumns.MS1_2A, 77 },
                { DatabaseManager.MetricColumns.MS1_3A, 78 },
                { DatabaseManager.MetricColumns.MS1_3B, 79 },
                { DatabaseManager.MetricColumns.MS1_5A, 80 },
                { DatabaseManager.MetricColumns.MS1_5B, 81 },
                { DatabaseManager.MetricColumns.MS1_5C, 82 },
                { DatabaseManager.MetricColumns.MS1_5D, 83 },
                { DatabaseManager.MetricColumns.MS2_1, 84 },
                { DatabaseManager.MetricColumns.MS2_2, 85 },
                { DatabaseManager.MetricColumns.MS2_3, 86 },
                { DatabaseManager.MetricColumns.MS2_4A, 87 },
                { DatabaseManager.MetricColumns.MS2_4B, 88 },
                { DatabaseManager.MetricColumns.MS2_4C, 89 },
                { DatabaseManager.MetricColumns.MS2_4D, 90 },
                { DatabaseManager.MetricColumns.P_1A, 91 },
                { DatabaseManager.MetricColumns.P_1B, 92 },
                { DatabaseManager.MetricColumns.P_3, 93 },
                { DatabaseManager.MetricColumns.Smaqc_Last_Affected, 94 },
                { DatabaseManager.MetricColumns.PSM_Source_Job, 95 }
            };
        }

        /// <summary>
        /// Appends the metrics for the given dataset to the string builder
        /// </summary>
        /// <param name="datasetID"></param>
        /// <param name="instrumentGroup"></param>
        /// <param name="metricsOneDataset"></param>
        /// <param name="requiredValues"></param>
        /// <param name="showWarning"></param>
        /// <param name="sb"></param>
        /// <returns>True if the metrics are valid, otherwise false</returns>
        private bool AppendToCsv(int datasetID, string instrumentGroup, Dictionary<DatabaseManager.MetricColumns, string> metricsOneDataset, SortedSet<DatabaseManager.MetricColumns> requiredValues, bool showWarning, StringBuilder sb)
        {
            if (RowIsMissingValues(datasetID, metricsOneDataset, requiredValues, showWarning))
            {
                return false;
            }

            sb.AppendFormat("{0},", instrumentGroup);

            // Append the metrics, skipping the first metric (instrument group)

            var sortedKeys = (from item in metricsOneDataset.Keys orderby (int)item select item).ToList();

            if (sortedKeys[0] != DatabaseManager.MetricColumns.Instrument_Group)
                throw new Exception("The first metric is not Instrument_Group; cannot write to the .csv file");

            // Column order should match the HEADER_LINE constant:
            // Instrument_Category, Dataset_ID, Instrument, Dataset, XIC_WideFrac, etc.

            var columnIndex = 1;

            foreach (var item in sortedKeys.Skip(1))
            {
                var expectedIndex = ColumnIndexMap[item];

                if (columnIndex != expectedIndex)
                {
                    throw new Exception(string.Format(
                        "Column order in the LINQ query does not match the expected order: metric {0} should be at column index {1} but is instead at {2}",
                        Enum.GetName(typeof(DatabaseManager.MetricColumns), item), expectedIndex, columnIndex));
                }

                sb.AppendFormat("{0},", metricsOneDataset[item]);
                columnIndex++;
            }

            // Append the current year
            sb.Append(DateTime.Now.Year.ToString());

            // Append a newline character
            sb.AppendLine();

            return true;
        }

        /// <summary>
        /// Deletes old files so they don't interfere with new ones
        /// </summary>
        /// <param name="workingDirPath"></param>
        public void DeleteFiles(string workingDirPath)
        {
            var filesToDelete = new List<string>
            {
                "TestingDataset.csv",
                "data.csv"
            };

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
        private bool RowIsMissingValues(
            int datasetID,
            IReadOnlyDictionary<DatabaseManager.MetricColumns, string> row,
            SortedSet<DatabaseManager.MetricColumns> requiredValues,
            bool showWarning)
        {
            foreach (var item in requiredValues)
            {
                if (!row.TryGetValue(item, out var metricValue))
                {
                    if (showWarning)
                        OnWarningEvent("Warning: Dataset " + datasetID + " is missing metric " + Enum.GetName(typeof(DatabaseManager.MetricColumns), item));

                    return true;
                }

                if (string.IsNullOrWhiteSpace(metricValue) || metricValue.Equals("NA", StringComparison.OrdinalIgnoreCase))
                {
                    if (showWarning)
                        OnWarningEvent("Warning: Dataset " + datasetID + " has a missing value for column " + Enum.GetName(typeof(DatabaseManager.MetricColumns), item));

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
            var contents = '"' + RProgramPath + '"' + " CMD BATCH --vanilla --slave " + '"' + Path.Combine(directoryPath, LLR_SCRIPT_NAME) + '"';
            File.WriteAllText(Path.Combine(directoryPath, "RunR.bat"), contents);
        }

        /// <summary>
        /// Writes the data from the database into a .csv file to be used in the R program
        /// </summary>
        /// <param name="metricsByDataset"></param>
        /// <param name="workingDirPath"></param>
        /// <returns>The list of valid dataset IDs</returns>
        public SortedSet<int> WriteCsv(Dictionary<int, Dictionary<DatabaseManager.MetricColumns, string>> metricsByDataset, string workingDirPath)
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

                var instrumentGroup = metricsOneDataset.Value[DatabaseManager.MetricColumns.Instrument_Group];
                var datasetID = metricsOneDataset.Key;
                var validInstrumentGroup = false;

                if (datasetID <= 0)
                {
                    continue;
                }

                var requiredValues = new SortedSet<DatabaseManager.MetricColumns>();

                var validMetrics = false;

                if (instrumentGroup.Equals("LTQ", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("LTQ-ETD", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("LTQ-Prep", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("VelosPro", StringComparison.OrdinalIgnoreCase))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseManager.MetricColumns.XIC_Wide_Frac);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS2_Density_Q1);
                    requiredValues.Add(DatabaseManager.MetricColumns.P_2C);

                    validMetrics = AppendToCsv(datasetID, "LTQ_IonTrap", metricsOneDataset.Value, requiredValues, showWarning, sb);
                }

                if (instrumentGroup.Equals("Exactive", StringComparison.OrdinalIgnoreCase))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseManager.MetricColumns.MS1_TIC_Q2);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS1_Density_Q1);

                    validMetrics = AppendToCsv(datasetID, "Exactive", metricsOneDataset.Value, requiredValues, showWarning, sb);
                }

                if (instrumentGroup.Equals("LTQ-FT", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("Orbitrap", StringComparison.OrdinalIgnoreCase))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseManager.MetricColumns.XIC_Wide_Frac);
                    requiredValues.Add(DatabaseManager.MetricColumns.XIC_Height_Q4);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS1_TIC_Change_Q2);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS1_Density_Q2);
                    requiredValues.Add(DatabaseManager.MetricColumns.DS_1A);
                    requiredValues.Add(DatabaseManager.MetricColumns.DS_2A);
                    requiredValues.Add(DatabaseManager.MetricColumns.IS_1A);
                    requiredValues.Add(DatabaseManager.MetricColumns.IS_3A);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS2_1);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS2_4A);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS2_4B);
                    requiredValues.Add(DatabaseManager.MetricColumns.P_2B);

                    validMetrics = AppendToCsv(datasetID, "Orbitrap", metricsOneDataset.Value, requiredValues, showWarning, sb);
                }

                if (instrumentGroup.Equals("VelosOrbi", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("QExactive", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("Lumos", StringComparison.OrdinalIgnoreCase) ||
                    instrumentGroup.Equals("QEHFX", StringComparison.OrdinalIgnoreCase))
                {
                    validInstrumentGroup = true;
                    requiredValues.Add(DatabaseManager.MetricColumns.XIC_Wide_Frac);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS2_4A);
                    requiredValues.Add(DatabaseManager.MetricColumns.MS2_4B);
                    requiredValues.Add(DatabaseManager.MetricColumns.P_2B);

                    validMetrics = AppendToCsv(datasetID, "VOrbitrap", metricsOneDataset.Value, requiredValues, showWarning, sb);
                }

                if (validMetrics)
                {
                    validDatasetIDs.Add(datasetID);
                }
                else
                {
                    if (!validInstrumentGroup)
                    {
                        instrumentWarnCount++;
                        if (instrumentWarnCount <= 10)
                            OnWarningEvent("Unsupported instrument group \"" + instrumentGroup + "\" for DatasetID " + datasetID);
                    }
                    else
                    {
                        warnCount++;
                        if (warnCount == 10)
                            showWarning = false;
                    }
                }
            }

            File.WriteAllText(Path.Combine(workingDirPath, "data.csv"), sb.ToString());

            if (warnCount > 10)
            {
                OnWarningEvent(" ... " + (warnCount - 10) + " additional missing value warnings not shown");
            }

            if (instrumentWarnCount > 10)
            {
                OnWarningEvent(" ... " + (instrumentWarnCount - 10) + " additional instrument warnings not shown");
            }

            return validDatasetIDs;
        }

        /// <summary>
        /// Gets the location of where the R is installed
        /// </summary>
        /// <param name="errorMessage">Output: error message if an error, otherwise an empty string</param>
        /// <returns>Full path to R.exe</returns>
        public string GetRPathFromWindowsRegistry(out string errorMessage)
        {
            var directoryPath = PRISMWin.RegistryUtils.GetRPathFromWindowsRegistry(out var prismWinErrorMessage);

            if (!string.IsNullOrWhiteSpace(prismWinErrorMessage))
            {
                errorMessage = prismWinErrorMessage;
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                errorMessage = "GetRPathFromWindowsRegistry returned an empty string (and an empty error message)";
                return string.Empty;
            }

            var rExecutable = new FileInfo(Path.Combine(directoryPath, "R.exe"));

            if (rExecutable.Exists)
            {
                errorMessage = string.Empty;
                return rExecutable.FullName;
            }

            if (rExecutable.Directory?.Exists == true)
            {
                errorMessage = "R program directory exists, but the R executable is missing: " + rExecutable.FullName;
                return string.Empty;
            }

            errorMessage = "R program directory does not exist; cannot find " + rExecutable.FullName;
            return string.Empty;
        }
    }
}
