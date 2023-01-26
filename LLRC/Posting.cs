using System;
using System.Collections.Generic;
using System.IO;

namespace LLRC
{
    class Posting
    {
        public const string STORED_PROCEDURE = "StoreQCDMResults";

        PRISM.ExecuteDatabaseSP _mExecuteSp;
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
        /// <param name="metricsByDataset"></param>
        /// <param name="validDatasetIDs"></param>
        /// <param name="workingDirPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Use the Errors property of this class to view any errors</remarks>
        public bool PostToDatabase(List<List<string>> metricsByDataset, SortedSet<int> validDatasetIDs, string workingDirPath)
        {
            mErrors.Clear();

            try
            {
                // Cache the QCDMResults
                var results = CacheQCDMResults(workingDirPath);

                try
                {
                    Console.WriteLine();

                    mBadDatasetIDs.Clear();
                    mErrors.Clear();

                    foreach (var metricsOneDataset in metricsByDataset)
                    {
                        var datasetID = LLRCWrapper.GetDatasetIdForMetricRow(metricsOneDataset);

                        if (!validDatasetIDs.Contains(datasetID))
                        {
                            continue;
                        }

                        var smaqcJob = metricsOneDataset[DatabaseMang.MetricColumnIndex.SMAQC_Job];
                        var quameterJob = metricsOneDataset[DatabaseMang.MetricColumnIndex.Quameter_Job];
                        var datasetName = metricsOneDataset[DatabaseMang.MetricColumnIndex.DatasetName];

                        if (!results.TryGetValue(datasetID, out var llrcPrediction))
                        {
                            Console.WriteLine("LLRC value not computed for DatasetID " + datasetID);
                            continue;
                        }

                        // Create XML for posting to the database
                        var xml = ConvertQCDMtoXml(llrcPrediction, smaqcJob, quameterJob, datasetName);

                        //attempts to post to database and returns true or false
                        var success = PostQcdmResultsToDb(datasetID, xml, mConnectionString, STORED_PROCEDURE);
                        if (!success)
                        {
                            mBadDatasetIDs.Add(datasetID);

                            Console.WriteLine("  Error posting results: " + mErrorMessage);
                            if (string.IsNullOrEmpty(mStoredProcedureError))
                                mErrors.Add(mErrorMessage);
                            else
                                mErrors.Add(mErrorMessage + "; " + mStoredProcedureError);
                        }
                    }
                }
                catch (Exception ex)
                {
                    mErrors.Add("Exception posting to the database: " + ex.Message);
                }

                if (mErrors.Count == 0)
                {
                    Console.WriteLine("  Successfully posted results");
                }
            }
            catch (Exception ex)
            {
                mErrors.Add("Exception caching the results: " + ex.Message);
            }


            if (mErrors.Count == 0)
                return true;

            return false;
        }

        //gets the QCDM value from the .csv file that is created from the R program
        public Dictionary<int, string> CacheQCDMResults(string workingDirPath)
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
                    else
                    {
                        if (int.TryParse(resultValues[DatabaseMang.MetricColumnIndex.DatasetID], out var datasetID))
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
                Console.WriteLine("Error converting Quameter results to XML; details:");
                Console.WriteLine(ex);
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
        protected bool PostQcdmResultsToDb(int datasetId, string xmlResults, string connectionString, string storedProcedure)
        {
            const int maxRetryCount = 3;
            const int secBetweenRetries = 20;

            bool success;
            mErrorMessage = string.Empty;
            mStoredProcedureError = string.Empty;

            try
            {
                Console.WriteLine("Posting QCDM Results to the database for Dataset ID " + datasetId);

                // We need to remove the encoding line from sXMLResults before posting to the DB
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

                // Call the stored procedure

                var dbCommand = new System.Data.SqlClient.SqlCommand();

                {
                    dbCommand.CommandType = System.Data.CommandType.StoredProcedure;
                    dbCommand.CommandText = storedProcedure;

                    dbCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@Return", System.Data.SqlDbType.Int));
                    dbCommand.Parameters["@Return"].Direction = System.Data.ParameterDirection.ReturnValue;

                    dbCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@DatasetID", System.Data.SqlDbType.Int));
                    dbCommand.Parameters["@DatasetID"].Direction = System.Data.ParameterDirection.Input;
                    dbCommand.Parameters["@DatasetID"].Value = datasetId;

                    dbCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@ResultsXML", System.Data.SqlDbType.Xml));
                    dbCommand.Parameters["@ResultsXML"].Direction = System.Data.ParameterDirection.Input;
                    dbCommand.Parameters["@ResultsXML"].Value = xmlResultsClean;
                }

                _mExecuteSp = new PRISM.ExecuteDatabaseSP(connectionString);
                AttachExecuteSpEvents();

                var result = _mExecuteSp.ExecuteSP(dbCommand, maxRetryCount, secBetweenRetries);

                if (result == PRISM.ExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    success = true;
                }
                else
                {
                    mErrorMessage = "Error storing QCDM Results in database for DatasetID " + datasetId + ": " + storedProcedure + " returned " + result;
                    success = false;
                }
            }
            catch (Exception ex)
            {
                mErrorMessage = "Exception storing QCDM Results in database for DatasetID " + datasetId + ": " + ex.Message;
                success = false;
            }
            finally
            {
                DetachExecuteSpEvents();
                _mExecuteSp = null;
            }

            return success;
        }

        private void AttachExecuteSpEvents()
        {
            try
            {
                _mExecuteSp.ErrorEvent += mExecuteSP_DBErrorEvent;
            }
            catch
            {
                // Ignore errors here
            }
        }

        private void DetachExecuteSpEvents()
        {
            try
            {
                if (_mExecuteSp != null)
                {
                    _mExecuteSp.ErrorEvent -= mExecuteSP_DBErrorEvent;
                }
            }
            catch
            {
                // Ignore errors here
            }
        }

        private void mExecuteSP_DBErrorEvent(string message, Exception ex)
        {
            mStoredProcedureError = message;
            Console.WriteLine("Stored procedure execution error: " + message);
        }
    }
}
