using PRISMDatabaseUtils;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using PRISM;

namespace LLRC
{
    class Posting : EventNotifier
    {
        public const string STORED_PROCEDURE = "StoreQCDMResults";

        protected string mConnectionString;
        protected string mErrorMessage;
        protected string mStoredProcedureError;

        protected List<int> mBadDatasetIDs;
        protected List<string> mErrors;

        public List<int> BadDatasetIDs => mBadDatasetIDs;

        public List<string> Errors => mErrors;

        /// <summary>
        /// Constructor
        /// </summary>
        public Posting(string connectionString)
        {
            mConnectionString = connectionString;
            mErrorMessage = string.Empty;

            mBadDatasetIDs = new List<int>();
            mErrors = new List<string>();
        }

        /// <summary>
        /// Posts the QCDM metric to the database
        /// </summary>
        /// <param name="metricsByDataset">Dictionary where keys are Dataset IDs and values are the metric values</param>
        /// <param name="validDatasetIDs"></param>
        /// <param name="workingDirPath"></param>
        /// <remarks>Use the Errors property of this class to view any errors</remarks>
        /// <returns>True if success, false if an error</returns>
        public bool PostToDatabase(Dictionary<int, Dictionary<DatabaseManager.MetricColumns, string>> metricsByDataset, SortedSet<int> validDatasetIDs, string workingDirPath)
        {
            mErrors.Clear();

            try
            {
                // Read the QCDMResults
                var results = LoadQCDMResults(workingDirPath);

                var currentDatasetID = 0;

                try
                {
                    Console.WriteLine();

                    mBadDatasetIDs.Clear();
                    mErrors.Clear();

                    foreach (var metricsOneDataset in metricsByDataset)
                    {
                        var datasetID = metricsOneDataset.Key;

                        currentDatasetID = datasetID;

                        if (!validDatasetIDs.Contains(datasetID))
                        {
                            continue;
                        }

                        var smaqcJob = metricsOneDataset.Value[DatabaseManager.MetricColumns.SMAQC_Job];
                        var quameterJob = metricsOneDataset.Value[DatabaseManager.MetricColumns.Quameter_Job];
                        var datasetName = metricsOneDataset.Value[DatabaseManager.MetricColumns.Dataset];

                        if (!results.TryGetValue(datasetID, out var llrcPrediction))
                        {
                            Console.WriteLine("LLRC value not computed for DatasetID " + datasetID);
                            continue;
                        }

                        // Create XML for posting to the database
                        var xml = ConvertQCDMtoXml(llrcPrediction, smaqcJob, quameterJob, datasetName);

                        //attempts to post to database and returns true or false
                        var success = PostQCDMResultsToDb(datasetID, xml, mConnectionString, STORED_PROCEDURE);

                        if (success)
                            continue;

                        mBadDatasetIDs.Add(datasetID);

                        if (string.IsNullOrEmpty(mStoredProcedureError))
                            mErrors.Add(mErrorMessage);
                        else
                            mErrors.Add(mErrorMessage + "; " + mStoredProcedureError);
                    }
                }
                catch (Exception ex)
                {
                    OnErrorEvent(string.Format("Exception storing results for dataset ID {0}", currentDatasetID), ex);

                    mErrors.Add("Exception posting to the database: " + ex.Message);
                }

                if (mErrors.Count == 0)
                {
                    Console.WriteLine("  Successfully posted results");
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception loading the QCDM results", ex);
                mErrors.Add("Exception loading the results: " + ex.Message);
            }

            if (mErrors.Count == 0)
                return true;

            return false;
        }

        /// <summary>
        /// Reads the QCDM value from the .csv file that is created from the R program
        /// </summary>
        /// <param name="workingDirPath"></param>
        public Dictionary<int, string> LoadQCDMResults(string workingDirPath)
        {
            var resultsFilePath = Path.Combine(workingDirPath, "TestingDataset.csv");
            var results = new Dictionary<int, string>();

            if (!File.Exists(resultsFilePath))
            {
                Console.WriteLine("Results file not found: " + resultsFilePath);
                return results;
            }

            using (var reader = new StreamReader(new FileStream(resultsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                var headersParsed = false;
                var colIndexLLRC = -1;

                while (!reader.EndOfStream)
                {
                    var resultLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(resultLine))
                        continue;

                    var resultValues = resultLine.Split(',');

                    if (resultValues.Length < 1)
                        continue;

                    if (!headersParsed)
                    {
                        // The final column should be the LLRC value
                        if (resultValues[resultValues.Length - 1].StartsWith("\"LLRC.Prediction"))
                        {
                            colIndexLLRC = resultValues.Length - 1;
                            headersParsed = true;
                        }
                        else
                        {
                            Console.WriteLine("Error, last column of the header line in the results file does not start with LLRC: " + resultsFilePath);
                            return results;
                        }
                    }
                    else if (int.TryParse(resultValues[DatabaseManager.DATASET_ID_COLUMN_INDEX], out var datasetID))
                    {
                        if (double.TryParse(resultValues[colIndexLLRC], out _))
                        {
                            // Yes, it's a double
                            // Store the string representation
                            results.Add(datasetID, resultValues[colIndexLLRC]);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Converts the QCDM to XML to be used by database
        /// </summary>
        /// <param name="llrcPrediction"></param>
        /// <param name="smaqcJob"></param>
        /// <param name="quameterJob"></param>
        /// <param name="datasetName"></param>
        /// <returns>QCDM results, as XML</returns>
        private string ConvertQCDMtoXml(string llrcPrediction, string smaqcJob, string quameterJob, string datasetName)
        {
            var xmlResults = new System.Text.StringBuilder();

            try
            {
                xmlResults.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
                xmlResults.Append("<QCDM_Results>");

                xmlResults.AppendFormat("<Dataset>{0}</Dataset>", datasetName);
                if (smaqcJob == "NA")
                {
                    smaqcJob = "0";
                }
                xmlResults.AppendFormat("<SMAQC_Job>{0}</SMAQC_Job>", smaqcJob);
                xmlResults.AppendFormat("<Quameter_Job>{0}</Quameter_Job>", quameterJob);

                xmlResults.Append("<Measurements>");
                xmlResults.AppendFormat("<Measurement Name=\"QCDM\">{0}</Measurement>", llrcPrediction);
                xmlResults.Append("</Measurements>");

                xmlResults.Append("</QCDM_Results>");

                return xmlResults.ToString();
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error converting Quameter results to XML", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Posts the xml to the database
        /// </summary>
        /// <param name="datasetId"></param>
        /// <param name="xmlResults"></param>
        /// <param name="connectionString"></param>
        /// <param name="storedProcedure"></param>
        /// <returns>True if successful, false if an error</returns>
        protected bool PostQCDMResultsToDb(int datasetId, string xmlResults, string connectionString, string storedProcedure)
        {
            mErrorMessage = string.Empty;
            mStoredProcedureError = string.Empty;

            try
            {
                Console.WriteLine("Posting QCDM Results to the database for Dataset ID " + datasetId);

                // We need to remove the encoding line from xmlResults before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-8" standalone="yes"?>

                var startIndex = xmlResults.IndexOf("?>", StringComparison.Ordinal);
                string xmlResultsClean;

                if (startIndex > 0)
                {
                    xmlResultsClean = xmlResults.Substring(startIndex + 2).Trim();
                }
                else
                {
                    xmlResultsClean = xmlResults;
                }

                // Call stored procedure StoreQCDMResults
                var dbTools = DbToolsFactory.GetDBTools(connectionString);

                var cmdPost = dbTools.CreateCommand(storedProcedure, CommandType.StoredProcedure);

                // Define parameter for procedure's return value
                var returnParam = dbTools.AddParameter(cmdPost, "@Return", SqlType.Int, ParameterDirection.ReturnValue);

                // Define parameters for the procedure's arguments
                dbTools.AddParameter(cmdPost, "@DatasetID", SqlType.Int).Value = datasetId;
                dbTools.AddParameter(cmdPost, "@ResultsXML", SqlType.XML).Value = xmlResultsClean;

                // Execute the stored procedure
                dbTools.ExecuteSP(cmdPost);

                // Get return value
                var returnCode = dbTools.GetInteger(returnParam.Value);

                if (returnCode == 0)
                {
                    // No errors
                    return true;
                }

                mErrorMessage = string.Format(
                    "Error storing QCDM Results in database for DatasetID {0}; {1} returned {2}",
                    datasetId, storedProcedure, returnCode);

                OnWarningEvent(mErrorMessage);
                return false;
            }
            catch (Exception ex)
            {
                var msg = string.Format("Exception storing QCDM Results in database for DatasetID {0}", datasetId);
                OnErrorEvent(msg, ex);

                mErrorMessage = msg + ": " + ex.Message;
                return false;
            }
        }
    }
}
