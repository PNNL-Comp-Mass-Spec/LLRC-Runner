﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace LLRC
{
    class DatabaseMang
    {
		// Old: public const string DEFAULT_CONNECTION_STRING = "user id=dmsreader;password=dms4fun;server=gigasax;Trusted_Connection=yes;database=DMS5;connection timeout=30";
		// Old: public const string DEFAULT_CONNECTION_STRING = "Persist Security Info=False;Integrated Security=true;Initial Catalog=Northwind;server=gigasax"

		public const string DEFAULT_CONNECTION_STRING = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

		private SqlConnection mConnection;

		protected string mErrorMessage;

		// These column indices are dependent on the query in DatabaseMang.GetData
		public class MetricColumnIndex
		{
			public const int InstrumentGroup = 0;
			public const int DatasetID = 1;
			public const int Instrument = 2;
			public const int DatasetName = 3;
			public const int XIC_WideFrac = 4;
			public const int MS1_TIC_Change_Q2 = 5;
			public const int MS1_TIC_Q2 = 6;
			public const int MS1_Density_Q1 = 7;
			public const int MS1_Density_Q2 = 8;
			public const int MS2_Density_Q1 = 9;
			public const int DS_2A = 10;
			public const int DS_2B = 11;
			public const int MS1_2B = 12;
			public const int P_2A = 13;
			public const int P_2B = 14;
			public const int P_2C = 15;
			public const int SMAQC_Job = 16;
			public const int Quameter_Job= 17;
		}

		#region "Properties"
		public string ErrorMessge
		{
			get
			{
				return mErrorMessage;
			}
		}

		#endregion

		/// <summary>
		/// Constructor
		/// </summary>
		public DatabaseMang() : this(DEFAULT_CONNECTION_STRING)
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public DatabaseMang(string connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
				connectionString = DatabaseMang.DEFAULT_CONNECTION_STRING;			

			mConnection = new SqlConnection(connectionString);
			mErrorMessage = string.Empty;
		}

        //Connection to Database
        protected bool Open()
        {
            try
            {
                mConnection.Open();
                return true;
            }
            catch (Exception ex)
            {
				mErrorMessage = "Error opening the connection to the database: " + ex.Message;
                return false;
            }
        }

        //Closes Database
		protected bool Close()
        {
            try
            {
                mConnection.Close();
                return true;
            }
            catch (Exception ex)
			{
				mErrorMessage = "Error closing the connection to the database: " + ex.Message;
                return false;
            }
        }

		protected string GetColumnString(SqlDataReader drReader, int colIndex)
		{
			if (drReader.IsDBNull(colIndex))
			{
				return string.Empty;
			}
			else
			{
				return drReader[colIndex].ToString();
			}

		}
        /// <summary>
		/// Retrieves Data from database for the given datasetIDs
        /// </summary>
        /// <param name="datasetIDs"></param>
        /// <returns></returns>
        public List<List<string>> GetData(List<int> datasetIDs)
        {
			const int CHUNK_SIZE = 500;

			SortedSet<int> datasetIDsWithMetrics = new SortedSet<int>();
            List<List<string>> lstMetricsByDataset = new List<List<string>>();
			System.Text.StringBuilder sbDatasets = new System.Text.StringBuilder();

			// Open the database connection
			if (!Open())
				return new List<List<string>>();

			// Process the datasets in chunks, 500 datasets at a time
			for (int i = 0; i < datasetIDs.Count; i += CHUNK_SIZE)
			{

				// Construct a comma-separated list of dataset IDs
				sbDatasets.Clear();
				for (int j = i; j < i + CHUNK_SIZE && j < datasetIDs.Count; j += 1)
				{
					if (sbDatasets.Length > 0)
						sbDatasets.Append(",");

					sbDatasets.Append(datasetIDs[j]);
				}

				try
				{
					// Uses are massive SQL command to get the data to come out in the order we want it too
					// If you add/remove columns, you must update the iWriteFiles.WriteCsv
					SqlCommand command = new SqlCommand(
					 "SELECT M.[Instrument Group], M.[Dataset_ID], [Instrument], [Dataset], [XIC_WideFrac]" +
					 ", [MS1_TIC_Change_Q2], [MS1_TIC_Q2], [MS1_Density_Q1], [MS1_Density_Q2], [MS2_Density_Q1], [DS_2A], [DS_2B], [MS1_2B]" +
					 ", [P_2A], [P_2B], [P_2C], [SMAQC_Job], [Quameter_Job], [XIC_FWHM_Q1], [XIC_FWHM_Q2], [XIC_FWHM_Q3], [XIC_Height_Q2], [XIC_Height_Q3]" +
					 ", [XIC_Height_Q4], [RT_Duration], [RT_TIC_Q1], [RT_TIC_Q2], [RT_TIC_Q3], [RT_TIC_Q4], [RT_MS_Q1], [RT_MS_Q2], [RT_MS_Q3], [RT_MS_Q4]" +
					 ", [RT_MSMS_Q1], [RT_MSMS_Q2], [RT_MSMS_Q3], [RT_MSMS_Q4], [MS1_TIC_Change_Q3], [MS1_TIC_Change_Q4], [MS1_TIC_Q3], [MS1_TIC_Q4]" +
					 ", [MS1_Count], [MS1_Freq_Max], [MS1_Density_Q3], [MS2_Count], [MS2_Freq_Max], [MS2_Density_Q2], [MS2_Density_Q3], [MS2_PrecZ_1]" +
					 ", [MS2_PrecZ_2], [MS2_PrecZ_3], [MS2_PrecZ_4], [MS2_PrecZ_5], [MS2_PrecZ_more], [MS2_PrecZ_likely_1], [MS2_PrecZ_likely_multi], [Quameter_Last_Affected]" +
					 ", [C_1A], [C_1B], [C_2A], [C_2B], [C_3A], [C_3B], [C_4A], [C_4B], [C_4C], [DS_1A], [DS_1B], [DS_3A], [DS_3B], [IS_1A], [IS_1B], [IS_2], [IS_3A]" +
					 ", [IS_3B], [IS_3C], [MS1_1], [MS1_2A], [MS1_3A], [MS1_3B], [MS1_5A], [MS1_5B], [MS1_5C], [MS1_5D], [MS2_1], [MS2_2], [MS2_3], [MS2_4A]" +
					 ", [MS2_4B], [MS2_4C], [MS2_4D], [P_1A], [P_1B], [P_3], [Smaqc_Last_Affected], [PSM_Source_Job]" +
					 "FROM [V_Dataset_QC_Metrics] M INNER JOIN [T_Dataset]" +
					 "ON M.[Dataset_ID] = [T_Dataset].[Dataset_ID]" +
					 "WHERE [T_Dataset].[DS_sec_sep] NOT LIKE 'LC-Agilent-2D-Formic%'" +
					 "AND M.[Dataset_ID] IN (" + sbDatasets.ToString() + ")", mConnection);

					SqlDataReader drReader = command.ExecuteReader();

					//If the sql command doesnt find anything
					if (drReader.HasRows)
					{
						// Gets the data from the results and adds them to the list
						// Converts empty values to NA
						while (drReader.Read())
						{
							int datasetID;
							if (!int.TryParse(GetColumnString(drReader, 1), out datasetID))
							{
								Console.WriteLine("Null dataset ID value found in GetData; this is unexpected");
							}
							else
							{
								datasetIDsWithMetrics.Add(datasetID);

								List<string> lstMetrics = new List<string>();
								for (int j = 0; j < drReader.FieldCount; j++)
								{
									lstMetrics.Add(string.IsNullOrWhiteSpace(GetColumnString(drReader, j)) ? "NA" : drReader[j].ToString());
								}
								lstMetricsByDataset.Add(lstMetrics);
							}
						}

					}

					drReader.Close();
				}
				catch (Exception ex)
				{
					mErrorMessage = "Error obtaining QC Metric values: " + ex.Message;
					return new List<List<string>>();
				}
			}

			// Look for datasets for which metrics were not available
			foreach (int datasetID in datasetIDs)
            {
				if (!datasetIDsWithMetrics.Contains(datasetID))
					Console.WriteLine("Warning: DatasetID does not have QC Metrics: " + datasetID);
            }

			if (datasetIDs.Count > 1)
				Console.WriteLine("\nRetrieved dataset metrics for " + lstMetricsByDataset.Count + " / " + datasetIDs.Count + " datasets");

			// Close connection
			Close();

			return lstMetricsByDataset;
        }
      
    }

    
}