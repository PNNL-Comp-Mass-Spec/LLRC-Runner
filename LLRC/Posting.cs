using System;
using System.Collections.Generic;
using System.IO;

namespace LLRC
{
    class Posting
    {
        
        public const string STORED_PROCEDURE = "StoreQCDMResults";

        PRISM.DataBase.clsExecuteDatabaseSP _mExecuteSp;
        protected string mConnectionString;
        protected string mErrorMessage;
        protected string mStoredProcedureError;

        protected List<int> mBadDatasetIDs;
        protected List<string> mErrors;

        #region "Properties"
        public List<int> BadDatasetIDs
        {
            get
            {
                return mBadDatasetIDs;
            }
        }
        public List<string> Errors
        {
            get 
            { 
                return mErrors; 
            }
        }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public Posting()
            : this(DatabaseMang.DEFAULT_CONNECTION_STRING)
        {
        }

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
        /// <param name="lstMetricsByDataset"></param>
        /// <param name="lstValidDatasetIDs"></param>
        /// <param name="workingDirPath"></param>
        /// <returns>True if success, false if an error</returns>
        /// <remarks>Use the Errors property of this class to view any errors</remarks>
        public bool PostToDatabase(List<List<string>> lstMetricsByDataset, SortedSet<int> lstValidDatasetIDs, string workingDirPath)
        {

            mErrors.Clear();

            try
            {
                // Cache the QCDMResults
                var dctResults = CacheQCDMResults(workingDirPath);

                try
                {

                    Console.WriteLine();

                    mBadDatasetIDs.Clear();
                    mErrors.Clear();

                    foreach (var metricsOneDataset in lstMetricsByDataset)
                    {

                        var datasetID = LLRCWrapper.GetDatasetIdForMetricRow(metricsOneDataset);

                        if (!lstValidDatasetIDs.Contains(datasetID))
                        {
                            continue;
                        }

                        var smaqcJob = metricsOneDataset[(int)DatabaseMang.MetricColumnIndex.SMAQC_Job];
                        var quameterJob = metricsOneDataset[(int)DatabaseMang.MetricColumnIndex.Quameter_Job];
                        var datasetName = metricsOneDataset[(int)DatabaseMang.MetricColumnIndex.DatasetName];
                        string LLRCPrediction;

                        if (!dctResults.TryGetValue(datasetID, out LLRCPrediction))
                        {
                            Console.WriteLine("LLRC value not computed for DatasetID " + datasetID);
                            continue;
                        }

                        // Create XML for posting to the database
                        var xml = ConvertQcdmtoXml(LLRCPrediction, smaqcJob, quameterJob, datasetName);

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
            else
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

            using (var srResults = new StreamReader(new FileStream(resultsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                var headersParsed = false;
                var colIndexLLRC = -1;

                while (srResults.Peek() > -1)
                {
                    var resultLine = srResults.ReadLine();
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
                        int datasetID;
                        double LLRCPrediction;

                        if (int.TryParse(resultValues[(int)DatabaseMang.MetricColumnIndex.DatasetID], out datasetID))
                        {

                            if (double.TryParse(resultValues[colIndexLLRC], out LLRCPrediction))
                            {
                                results.Add(datasetID, resultValues[colIndexLLRC]);
                            }
                        }
                    }

                }
            }

            return results;

        }
        
        /// <summary>
        /// Converts the QCDM to xml to be used by database
        /// </summary>
        /// <param name="LLRCPrediction"></param>
        /// <param name="smaqcJob"></param>
        /// <param name="quameterJob"></param>
        /// <param name="datasetName"></param>
        /// <returns></returns>
        private string ConvertQcdmtoXml(string LLRCPrediction, string smaqcJob, string quameterJob, string datasetName)
        {
            var sbXml = new System.Text.StringBuilder();
            string sXmlResults;

            try
            {
                sbXml.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
                sbXml.Append("<QCDM_Results>");

                sbXml.Append("<Dataset>" + datasetName + "</Dataset>");
                if (smaqcJob == "NA")
                {
                    smaqcJob = "0";
                }
                sbXml.Append("<SMAQC_Job>" + smaqcJob + "</SMAQC_Job>");
                sbXml.Append("<Quameter_Job>" + quameterJob + "</Quameter_Job>");

                sbXml.Append("<Measurements>");
                sbXml.Append("<Measurement Name=\"" + "QCDM" + "\">" + LLRCPrediction + "</Measurement>");
                sbXml.Append("</Measurements>");

                sbXml.Append("</QCDM_Results>");

                sXmlResults = sbXml.ToString();

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error converting Quameter results to XML; details:");
                Console.WriteLine(ex);
                return string.Empty;
            }

            return sXmlResults;

        }

        /// <summary>
        /// Posts the xml to the database
        /// </summary>
        /// <param name="intDatasetId"></param>
        /// <param name="sXmlResults"></param>
        /// <param name="sConnectionString"></param>
        /// <param name="sStoredProcedure"></param>
        /// <returns></returns>
        protected bool PostQcdmResultsToDb(int intDatasetId, string sXmlResults, string sConnectionString, string sStoredProcedure)
        {

            const int maxRetryCount = 3;
            const int secBetweenRetries = 20;

            var intStartIndex = 0;
            var intResult = 0;

            string sXMLResultsClean = null;

            System.Data.SqlClient.SqlCommand objCommand;

            var blnSuccess = false;
            mErrorMessage = string.Empty;
            mStoredProcedureError = string.Empty;

            try
            {
                Console.WriteLine("Posting QCDM Results to the database for Dataset ID " + intDatasetId);

                // We need to remove the encoding line from sXMLResults before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-8" standalone="yes"?>

                intStartIndex = sXmlResults.IndexOf("?>");
                if (intStartIndex > 0)
                {
                    sXMLResultsClean = sXmlResults.Substring(intStartIndex + 2).Trim();
                }
                else
                {
                    sXMLResultsClean = sXmlResults;
                }

                // Call stored procedure sStoredProcedure using connection string sConnectionString

                objCommand = new System.Data.SqlClient.SqlCommand();

                {
                    objCommand.CommandType = System.Data.CommandType.StoredProcedure;
                    objCommand.CommandText = sStoredProcedure;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@Return", System.Data.SqlDbType.Int));
                    objCommand.Parameters["@Return"].Direction = System.Data.ParameterDirection.ReturnValue;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@DatasetID", System.Data.SqlDbType.Int));
                    objCommand.Parameters["@DatasetID"].Direction = System.Data.ParameterDirection.Input;
                    objCommand.Parameters["@DatasetID"].Value = intDatasetId;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@ResultsXML", System.Data.SqlDbType.Xml));
                    objCommand.Parameters["@ResultsXML"].Direction = System.Data.ParameterDirection.Input;
                    objCommand.Parameters["@ResultsXML"].Value = sXMLResultsClean;
                }

                _mExecuteSp = new PRISM.DataBase.clsExecuteDatabaseSP(sConnectionString);
                AttachExecuteSpEvents();

                intResult = _mExecuteSp.ExecuteSP(objCommand, maxRetryCount, secBetweenRetries);

                if (intResult == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    blnSuccess = true;
                }
                else
                {
                    mErrorMessage = "Error storing QCDM Results in database for DatasetID " + intDatasetId + ": " + sStoredProcedure + " returned " + intResult;
                    blnSuccess = false;
                }

            }
            catch (System.Exception ex)
            {
                mErrorMessage = "Exception storing QCDM Results in database for DatasetID " + intDatasetId + ": " + ex.Message;
                blnSuccess = false;
            }
            finally
            {
                DetachExecuteSpEvents();
                _mExecuteSp = null;
            }

            return blnSuccess;
        }

        private void AttachExecuteSpEvents()
        {
            try
            {
                _mExecuteSp.DBErrorEvent += new PRISM.DataBase.clsExecuteDatabaseSP.DBErrorEventEventHandler(mExecuteSP_DBErrorEvent);
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
                    _mExecuteSp.DBErrorEvent -= mExecuteSP_DBErrorEvent;
                }
            }
            catch
            {
                // Ignore errors here
            }
        }

        private void mExecuteSP_DBErrorEvent(string message)
        {
            mStoredProcedureError = message;
            Console.WriteLine("Stored procedure execution error: " + message);
        }
    }
}
