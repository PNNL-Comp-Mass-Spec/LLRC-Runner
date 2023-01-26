using PRISMDatabaseUtils;
using System;
using System.Collections.Generic;
using System.Data;

namespace LLRC
{
    internal class DatabaseManager : PRISM.EventNotifier
    {
        private readonly IDBTools mDbTools;

        private string mErrorMessage;

        public string ErrorMessage => mErrorMessage;

        public const int DATASET_ID_COLUMN_INDEX = 1;
        // ReSharper disable InconsistentNaming

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
            XIC_FWHM_Q1 = 18,
            XIC_FWHM_Q2 = 19,
            XIC_FWHM_Q3 = 20,
            XIC_Height_Q2 = 21,
            XIC_Height_Q3 = 22,
            XIC_Height_Q4 = 23,
            RT_Duration = 24,
            RT_TIC_Q1 = 25,
            RT_TIC_Q2 = 26,
            RT_TIC_Q3 = 27,
            RT_TIC_Q4 = 28,
            RT_MS_Q1 = 29,
            RT_MS_Q2 = 30,
            RT_MS_Q3 = 31,
            RT_MS_Q4 = 32,
            RT_MSMS_Q1 = 33,
            RT_MSMS_Q2 = 34,
            RT_MSMS_Q3 = 35,
            RT_MSMS_Q4 = 36,
            MS1_TIC_Change_Q3 = 37,
            MS1_TIC_Change_Q4 = 38,
            MS1_TIC_Q3 = 39,
            MS1_TIC_Q4 = 40,
            MS1_Count = 41,
            MS1_Freq_Max = 42,
            MS1_Density_Q3 = 43,
            MS2_Count = 44,
            MS2_Freq_Max = 45,
            MS2_Density_Q2 = 46,
            MS2_Density_Q3 = 47,
            MS2_Prec_Z_1 = 48,
            MS2_Prec_Z_2 = 49,
            MS2_Prec_Z_3 = 50,
            MS2_Prec_Z_4 = 51,
            MS2_Prec_Z_5 = 52,
            MS2_Prec_Z_more = 53,
            MS2_Prec_Z_likely_1 = 54,
            MS2_Prec_Z_likely_multi = 55,
            Quameter_Last_Affected = 56,
            C_1A = 57,
            C_1B = 58,
            C_2A = 59,
            C_2B = 60,
            C_3A = 61,
            C_3B = 62,
            C_4A = 63,
            C_4B = 64,
            C_4C = 65,
            DS_1A = 66,
            DS_1B = 67,
            DS_3A = 68,
            DS_3B = 69,
            IS_1A = 70,
            IS_1B = 71,
            IS_2 = 72,
            IS_3A = 73,
            IS_3B = 74,
            IS_3C = 75,
            MS1_1 = 76,
            MS1_2A = 77,
            MS1_3A = 78,
            MS1_3B = 79,
            MS1_5A = 80,
            MS1_5B = 81,
            MS1_5C = 82,
            MS1_5D = 83,
            MS2_1 = 84,
            MS2_2 = 85,
            MS2_3 = 86,
            MS2_4A = 87,
            MS2_4B = 88,
            MS2_4C = 89,
            MS2_4D = 90,
            P_1A = 91,
            P_1B = 92,
            P_3 = 93,
            Smaqc_Last_Affected = 94,
            PSM_Source_Job = 95
        }

        // ReSharper restore InconsistentNaming

        /// <summary>
        /// Constructor
        /// </summary>
        public DatabaseManager(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string must be provided", nameof(connectionString));

            mDbTools = DbToolsFactory.GetDBTools(connectionString);
            RegisterEvents(mDbTools);

            mErrorMessage = string.Empty;
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
                    // Query to retrieve metric data
                    // Column names here must be lowercase, and must correspond to the enum names in enum MetricColumns

                    var sqlQuery = string.Format(
                        "SELECT instrument_group, dataset_id, instrument, dataset, xic_wide_frac," +
                              " ms1_tic_change_q2, ms1_tic_q2, ms1_density_q1, ms1_density_q2, ms2_density_q1, ds_2a, ds_2b, ms1_2b," +
                              " p_2a, p_2b, p_2c, smaqc_job, quameter_job, xic_fwhm_q1, xic_fwhm_q2, xic_fwhm_q3, xic_height_q2, xic_height_q3," +
                              " xic_height_q4, rt_duration, rt_tic_q1, rt_tic_q2, rt_tic_q3, rt_tic_q4, rt_ms_q1, rt_ms_q2, rt_ms_q3, rt_ms_q4," +
                              " rt_msms_q1, rt_msms_q2, rt_msms_q3, rt_msms_q4, ms1_tic_change_q3, ms1_tic_change_q4, ms1_tic_q3, ms1_tic_q4," +
                              " ms1_count, ms1_freq_max, ms1_density_q3, ms2_count, ms2_freq_max, ms2_density_q2, ms2_density_q3," +
                              " ms2_prec_z_1, ms2_prec_z_2, ms2_prec_z_3, ms2_prec_z_4, ms2_prec_z_5, ms2_prec_z_more, " +
                              " ms2_prec_z_likely_1, ms2_prec_z_likely_multi, quameter_last_affected," +
                              " c_1a, c_1b, c_2a, c_2b, c_3a, c_3b, c_4a, c_4b, c_4c, ds_1a, ds_1b, ds_3a, ds_3b, is_1a, is_1b, is_2, is_3a," +
                              " is_3b, is_3c, ms1_1, ms1_2a, ms1_3a, ms1_3b, ms1_5a, ms1_5b, ms1_5c, ms1_5d, ms2_1, ms2_2, ms2_3, ms2_4a," +
                              " ms2_4b, ms2_4c, ms2_4d, p_1a, p_1b, p_3, smaqc_last_affected, psm_source_job " +
                        " FROM V_Dataset_QC_Metrics_Export" +
                        " WHERE Not separation_type LIKE 'LC-Agilent-2D-Formic%' " +
                        " AND Dataset_ID IN ({0}) {1}",
                        datasets, skipAlreadyProcessedDatasets ? " AND QCDM Is Null" : string.Empty);

                    var cmd = mDbTools.CreateCommand(sqlQuery);

                    var success = mDbTools.GetQueryResultsDataTable(cmd, out var queryResults);

                    if (!success)
                    {
                        mErrorMessage = "Error obtaining data from V_Dataset_QC_Metrics_Export using GetQueryResultsDataTable";
                        return new Dictionary<int, Dictionary<MetricColumns, string>>();
                    }

                    // Keys in this dictionary are enum values, values are the string name of the enum, converted to lowercase
                    var columnMap = new Dictionary<MetricColumns, string>();

                    foreach (MetricColumns column in Enum.GetValues(typeof(MetricColumns)))
                    {
                        var metricName = Enum.GetName(typeof(MetricColumns), column);

                        if (metricName == null)
                            throw new NullReferenceException("Encountered a null metric name while populating the column map dictionary");

                        columnMap.Add(column, metricName.ToLower());
                    }

                    // Append the data to metricsByDataset
                    // Converts empty values to NA
                    foreach (DataRow resultRow in queryResults.Rows)
                    {
                        var datasetId = mDbTools.GetInteger(resultRow[columnMap[MetricColumns.Dataset_ID]]);

                        if (datasetId == 0)
                        {
                            OnWarningEvent("Null dataset ID value found in GetData; this is unexpected");
                            continue;
                        }

                        datasetIDsWithMetrics.Add(datasetId);

                        var metrics = new Dictionary<MetricColumns, string>();

                        foreach (var column in columnMap)
                        {
                            var textValue = mDbTools.GetString(resultRow[column.Value]);

                            metrics.Add(column.Key, string.IsNullOrWhiteSpace(textValue) ? "NA" : textValue);
                        }

                        metricsByDataset.Add(datasetId, metrics);
                    }
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

            return metricsByDataset;
        }
    }
}
