using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PRISMDatabaseUtils;
using System.Data;

namespace LLRC
{
    public class LLRCWrapper : PRISM.EventNotifier
    {
        public const string NO_NEW_RECENT_DATASETS = "No new datasets found with new QC values from the last";

        public const string RDATA_FILE_MODELS = "Models_paper.Rdata";
        public const string RDATA_FILE_ALLDATA = "allData_v4.Rdata";

        private readonly string mConnectionString;

        private string mErrorMessage;

        public string ErrorMessage => mErrorMessage;

        /// <summary>
        /// Maximum number of results to display
        /// </summary>
        /// <remarks>Only used if mPostToDB is false</remarks>
        public int MaxResultsToDisplay { get; set; }

        public bool PostToDB { get; set; }

        /// <summary>
        /// Set this to True if processing a set of DatasetIDs from a timespan; when this is true, the code will not report an error if none of the datasets has valid metrics
        /// </summary>
        public bool ProcessingTimespan { get; set; }

        public bool SkipAlreadyProcessedDatasets { get; set; }

        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public LLRCWrapper(string connectionString)
        {
            mErrorMessage = string.Empty;

            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string must be provided", nameof(connectionString));

            mConnectionString = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, "LLRC");

            WorkingDirectory = PRISM.AppUtils.GetAppDirectoryPath();

            MaxResultsToDisplay = 10;
            PostToDB = false;
            ProcessingTimespan = false;
            SkipAlreadyProcessedDatasets = false;
        }

        /// <summary>
        /// Looks for datasets with entries in T_Dataset_QC where Quameter_Last_Affected or Last_Affected are within the last x hours, while QCDM is null
        /// </summary>
        /// <param name="hours"></param>
        /// <param name="connectionString"></param>
        /// <returns>List of dataset IDs</returns>
        private List<int> FindRecentNewDatasets(int hours, string connectionString)
        {
            var datasetIDs = new List<int>();

            try
            {
                var timestampThreshold = DateTime.Now.Subtract(new TimeSpan(hours, 0, 0)).ToString("yyyy-MM-dd hh:mm:ss tt");

                var sqlQuery = string.Format(
                    " SELECT Dataset_ID" +
                    " FROM T_Dataset_QC" +
                    " WHERE QCDM IS NULL AND" +
                    "       SMAQC_Job IS NOT NULL AND" +
                    "       (Quameter_Last_Affected >= '{0}' OR Last_Affected >= '{0}')", timestampThreshold);

                var dbTools = DbToolsFactory.GetDBTools(connectionString);

                var cmd = dbTools.CreateCommand(sqlQuery);

                var success = dbTools.GetQueryResultsDataTable(cmd, out var queryResults);

                if (!success)
                {
                    OnWarningEvent("Error obtaining data from v_annotation_type_picker using GetQueryResultsDataTable");
                    return new List<int>();
                }

                foreach (DataRow resultRow in queryResults.Rows)
                {
                    var datasetId = dbTools.GetInteger(resultRow[0]);

                    datasetIDs.Add(datasetId);
                }

                return datasetIDs;
            }
            catch (Exception ex)
            {
                OnErrorEvent("Exception in FindRecentNewDatasets", ex);
                return new List<int>();
            }
        }

        /// <summary>
        /// Reads the R Output file to look for errors
        /// </summary>
        /// <param name="scriptOutFilePath"></param>
        /// <returns>True if an error, false if no errors</returns>
        private bool ErrorReportedByR(string scriptOutFilePath)
        {
            if (!File.Exists(scriptOutFilePath))
                return false;

            using (var reader = new StreamReader(new FileStream(scriptOutFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                while (!reader.EndOfStream)
                {
                    var logText = reader.ReadLine();

                    if (string.IsNullOrEmpty(logText))
                        continue;

                    if (logText.Contains("there is no package called 'QCDM'") || logText.Contains("Execution halted"))
                    {
                        mErrorMessage = "Error with R: " + logText;
                        OnErrorEvent(mErrorMessage);

                        return true;
                    }

                    if (logText.StartsWith("Error in "))
                    {
                        mErrorMessage = "Error with R: " + logText;
                        OnErrorEvent(mErrorMessage);

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Parses the DatasetID list or range into a valid list of DatasetIDs
        /// </summary>
        /// <param name="datasetIDList">Dataset ID List or range; alternatively, a timespan in number of hours (e.g. 24h)</param>
        /// <param name="connectionString"></param>
        /// <param name="errorMessage">Output: Error message</param>
        /// <param name="processingTimespan">Output: set to true if processing a time span</param>
        /// <returns>True if success; false if an error or no dataset IDs</returns>
        public List<int> ParseDatasetIDList(string datasetIDList, string connectionString, out string errorMessage, out bool processingTimespan)
        {
            var connectionStringToUse = DbToolsFactory.AddApplicationNameToConnectionString(connectionString, "LLRC");

            var datasetIDs = new List<int>();
            int value;

            datasetIDList = datasetIDList.Trim();
            errorMessage = string.Empty;
            processingTimespan = false;

            if (string.IsNullOrWhiteSpace(datasetIDList))
            {
                errorMessage = "datasetIDList is empty";
                return new List<int>();
            }

            if (datasetIDList.IndexOf(',') > 0)
            {
                // Split datasetIDList on commas
                foreach (var datasetID in datasetIDList.Split(','))
                {
                    if (int.TryParse(datasetID, out value))
                    {
                        datasetIDs.Add(value);
                    }
                }
                return datasetIDs;
            }

            if (datasetIDList.IndexOf('-') > 0)
            {
                // Split datasetIDList on the dash
                var datasetIDText = datasetIDList.Split('-').ToList();

                if (datasetIDText.Count != 2)
                {
                    errorMessage = "DatasetIDList contains a dash but does not contain two numbers separated by a dash: " + datasetIDList;
                    return new List<int>();
                }

                int datasetIDStart;
                int datasetIDEnd;

                if (int.TryParse(datasetIDText[0], out value))
                {
                    datasetIDStart = value;
                }
                else
                {
                    errorMessage = "Text before the dash is not an integer: " + datasetIDList;
                    return new List<int>();
                }

                if (int.TryParse(datasetIDText[1], out value))
                {
                    datasetIDEnd = value;
                }
                else
                {
                    errorMessage = "Text after the dash is not an integer: " + datasetIDList;
                    return new List<int>();
                }

                if (datasetIDStart > datasetIDEnd)
                {
                    errorMessage = "Invalid dataset range, first value is larger than the second value: " + datasetIDList;
                    return new List<int>();
                }

                datasetIDs.AddRange(Enumerable.Range(datasetIDStart, datasetIDEnd - datasetIDStart + 1));

                return datasetIDs;
            }

            if (datasetIDList.EndsWith("h"))
            {
                // Timespan
                if (int.TryParse(datasetIDList.Substring(0, datasetIDList.Length - 1), out var hours))
                {
                    datasetIDs = FindRecentNewDatasets(hours, connectionStringToUse);
                    processingTimespan = true;

                    if (datasetIDs.Count == 0)
                        errorMessage = NO_NEW_RECENT_DATASETS + " " + hours + " hours";

                    return datasetIDs;
                }

                errorMessage = "Timespan must be of the form \"24h\" or similar: " + datasetIDList;
                return new List<int>();
            }

            if (int.TryParse(datasetIDList, out value))
            {
                datasetIDs.Add(value);
                return datasetIDs;
            }

            errorMessage = "DatasetIDList must contain an integer, a list of integers, a range of integers, or a number of hours: " + datasetIDList;
            return new List<int>();
        }

        /// <summary>
        /// Processes the dataset IDs in datasetIDs
        /// </summary>
        /// <param name="datasetIDs"></param>
        /// <remarks>Use property ErrorMessage to view any error messages</remarks>
        /// <returns>True if success, otherwise false</returns>
        public bool ProcessDatasets(List<int> datasetIDs)
        {
            try
            {
                // Validate that required files are present
                var requiredFiles = new List<string>
                {
                    RDATA_FILE_MODELS,
                    RDATA_FILE_ALLDATA
                };

                foreach (var filename in requiredFiles)
                {
                    if (!File.Exists(Path.Combine(WorkingDirectory, filename)))
                    {
                        mErrorMessage = "Required input file not found: " + filename + " at " + WorkingDirectory;

                        OnErrorEvent(mErrorMessage);
                        return false;
                    }
                }

                // Open the database
                // Get the data from the database about the dataset IDs
                var db = new DatabaseManager(mConnectionString);
                RegisterEvents(db);

                var metricsByDataset = db.GetData(datasetIDs, SkipAlreadyProcessedDatasets);

                // Checks to see if we have any datasets
                if (metricsByDataset.Count == 0)
                {
                    mErrorMessage = "No Metrics were found for the given Datasets IDs";
                    OnErrorEvent(mErrorMessage);

                    if (ProcessingTimespan)
                        return true;

                    return false;
                }

                // Deletes Old files so they don't interfere with new ones
                // Writes the data.csv file from the data gathered from database
                // Writes the R file and the batch file to run it
                var wf = new WriteFiles();
                RegisterEvents(wf);

                if (!string.IsNullOrWhiteSpace(wf.ErrorMessage))
                {
                    mErrorMessage = "Error determining the path to R.exe: " + wf.ErrorMessage;
                    OnErrorEvent(mErrorMessage);
                    return false;
                }

                wf.DeleteFiles(WorkingDirectory);
                var validDatasetIDs = wf.WriteCsv(metricsByDataset, WorkingDirectory);

                wf.WriteRFile(WorkingDirectory);
                wf.WriteBatch(WorkingDirectory);

                if (validDatasetIDs.Count == 0)
                {
                    if (metricsByDataset.Count == 1)
                        mErrorMessage = "DatasetID " + datasetIDs[0] + " was missing 1 or more required metrics; unable to run LLRC";
                    else
                        mErrorMessage = "All of the datasets were missing 1 or more required metrics; unable to run LLRC";

                    OnErrorEvent(mErrorMessage);

                    return ProcessingTimespan;
                }

                var datasetIdColumnIndex = wf.ColumnIndexMap[DatabaseManager.MetricColumns.Dataset_ID];

                var success = RunLLRC(WorkingDirectory, validDatasetIDs.Count);

                if (!success)
                {
                    // mErrorMessage should have a description of the error
                    return false;
                }

                if (!PostToDB)
                {
                    // Display the results
                    var post = new Posting(mConnectionString);
                    RegisterEvents(post);

                    var qcdmResults = post.LoadQCDMResults(WorkingDirectory, datasetIdColumnIndex);
                    var datasetCountDisplayed = 0;

                    // Display results for the first 10 datasets
                    Console.WriteLine();
                    OnStatusEvent("Results:");

                    foreach (var item in qcdmResults)
                    {
                        OnStatusEvent("DatasetID " + item.Key + ": " + item.Value);
                        datasetCountDisplayed++;

                        if (datasetCountDisplayed >= MaxResultsToDisplay)
                        {
                            OnStatusEvent("Results for " + (qcdmResults.Count - datasetCountDisplayed) + " additional datasets not displayed");
                            break;
                        }
                    }
                }
                else
                {
                    // Post to the database
                    // Allow for up to 2 retries

                    var retry = 3;

                    while (retry > 0)
                    {
                        // Wait 300 milliseconds to let R close
                        PRISM.AppUtils.SleepMilliseconds(333);
                        PRISM.AppUtils.GarbageCollectNow();

                        var post = new Posting(mConnectionString);

                        success = post.PostToDatabase(metricsByDataset, validDatasetIDs, WorkingDirectory, datasetIdColumnIndex);

                        if (success)
                        {
                            retry = 0;
                        }
                        else
                        {
                            retry--;

                            if (post.Errors.Count == 0)
                            {
                                mErrorMessage = "Unknown error posting results to the database";
                            }
                            else
                            {
                                foreach (var error in post.Errors)
                                {
                                    if (string.IsNullOrEmpty(mErrorMessage))
                                        mErrorMessage = string.Copy(error);
                                    else
                                        mErrorMessage += "; " + error;
                                }

                                OnErrorEvent("Error in ProcessDatasets (retry={0}): {1}", retry, mErrorMessage);
                            }
                        }

                        if (post.BadDatasetIDs.Count == 0)
                            continue;

                        var badDatasetIDs = new StringBuilder();

                        foreach (var datasetID in post.BadDatasetIDs)
                        {
                            if (badDatasetIDs.Length > 0)
                                badDatasetIDs.Append(", ");
                            badDatasetIDs.Append(datasetID);
                        }

                        OnWarningEvent("Dataset IDs for which LLRC could not compute a QCDM result: " + badDatasetIDs);
                    }

                    if (!success)
                        return false;
                }
            }

            // Displays errors if any occur
            catch (Exception ex)
            {
                OnErrorEvent("Exception in ProcessDatasets", ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Starts R to perform the processing
        /// </summary>
        /// <param name="WorkingDirPath"></param>
        /// <param name="datasetCount"></param>
        /// <returns>True if success, false if an error</returns>
        private bool RunLLRC(string WorkingDirPath, int datasetCount)
        {
            var appDirectoryPath = PRISM.AppUtils.GetAppDirectoryPath();

            // Runs the batch program
            var p = new System.Diagnostics.Process
            {
                StartInfo = { FileName = Path.Combine(WorkingDirPath, "RunR.bat") }
            };
            p.Start();

            var scriptOutFilePath = Path.Combine(appDirectoryPath, "QCDMscript.r.Rout");

            // Note that this results file must be named TestingDataset.csv
            var resultsFile = new FileInfo(Path.Combine(WorkingDirPath, "TestingDataset.csv"));
            var abortProcessing = false;

            Console.WriteLine();
            OnStatusEvent("Starting R to compute LLRC for " + datasetCount + " dataset" + (datasetCount > 1 ? "s" : ""));

            var sleepTimeMsec = 500;

            // Checks to see if the files have been made
            while (!p.HasExited && !resultsFile.Exists)
            {
                PRISM.AppUtils.SleepMilliseconds(sleepTimeMsec);

                if (ErrorReportedByR(scriptOutFilePath))
                {
                    abortProcessing = true;
                    break;
                }

                resultsFile.Refresh();

                if (sleepTimeMsec < 4000)
                    sleepTimeMsec *= 2;
            }

            if (abortProcessing)
            {
                if (!p.HasExited)
                    p.Kill();

                if (string.IsNullOrEmpty(mErrorMessage))
                {
                    mErrorMessage = "Unknown error running R";
                    OnErrorEvent(mErrorMessage);
                }

                return false;
            }

            resultsFile.Refresh();

            if (!resultsFile.Exists)
            {
                mErrorMessage = "R exited without error, but the results file does not exist: " + resultsFile.Name + " at " + resultsFile.FullName;
                OnErrorEvent(mErrorMessage);
                return false;
            }

            OnStatusEvent("  LLRC computation complete");

            return true;
        }
    }
}
