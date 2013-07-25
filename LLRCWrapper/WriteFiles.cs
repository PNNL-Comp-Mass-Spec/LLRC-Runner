using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace QCDMWrapper
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
		public void DeleteFiles(string outputFolderPath)
        {
			List<string> lstFilesToDelete = CreateList("TestingDataset.csv", "data.csv");
			
			foreach (string fileName in lstFilesToDelete)
			{
				FileInfo fiFile = new FileInfo(Path.Combine(outputFolderPath, fileName));
				if (fiFile.Exists)
					fiFile.Delete();
            }

        }

		protected bool RowIsMissingValues(string datasetName, List<string> row, Dictionary<int, string> dctRequiredValues)
		{
			foreach (var item in dctRequiredValues)
			{
				if (string.IsNullOrWhiteSpace(row[item.Key]) || row[item.Key] == "NA")
				{
					Console.WriteLine("Dataset " + datasetName + " has a missing value for column " + item.Value);
					return true;
				}
			}

			return false;
		}

        /// <summary>
		/// Writes the .R file to run the formula
        /// </summary>
        /// <param name="fileloc"></param>
		public void WriteRFile(string outputFolderPath)
        {
			string folderPathUnix = outputFolderPath.Replace(@"\", "/");
			if (!folderPathUnix.EndsWith("/"))
				folderPathUnix += '/';

			string contents = "require(QCDM)" + "\n" +
            "outDataName <- " + '"' + folderPathUnix + "allData_v3.Rdata" + '"' + "\n" +
            "outputFolder <- " + '"' + folderPathUnix + '"' + "\n" +
			"ncdataFilename <- " + '"' + folderPathUnix + "data.csv" + '"' + "\n" +
            "noncuratedPrediction(ncdataFilename=ncdataFilename, modelsFile=paste(outputFolder,\"Models_paper.Rdata\",sep=\"\"), dataFilename=outDataName,outputFolder=outputFolder)";

			File.WriteAllText(Path.Combine(outputFolderPath, LLR_SCRIPT_NAME), contents);
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
        /// <param name="metricCount"></param>
		public void WriteCsv(List<List<string>> lstMetricsByDataset, string outputFolderPath, int metricCount)
        {

			StringBuilder sb = new StringBuilder();
			sb.AppendLine(HEADER_LINE);

			foreach (List<string> metricsOneDataset in lstMetricsByDataset)
			{
		
                //Checks the instrument Catagory and checks to make sure the appropriate columns are present to calculate the results
                //If a column is missing will return has misssing value else will add the values to the string builder
				string instrumentGroup = metricsOneDataset[DatabaseMang.MetricColumnIndex.InstrumentGroup];
				Dictionary<int, string> dctRequiredValues = new Dictionary<int, string>();

                if (instrumentGroup.Equals("LTQ") || instrumentGroup.Equals("LTQ-ETD") || instrumentGroup.Equals("LTQ-Prep") || instrumentGroup.Equals("VelosPro"))
                {
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_Density_Q1, "MS2_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2C, "P_2C");

					if (!RowIsMissingValues(metricsOneDataset[DatabaseMang.MetricColumnIndex.DatasetID], metricsOneDataset, dctRequiredValues))
					{
						sb.Append("LTQ_IonTrap,");
						// Append the remaining values
						for (int j = 1; j < metricCount; j++)
						{
							sb.Append(metricsOneDataset[j] + ",");
						}
					}
                  
                }

                if (instrumentGroup.Equals("Exactive") || instrumentGroup.Equals("QExactive"))
                {
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Q2, "MS1_TIC_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");

					if (!RowIsMissingValues(metricsOneDataset[DatabaseMang.MetricColumnIndex.DatasetID], metricsOneDataset, dctRequiredValues))
					{
						sb.Append("Exactive,");
						// Append the remaining values
						for (int j = 1; j < metricCount; j++)
						{
							sb.Append(metricsOneDataset[j] + ",");
						}
					}

                }

                if (instrumentGroup.Equals("LTQ-FT") || instrumentGroup.Equals("Orbitrap"))
                {
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Change_Q2, "MS1_TIC_Change_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_TIC_Q2, "MS1_TIC_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q2, "MS1_Density_Q2");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2A, "DS_2A");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2B, "P_2B");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2A, "P_2A");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2B, "DS_2B");

					if (!RowIsMissingValues(metricsOneDataset[DatabaseMang.MetricColumnIndex.DatasetID], metricsOneDataset, dctRequiredValues))
					{
						sb.Append("Orbitrap,");
						// Append the remaining values
						for (int j = 1; j < metricCount; j++)
						{
							sb.Append(metricsOneDataset[j] + ",");
						}
					}
               
                }

                if (instrumentGroup.Equals("VelosOrbi"))
                {
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.XIC_WideFrac, "XIC_WideFrac");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS2_Density_Q1, "MS2_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_2B, "MS1_2B");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.MS1_Density_Q1, "MS1_Density_Q1");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2B, "P_2B");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.P_2A, "P_2A");
					dctRequiredValues.Add(DatabaseMang.MetricColumnIndex.DS_2B, "DS_2B");

					if (!RowIsMissingValues(metricsOneDataset[DatabaseMang.MetricColumnIndex.DatasetID], metricsOneDataset, dctRequiredValues))
					{
						sb.Append("VOrbitrap,");
						// Append the remaining values
						for (int j = 1; j < metricCount; j++)
						{
							sb.Append(metricsOneDataset[j] + ",");
						}
					}              
                }

				// Append the current year
                sb.AppendLine(System.DateTime.Now.Year.ToString());
            }

			File.WriteAllText(Path.Combine(outputFolderPath, "data.csv"), sb.ToString());
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
