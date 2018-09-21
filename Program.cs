using System;
using System.Collections.Generic;
using LLRC;
using PRISM;

namespace LLRCRunner
{
    // This program computes the LLRC values for a given set of Smaqc and Quameter values
    //
    // -------------------------------------------------------------------------------
    // Written by Josh Davis and Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    //
    // E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com
    // Website: http://panomics.pnnl.gov/ or http://omics.pnl.gov or http://www.sysbio.org/resources/staff/
    // -------------------------------------------------------------------------------
    // 

    internal class Program
    {

        public const string PROGRAM_DATE = "January 6, 2016";

        protected const string CONNECTION_STRING = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

        protected static string mDatasetIDList;
        protected static string mWorkingDirectory;

        protected static int mMaxResultsToDisplay;      // Only used if mPostToDB is false
        protected static bool mPostToDB;

        protected static bool mSkipAlreadyProcessedDatasets;

        public static int Main(string[] args)
        {
            var objParseCommandLine = new clsParseCommandLine();

            mDatasetIDList = string.Empty;
            mWorkingDirectory = string.Empty;
            mMaxResultsToDisplay = 10;
            mPostToDB = false;
            mSkipAlreadyProcessedDatasets = false;

            try
            {
                var success = false;

                if (objParseCommandLine.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(objParseCommandLine))
                        success = true;
                }

                if (!success ||
                    objParseCommandLine.NeedToShowHelp ||
                    objParseCommandLine.ParameterCount + objParseCommandLine.NonSwitchParameterCount == 0 ||
                    mDatasetIDList.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;

                }

                // Parse the dataset ID list
                var lstDatasetIDs = LLRCWrapper.ParseDatasetIDList(mDatasetIDList, CONNECTION_STRING, out var errorMessage, out var processingTimespan);

                if (lstDatasetIDs.Count == 0)
                {
                    if (errorMessage.StartsWith(LLRCWrapper.NO_NEW_RECENT_DATASETS))
                    {
                        // No new, recent datasets
                        // This is not a critical error
                        Console.WriteLine(errorMessage);
                        return 0;
                    }

                    // Dataset IDs not defined
                    ShowErrorMessage(errorMessage);
                    ShowProgramHelp();
                    return -2;
                }

                var oProcessingClass = new LLRCWrapper
                {
                    MaxResultsToDisplay = mMaxResultsToDisplay,
                    PostToDB = mPostToDB,
                    SkipAlreadyProcessedDatasets = mSkipAlreadyProcessedDatasets,
                    ProcessingTimespan = processingTimespan
                };

                if (!string.IsNullOrWhiteSpace(mWorkingDirectory))
                    oProcessingClass.WorkingDirectory = mWorkingDirectory;

                success = oProcessingClass.ProcessDatasets(lstDatasetIDs);

                if (!success)
                {
                    if (oProcessingClass.ErrorMessage.StartsWith("Error processing the datasets"))
                        ShowErrorMessage(oProcessingClass.ErrorMessage);
                    else
                        ShowErrorMessage("Error processing the datasets: " + oProcessingClass.ErrorMessage);

                    return -3;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error occurred in Program->Main: " + ex.Message, ex);
                return -1;
            }

            return 0;
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine objParseCommandLine)
        {
            // Returns True if no problems; otherwise, returns false

            var lstValidParameters = new List<string> { "I", "W", "DB", "Skip", "Display" };

            try
            {
                // Make sure no invalid parameters are present
                if (objParseCommandLine.InvalidParametersPresent(lstValidParameters))
                {
                    var badArguments = new List<string>();
                    foreach (var item in objParseCommandLine.InvalidParameters(lstValidParameters))
                    {
                        badArguments.Add("/" + item);
                    }

                    ShowErrorMessage("Invalid command line parameters", badArguments);

                    return false;
                }

                // Query objParseCommandLine to see if various parameters are present
                if (objParseCommandLine.RetrieveValueForParameter("I", out var strValue))
                {
                    mDatasetIDList = string.Copy(strValue);
                }
                else if (objParseCommandLine.NonSwitchParameterCount > 0)
                {
                    mDatasetIDList = objParseCommandLine.RetrieveNonSwitchParameter(0);
                }

                if (objParseCommandLine.RetrieveValueForParameter("W", out strValue))
                {
                    mWorkingDirectory = string.Copy(strValue);
                }

                if (objParseCommandLine.IsParameterPresent("DB"))
                {
                    mPostToDB = true;
                }

                if (objParseCommandLine.IsParameterPresent("Skip"))
                {
                    mSkipAlreadyProcessedDatasets = true;
                }

                if (objParseCommandLine.RetrieveValueForParameter("Display", out strValue))
                {
                    if (int.TryParse(strValue, out var intValue))
                        mMaxResultsToDisplay = intValue;
                }

                return true;


            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + ex.Message, ex);
            }

            return false;
        }

        private static void ShowErrorMessage(string message, Exception ex = null)
        {
            ConsoleMsgUtils.ShowError(message, ex);
        }

        private static void ShowErrorMessage(string title, IEnumerable<string> errorMessages)
        {
            ConsoleMsgUtils.ShowErrors(title, errorMessages);
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
                Console.WriteLine("Program written by Joshua Davis and Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2013");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error displaying the program syntax: " + ex.Message, ex);
            }

        }

    }
}
