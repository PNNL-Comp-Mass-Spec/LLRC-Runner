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
                              " ms2_prec_z_1, ms2_prec_z_1, ms2_prec_z_2, ms2_prec_z_3, ms2_prec_z_4, ms2_prec_z_5, ms2_prec_z_more, " +
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
