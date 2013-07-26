using System;
using System.Collections.Generic;
using System.IO;
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
		/// Appends the metrics for the given dataset to the stringbuilder
		/// </summary>
		/// <param name="instrumentGroup"></param>
		/// <param name="metricsOneDataset"></param>
		/// <param name="dctRequiredValues"></param>
		/// <param name="sb"></param>
		/// <returns>True if hte metrics are valid, otherwise false</returns>
		protected bool AppendToCsv(string instrumentGroup, List<string> metricsOneDataset, Dictionary<int, string> dctRequiredValues, bool showWarning, ref StringBuilder sb)
		{
			int datasetID = LLRCWrapper.GetDatasetIdForMetricRow(metricsOneDataset);

			if (RowIsMissingValues(datasetID, metricsOneDataset, dctRequiredValues, showWarning))
			{
				return false;
			}
			else
			{
				sb.Append(instrumentGroup + ",");

				// Append the remaining values
				for (int j = 1; j < metricsOneDataset.Count; j++)
				{
					sb.Append(metricsOneDataset[j] + ",");
				}

				// Append the current year
				sb.AppendLine(DateTime.Now.Year.ToString());

				return true;
			}
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
		/// Deletes old files so they dont interfere with new ones
        /// </summary>
        /// <param name="fileloc"></param>
		public void DeleteFiles(string workingDirPath)
        {
			List<string> lstFilesToDelete = CreateList("TestingDataset.csv", "data.csv");
			
			foreach (string fileName in lstFilesToDelete)
			{
				FileInfo fiFile = new FileInfo(Path.Combine(workingDirPath, fileName));
				if (fiFile.Exists)
					fiFile.Delete();
            }

        }

		/// <summary>
		/// Examines the data in row to determine if any of the columns in dctRequiredValues is not numeric
		/// </summary>
		/// <param name="datasetName"></param>
		/// <param name="row"></param>
		/// <param name="dctRequiredValues"></param>
		/// <returns>True if one or more columns is missing a value, false if no problems</returns>
		protected bool RowIsMissingValues(int datasetID, List<string> row, Dictionary<int, string> dctRequiredValues, bool showWarning)
		{
			foreach (var item in dctRequiredValues)
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
        /// <param name="fileloc"></param>
		public void WriteRFile(string workingDirPath)
        {
			string folderPathUnix = workingDirPath.Replace(@"\", "/");
			if (!folderPathUnix.EndsWith("/"))
				folderPathUnix += '/';

			string contents = "require(QCDM)" + "\n" +
			"outDataName <- " + '"' + folderPathUnix + LLRCWrapper.RDATA_FILE_ALLDATA + '"' + "\n" +
            "outputFolder <- " + '"' + folderPathUnix + '"' + "\n" +
			"ncdataFilename <- " + '"' + folderPathUnix + "data.csv" + '"' + "\n" +
			"noncuratedPrediction(ncdataFilename=ncdataFilename, modelsFile=paste(outputFolder,\"" + LLRCWrapper.RDATA_FILE_MODELS + "\",sep=\"\"), dataFilename=outDataName,outputFolder=outputFolder)";

			File.WriteAllText(Path.Combine(workingDirPath, LLR_SCRIPT_NAME), contents);
        }

        /// <summary>
		/// Writes the batch file to run the .R file
        /// </summary>
        /// <param name="fileloc"></param>
        public void WriteBatch(string fileloc)
        {
			string contents = '"' + mRProgramPath + '"' + " CMD BATCH --vanilla --slave " + '"' + Path.Combine(fileloc, LLR_SCRIPT_NAME) + '"';
            File.WriteAllText(Path.Combine(fileloc, "RunR.bat"), contents);
        }

        /// <summary>
		/// Writes the data from the database into a .csv file to be used in the R program
        /// </summary>
        /// <param name="lstMetricsByDataset"></param>
        /// <param name="size"></param>
        /// <param name="fileloc"></param>
		/// <returns>The list of valid dataset IDs</returns>
		public SortedSet<int> WriteCsv(List<List<string>> lstMetricsByDataset, string workingDirPath)
        {

			StringBuilder sb = new StringBuilder();
			sb.AppendLine(HEADER_LINE);

			SortedSet<int> lstValidDatasetIDs = new SortedSet<int>();
			bool showWarning = true;
			int warnCount = 0;

			foreach (List<string> metricsOneDataset in lstMetricsByDataset)
			{
		
                //Checks the instrument Catagory and checks to make sure the appropriate columns are present to calculate the results
                //If a column is missing will return has misssing value else will add the values to the string builder
				string instrumentGroup = metricsOneDataset[DatabaseMang.MetricColumnIndex.InstrumentGroup];
				int datasetID = LLRCWrapper.GetDatasetIdForMetricRow(metricsOneDataset);
				bool validInstrumentGroup = false;

				if (datasetID <= 0)
				{
					continue;
				}

				Dictionary<int, string> dctRequiredValues = new Dictionary<int, string>();

				bool validMetrics = false;

                if (instrumentGroup.Equals("LTQ") || instrumentGroup.Equals("LTQ-ETD") || instrumentGroup.Equals("LTQ-Prep") || instrumentGroup.Equals("VelosPro"))
                {
					validInstrumentGroup = true;
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_Density_Q1, "MS2_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2C, "P_2C");

					validMetrics = AppendToCsv("LTQ_IonTrap", metricsOneDataset, dctRequiredValues, showWarning, ref sb);
                }

                if (instrumentGroup.Equals("Exactive"))
                {
					validInstrumentGroup = true;
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Q2, "MS1_TIC_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");

					validMetrics = AppendToCsv("Exactive", metricsOneDataset, dctRequiredValues, showWarning, ref sb);
                }

                if (instrumentGroup.Equals("LTQ-FT") || instrumentGroup.Equals("Orbitrap") || instrumentGroup.Equals("QExactive"))
                {
					validInstrumentGroup = true;
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Change_Q2, "MS1_TIC_Change_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Q2, "MS1_TIC_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q2, "MS1_Density_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2A, "DS_2A");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2B, "P_2B");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2A, "P_2A");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2B, "DS_2B");

					validMetrics = AppendToCsv("Orbitrap", metricsOneDataset, dctRequiredValues, showWarning, ref sb);
                }

                if (instrumentGroup.Equals("VelosOrbi"))
                {
					validInstrumentGroup = true;
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_Density_Q1, "MS2_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_2B, "MS1_2B");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2B, "P_2B");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2A, "P_2A");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2B, "DS_2B");

					validMetrics = AppendToCsv("VOrbitrap", metricsOneDataset, dctRequiredValues, showWarning, ref sb);
                }

				if (validMetrics)
				{
					lstValidDatasetIDs.Add(datasetID);
				}
				else
				{
					if (!validInstrumentGroup)
						Console.WriteLine("Unsupported instrument group \"" + instrumentGroup + "\" for DatasetID " + datasetID);
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
				Console.WriteLine(" ... " + (warnCount - 10).ToString() + " additional warnings not shown");
			}

			return lstValidDatasetIDs;
        }

        /// <summary>
		/// Gets the location of where the R is installed
        /// </summary>
        /// <returns></returns>
        public string GetRPathFromWindowsRegistry()
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
