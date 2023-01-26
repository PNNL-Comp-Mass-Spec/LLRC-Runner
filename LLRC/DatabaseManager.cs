using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace LLRC
{
    class DatabaseManager : PRISM.EventNotifier
    {
public const string DEFAULT_CONNECTION_STRING = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

        private readonly SqlConnection mConnection;

        protected string mErrorMessage;

        public string ErrorMessage => mErrorMessage;

        public const int DATASET_ID_COLUMN_INDEX = 1;

        /// <summary>
        /// This enum is used for the keys in the dictionary of metrics obtained for each dataset
        /// </summary>
        /// <remarks>
        /// <para>The names correspond to the column names in view V_Dataset_QC_Metrics_Export</para>
        /// <para>Metric values are written to the .csv file by increasing enum value</para>
        /// <para>
        /// The R processing code assumes the column order defined here.
        /// If the order is changed, or if any enum values are added or removed, the R code must be updated.
        /// </para>
        /// </remarks>
        public enum MetricColumns
        {
            Instrument_Group = 0,
            Dataset_ID = 1,
            Instrument = 2,
            Dataset = 3,
            XIC_Wide_Frac = 4,
            MS1_TIC_Change_Q2 = 5,
            MS1_TIC_Q2 = 6,
            MS1_Density_Q1 = 7,
            MS1_Density_Q2 = 8,
            MS2_Density_Q1 = 9,
            DS_2A = 10,
            DS_2B = 11,
            MS1_2B = 12,
            P_2A = 13,
            P_2B = 14,
            P_2C = 15,
            SMAQC_Job = 16,
            Quameter_Job = 17,
            XIC_Height_Q4 = 18,
            DS_1A = 19,
            IS_1A = 20,
            IS_3A = 21,
            MS2_1 = 22,
            MS2_4A = 23,
            MS2_4B = 24
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public DatabaseManager(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                connectionString = DEFAULT_CONNECTION_STRING;

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
        /// <param name="skipAlreadyProcessedDatasets">Set to True to skip DatasetIDs that already have a QCDM value</param>
        /// <returns>QC data dictionary, where keys are dataset IDs and values are a dictionary of metrics for the given dataset</returns>
        public Dictionary<int, Dictionary<MetricColumns, string>> GetData(List<int> datasetIDs, bool skipAlreadyProcessedDatasets)
        {
            const int CHUNK_SIZE = 500;

            var datasetIDsWithMetrics = new SortedSet<int>();
            var metricsByDataset = new Dictionary<int, Dictionary<MetricColumns, string>>();
            var datasets = new System.Text.StringBuilder();

            // Open the database connection
            if (!Open())
                return new List<List<string>>();

            var startTime = DateTime.UtcNow;
            var lastProgress = DateTime.UtcNow;
            var showProgress = false;

            // Process the datasets in chunks, 500 datasets at a time
            for (var i = 0; i < datasetIDs.Count; i += CHUNK_SIZE)
            {
                // Construct a comma-separated list of dataset IDs
                datasets.Clear();
                for (var j = i; j < i + CHUNK_SIZE && j < datasetIDs.Count; j++)
                {
                    if (datasets.Length > 0)
                        datasets.Append(",");

                    datasets.Append(datasetIDs[j]);
                }

                try
                {
                    // Uses are massive SQL command to get the data to come out in the order we want it too
                    // If you add/remove columns, you must update the iWriteFiles.WriteCsv
                    var sqlQuery = "SELECT M.[Instrument Group], M.[Dataset_ID], [Instrument], [Dataset], [XIC_WideFrac]" +
                     ", [MS1_TIC_Change_Q2], [MS1_TIC_Q2], [MS1_Density_Q1], [MS1_Density_Q2], [MS2_Density_Q1], [DS_2A], [DS_2B], [MS1_2B]" +
                     ", [P_2A], [P_2B], [P_2C], [SMAQC_Job], [Quameter_Job], [XIC_FWHM_Q1], [XIC_FWHM_Q2], [XIC_FWHM_Q3], [XIC_Height_Q2], [XIC_Height_Q3]" +
                     ", [XIC_Height_Q4], [RT_Duration], [RT_TIC_Q1], [RT_TIC_Q2], [RT_TIC_Q3], [RT_TIC_Q4], [RT_MS_Q1], [RT_MS_Q2], [RT_MS_Q3], [RT_MS_Q4]" +
                     ", [RT_MSMS_Q1], [RT_MSMS_Q2], [RT_MSMS_Q3], [RT_MSMS_Q4], [MS1_TIC_Change_Q3], [MS1_TIC_Change_Q4], [MS1_TIC_Q3], [MS1_TIC_Q4]" +
                     ", [MS1_Count], [MS1_Freq_Max], [MS1_Density_Q3], [MS2_Count], [MS2_Freq_Max], [MS2_Density_Q2], [MS2_Density_Q3], [MS2_PrecZ_1]" +
                     ", [MS2_PrecZ_2], [MS2_PrecZ_3], [MS2_PrecZ_4], [MS2_PrecZ_5], [MS2_PrecZ_more], [MS2_PrecZ_likely_1], [MS2_PrecZ_likely_multi], [Quameter_Last_Affected]" +
                     ", [C_1A], [C_1B], [C_2A], [C_2B], [C_3A], [C_3B], [C_4A], [C_4B], [C_4C], [DS_1A], [DS_1B], [DS_3A], [DS_3B], [IS_1A], [IS_1B], [IS_2], [IS_3A]" +
                     ", [IS_3B], [IS_3C], [MS1_1], [MS1_2A], [MS1_3A], [MS1_3B], [MS1_5A], [MS1_5B], [MS1_5C], [MS1_5D], [MS2_1], [MS2_2], [MS2_3], [MS2_4A]" +
                     ", [MS2_4B], [MS2_4C], [MS2_4D], [P_1A], [P_1B], [P_3], [Smaqc_Last_Affected], [PSM_Source_Job]" +
                     " FROM [V_Dataset_QC_Metrics] M INNER JOIN [T_Dataset] ON M.[Dataset_ID] = [T_Dataset].[Dataset_ID]" +
                     " WHERE [T_Dataset].[DS_sec_sep] NOT LIKE 'LC-Agilent-2D-Formic%'" +
                     " AND M.[Dataset_ID] IN (" + sbDatasets + ")";

                    if (skipAlreadyProcessedDatasets)
                        sqlQuery += " AND M.QCDM Is Null";

                    var command = new SqlCommand(sqlQuery, mConnection);

                    var drReader = command.ExecuteReader();

                    if (drReader.HasRows)
                    {
                        // Append the data to metricsByDataset
                        // Converts empty values to NA
                        while (drReader.Read())
                        {
                            if (!int.TryParse(GetColumnString(drReader, 1), out var datasetID))
                            {
                                Console.WriteLine("Null dataset ID value found in GetData; this is unexpected");
                            }
                            else
                            {
                                datasetIDsWithMetrics.Add(datasetID);

                                var metrics = new List<string>();

                                for (var j = 0; j < drReader.FieldCount; j++)
                                {
                                    metrics.Add(string.IsNullOrWhiteSpace(GetColumnString(drReader, j)) ? "NA" : drReader[j].ToString());
                                }

                                metricsByDataset.Add(metrics);
                            }
                        }
                    }

                    drReader.Close();
                }
                catch (Exception ex)
                {
                    OnErrorEvent("Error obtaining QC Metric value", ex);
                    mErrorMessage = "Error obtaining QC Metric values: " + ex.Message;
                    return new Dictionary<int, Dictionary<MetricColumns, string>>();
                }

                if (DateTime.UtcNow.Subtract(startTime).TotalSeconds >= 1 && i + CHUNK_SIZE < datasetIDs.Count)
                    showProgress = true;

                if (showProgress && DateTime.UtcNow.Subtract(lastProgress).TotalMilliseconds >= 333)
                {
                    var percentComplete = (i + CHUNK_SIZE) / (double)datasetIDs.Count * 100;
                    OnDebugEvent("Retrieving metrics from the database: " + percentComplete.ToString("0.0") + "% complete");
                    lastProgress = DateTime.UtcNow;
                }
            }

            // Look for datasets for which metrics were not available
            var warnCount = 0;
            foreach (var datasetID in datasetIDs)
            {
                if (!datasetIDsWithMetrics.Contains(datasetID))
                {
                    warnCount++;
                    if (warnCount <= 10)
                        OnWarningEvent("Warning: DatasetID does not have QC Metrics: " + datasetID);
                }
            }

            if (warnCount > 10)
                OnWarningEvent(" ... " + (warnCount - 10) + " additional warnings not shown");

            if (datasetIDs.Count > 1)
                OnStatusEvent("\nRetrieved dataset metrics for " + metricsByDataset.Count + " / " + datasetIDs.Count + " datasets");

            Console.WriteLine();

            // Close connection
            Close();

            return metricsByDataset;
        }
    }

}
