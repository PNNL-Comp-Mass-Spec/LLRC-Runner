using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

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

		public const string PROGRAM_DATE = "July 26, 2013";

		protected const string CONNECTION_STRING = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";

		protected static string mDatasetIDList;
		protected static string mWorkingDirectory;
		protected static bool mPostToDB;

        public static int Main(string[] args)
        {
			FileProcessor.clsParseCommandLine objParseCommandLine = new FileProcessor.clsParseCommandLine();
			bool success = false;

			mDatasetIDList = string.Empty;
			mWorkingDirectory = string.Empty;
			mPostToDB = false;

            try
            {
				success = false;

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
				else 
				{
					string errorMessage;

					// Parse the dataset ID list
					List<int> lstDatasetIDs = LLRC.LLRCWrapper.ParseDatasetIDList(mDatasetIDList, out errorMessage);

					if (lstDatasetIDs.Count == 0)
					{
						// Dataset IDs not defined
						ShowErrorMessage(errorMessage);
						ShowProgramHelp();
						return -2;
					}

					LLRC.LLRCWrapper oProcessingClass = new LLRC.LLRCWrapper();

					oProcessingClass.PostToDB = mPostToDB;
					if (!string.IsNullOrWhiteSpace(mWorkingDirectory))
						oProcessingClass.WorkingDirectory = mWorkingDirectory;

					success = oProcessingClass.ProcessDatasets(lstDatasetIDs);

					if (!success)
					{
						ShowErrorMessage("Error processing the datasets: " + oProcessingClass.ErrorMessage);
						return -3;
					}
				}

			}
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Program->Main: " + Environment.NewLine + ex.Message);
                return -1;
            }

            return 0;
		}

		private static string GetAppVersion()
		{
			return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (" + PROGRAM_DATE + ")";
		}

		private static bool SetOptionsUsingCommandLineParameters(FileProcessor.clsParseCommandLine objParseCommandLine)
        {
            // Returns True if no problems; otherwise, returns false

            string strValue = string.Empty;
            List<string> lstValidParameters = new List<string> {"I", "W", "DB"};

            try
            {
                // Make sure no invalid parameters are present
                if (objParseCommandLine.InvalidParametersPresent(lstValidParameters))
                {					
					List<string> badArguments = new List<string>();
					foreach (string item in objParseCommandLine.InvalidParameters(lstValidParameters))
					{
						badArguments.Add("/" + item);
					}

					ShowErrorMessage("Invalid commmand line parameters", badArguments);
                
                    return false;
                }
                else
                {
                    {
                        // Query objParseCommandLine to see if various parameters are present						
                        if (objParseCommandLine.RetrieveValueForParameter("I", out strValue))
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

					
                    }

                    return true;
                }

            }
            catch (Exception ex)
            {
                ShowErrorMessage("Error parsing the command line parameters: " + Environment.NewLine + ex.Message);
            }

            return false;
        }

		
		private static void ShowErrorMessage(string strMessage)
		{
			string strSeparator = "------------------------------------------------------------------------------";

			Console.WriteLine();
			Console.WriteLine(strSeparator);
			Console.WriteLine(strMessage);
			Console.WriteLine(strSeparator);
			Console.WriteLine();

			WriteToErrorStream(strMessage);
		}

		private static void ShowErrorMessage(string strTitle, List<string> items)
		{
			string strSeparator = "------------------------------------------------------------------------------";
			string strMessage = null;

			Console.WriteLine();
			Console.WriteLine(strSeparator);
			Console.WriteLine(strTitle);
			strMessage = strTitle + ":";

			foreach (string item in items) {
				Console.WriteLine("   " + item);
				strMessage += " " + item;
			}
			Console.WriteLine(strSeparator);
			Console.WriteLine();

			WriteToErrorStream(strMessage);
		}


        private static void ShowProgramHelp()
        {
			string exeName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            try
            {
				Console.WriteLine();
                Console.WriteLine("This program uses LLRC to compute the QCDM value using QC Metric values from Quameter and Smaqc.");
                Console.WriteLine();
                Console.WriteLine("Program syntax:" + Environment.NewLine + exeName);

				Console.WriteLine(" DatasetIDList [/W:WorkingDirectory] [/DB]");

				Console.WriteLine();
				Console.WriteLine("DatasetIDList can be a single DatasetID, a list of DatasetIDs separated by commas, or a range of DatasetIDs separated with a dash.  Examples:");
				Console.WriteLine(" " + exeName + " 325145");
				Console.WriteLine(" " + exeName + " 325145,325146,325150");
				Console.WriteLine(" " + exeName + " 325145-325150");
			
				Console.WriteLine();
				Console.WriteLine("Use /W to specify the working directory path; default is the folder with the .exe");
				Console.WriteLine("The working directory must have files " + LLRC.LLRCWrapper.RDATA_FILE_MODELS + " and " + LLRC.LLRCWrapper.RDATA_FILE_ALLDATA);
				Console.WriteLine();
				Console.WriteLine("Use /DB to post the LLRC results to the database");
				
				Console.WriteLine();
                Console.WriteLine("Program written by Joshua Davis and Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2013");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();

                Console.WriteLine("E-mail: matthew.monroe@pnl.gov or matt@alchemistmatt.com");
                Console.WriteLine("Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying the program syntax: " + ex.Message);
            }

        }

		private static void WriteToErrorStream(string strErrorMessage)
		{
			try {
				using (System.IO.StreamWriter swErrorStream = new System.IO.StreamWriter(Console.OpenStandardError())) {
					swErrorStream.WriteLine(strErrorMessage);
				}
			} catch 
			{
				// Ignore errors here
			}
		}
		
		static void ShowErrorMessage(string message, bool pauseAfterError)
		{
			Console.WriteLine();
			Console.WriteLine("===============================================");

			Console.WriteLine(message);

			if (pauseAfterError)
			{
				Console.WriteLine("===============================================");
				System.Threading.Thread.Sleep(1500);
			}
		}

    }
}