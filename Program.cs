using System;
using System.Collections.Generic;
using LLRC;
using PRISM;
using PRISM.Logging;

namespace LLRCRunner
{
    // This program computes the LLRC values for a given set of Smaqc and Quameter values
    //
    // -------------------------------------------------------------------------------
    // Written by Josh Davis and Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    //
    // E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov
    // Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    // -------------------------------------------------------------------------------
    //

    internal class Program
    {
        public const string PROGRAM_DATE = "January 25, 2023";

        protected const string CONNECTION_STRING = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

        protected static string mDatasetIDList;
        protected static string mWorkingDirectory;

        protected static int mMaxResultsToDisplay;      // Only used if mPostToDB is false
        protected static bool mPostToDB;

        protected static bool mSkipAlreadyProcessedDatasets;

        public static int Main(string[] args)
        {
            var commandLineParser = new clsParseCommandLine();

            mDatasetIDList = string.Empty;
            mWorkingDirectory = string.Empty;
            mMaxResultsToDisplay = 10;
            mPostToDB = false;
            mSkipAlreadyProcessedDatasets = false;

            try
            {
                var success = false;

                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        success = true;
                }

                if (!success ||
                    commandLineParser.NeedToShowHelp ||
                    commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount == 0 ||
                    mDatasetIDList.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;
                }


                var processingClass = new LLRCWrapper(CONNECTION_STRING)
                {
                    MaxResultsToDisplay = mMaxResultsToDisplay,
                    PostToDB = mPostToDB,
                    SkipAlreadyProcessedDatasets = mSkipAlreadyProcessedDatasets
                };

                RegisterEvents(processingClass);


                // Parse the dataset ID list
                var datasetIDs = processingClass.ParseDatasetIDList(mDatasetIDList, CONNECTION_STRING, out var errorMessage, out var processingTimespan);

                processingClass.ProcessingTimespan = processingTimespan;

                if (datasetIDs.Count == 0)
                {
                    if (errorMessage.StartsWith(LLRCWrapper.NO_NEW_RECENT_DATASETS))
                    {
                        // No new, recent datasets
                        // This is not a critical error
                        OnWarningEvent(errorMessage);
                        return 0;
                    }

                    // Dataset IDs not defined
                    OnErrorEvent(errorMessage);
                    ShowProgramHelp();
                    return -2;
                }

                if (!string.IsNullOrWhiteSpace(mWorkingDirectory))
                    processingClass.WorkingDirectory = mWorkingDirectory;

                success = processingClass.ProcessDatasets(datasetIDs);

                if (!success)
                {
                    if (processingClass.ErrorMessage.StartsWith("Error processing the datasets"))
                        OnErrorEvent(processingClass.ErrorMessage);
                    else
                        OnErrorEvent("Error processing the datasets: " + processingClass.ErrorMessage);

                    return -3;
                }
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error occurred in Program->Main: " + ex.Message, ex);
                return -1;
            }

            return 0;
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            // Returns True if no problems; otherwise, returns false

            var validParameters = new List<string> { "I", "W", "DB", "Skip", "Display" };

            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(validParameters))
                {
                    var badArguments = new List<string>();
                    foreach (var item in commandLineParser.InvalidParameters(validParameters))
                    {
                        badArguments.Add("/" + item);
                    }

                    OnErrorEvent("Invalid command line parameters", badArguments);

                    return false;
                }

                // Query commandLineParser to see if various parameters are present
                if (commandLineParser.RetrieveValueForParameter("I", out var datasetIDs))
                {
                    mDatasetIDList = string.Copy(datasetIDs);
                }
                else if (commandLineParser.NonSwitchParameterCount > 0)
                {
                    mDatasetIDList = commandLineParser.RetrieveNonSwitchParameter(0);
                }

                if (commandLineParser.RetrieveValueForParameter("W", out var workingDirectory))
                {
                    mWorkingDirectory = string.Copy(workingDirectory);
                }

                if (commandLineParser.IsParameterPresent("DB"))
                {
                    mPostToDB = true;
                }

                if (commandLineParser.IsParameterPresent("Skip"))
                {
                    mSkipAlreadyProcessedDatasets = true;
                }

                if (commandLineParser.RetrieveValueForParameter("Display", out var maxResultsToDisplay))
                {
                    if (int.TryParse(maxResultsToDisplay, out var maxResults))
                        mMaxResultsToDisplay = maxResults;
                }

                return true;

            }
            catch (Exception ex)
            {
                OnErrorEvent("Error parsing the command line parameters: " + ex.Message, ex);
            }

            return false;
        }

        private static void ShowProgramHelp()
        {
            var exeName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            try
            {
                Console.WriteLine();
                Console.WriteLine("This program uses LLRC to compute the QCDM value using QC Metric values from Quameter and Smaqc.");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + Environment.NewLine + exeName);

                Console.WriteLine(" DatasetIDList [/W:WorkingDirectory] [/DB] [/Skip]");

                Console.WriteLine();
                Console.WriteLine("DatasetIDList can be a single DatasetID, a list of DatasetIDs separated by commas, a range of DatasetIDs separated with a dash, or a timespan in hours.  Examples:");
                Console.WriteLine(" " + exeName + " 325145                 (will process 1 dataset)");
                Console.WriteLine(" " + exeName + " 325145,325146,325150   (will process 3 datasets)");
                Console.WriteLine(" " + exeName + " 325145-325150          (will process 6 dataset)");
                Console.WriteLine(" " + exeName + " 24h                    (will process all new datasets created in the last 24 hours)");
                Console.WriteLine();
                Console.WriteLine("Use /W to specify the working directory path; default is the folder with the .exe");
                Console.WriteLine("The working directory must have files " + LLRCWrapper.RDATA_FILE_MODELS + " and " + LLRCWrapper.RDATA_FILE_ALLDATA);
                Console.WriteLine();
                Console.WriteLine("Use /DB to post the LLRC results to the database");
                Console.WriteLine();
                Console.WriteLine("Use /Skip to skip datasets that already have a QCDM value defined");
                Console.WriteLine();
                Console.WriteLine("\"New\" datasets are those with null QCDM values in the T_Dataset_QC table");

                Console.WriteLine();
                Console.WriteLine("Program written by Joshua Davis and Matthew Monroe for the Department of Energy (PNNL, Richland, WA)");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);
            }
            catch (Exception ex)
            {
                OnErrorEvent("Error displaying the program syntax: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Use this method to chain events between classes
        /// </summary>
        /// <param name="sourceClass"></param>
        private static void RegisterEvents(IEventNotifier sourceClass)
        {
            // Ignore: sourceClass.DebugEvent += OnDebugEvent;
            sourceClass.StatusEvent += OnStatusEvent;
            sourceClass.ErrorEvent += OnErrorEvent;
            sourceClass.WarningEvent += OnWarningEvent;
            // Ignore: sourceClass.ProgressUpdate += OnProgressUpdate;
        }

        private static void OnErrorEvent(string format, params object[] args)
        {
            ConsoleMsgUtils.ShowError(format, args);
        }

        private static void OnErrorEvent(string message, Exception ex)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void OnStatusEvent(string message)
        {
            Console.WriteLine(message);
        }

        private static void OnWarningEvent(string message)
        {
            ConsoleMsgUtils.ShowWarning(message);
        }
    }
}
