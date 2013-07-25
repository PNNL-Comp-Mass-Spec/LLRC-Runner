using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace QCDMWrapper
{
    internal class Program
    {
        public static string Connection = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";
        public static string Storedpro = "StoreQCDMResults";
        public static string Fileloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\";

        private static int Main(string[] args)
        {
            try
            {
                // Checking the input data to make sure it is okay
                // Also getting the size and orginizing the dataset Ids
				InputCheck inp = new InputCheck();
				string outputFolderPath = inp.CmdInput(args);

				List<int> datasetIDs = inp.DatasetIDs;

				if (datasetIDs.Count == 0)
				{
					// Dataset IDs not defined
					// The user should have already been notified
					return 1;
				}

				// Validate that required files are present
				List<string> lstRequiredFiles = new List<string>();
				lstRequiredFiles.Add("Models_paper.Rdata");
				lstRequiredFiles.Add("allData_v3.Rdata");

				foreach (string filename in lstRequiredFiles)
				{
					if (!File.Exists(Path.Combine(outputFolderPath, filename)))
					{
						ShowErrorMessage("Error, required input file not found: " + filename + " at\n  " + outputFolderPath, true);
						return 2;
					}
				}

                // Open the database
                // Get the data from the database about the dataset Ids
				DatabaseMang db = new DatabaseMang();
                db.Open();

				List<List<string>> lstMetricsByDataset = db.GetData(datasetIDs);

                //Checks to see if we have any datasets
                if (lstMetricsByDataset.Count == 0)
                {
                    ShowErrorMessage("No Datasets were found", true);
                    return 3;
                }

                //Deletes Old files so they dont interfere with new ones
                //Writes the data.csv file from the data gathered from database
                //Writes the R file and the batch file to run it
                WriteFiles wf = new WriteFiles();
				wf.DeleteFiles(outputFolderPath);
				wf.WriteCsv(lstMetricsByDataset, outputFolderPath, db.MetricCount);
				wf.WriteRFile(outputFolderPath);
				wf.WriteBatch(outputFolderPath);

				bool success = RunLLRC(outputFolderPath);

				if (!success)
				{
					System.Threading.Thread.Sleep(1500);
					return 4;
				}
            
				//Posts the data to the database
                var post = new Posting();
				post.PostToDatabase(lstMetricsByDataset, outputFolderPath);

                // Close connection
                db.Close();                                
            }

            //Displays errors if any occur
            catch (Exception e)
            {
				ShowErrorMessage("Error occurred; details:", false);
                Console.WriteLine(e);
				System.Threading.Thread.Sleep(1500);
                return 5;
            }

			return 0;
        }

		/// <summary>
		/// Starts R to perform the processing
		/// </summary>
		/// <param name="outputFolderPath"></param>
		/// <returns>True if success, false if an error</returns>
		static bool RunLLRC(string outputFolderPath)
		{
			//Runs the batch program
			Process p = new Process();
			p.StartInfo.FileName = Path.Combine(outputFolderPath, "RunR.bat");
			p.Start();

			string scriptOutFilePath = Path.Combine(Fileloc, "QCDMscript.r.Rout");

			// Note that this results file must be named TestingDataset.csv
			FileInfo fiResultsFile = new FileInfo(Path.Combine(outputFolderPath, "TestingDataset.csv"));
			bool bAbort = false;

			Console.WriteLine("Starting R to compute LLRC");

			int sleepTimeMsec = 500;

			// Checks to see if the files have been made
			while (!p.HasExited && !fiResultsFile.Exists)
			{
				System.Threading.Thread.Sleep(sleepTimeMsec);

				if (ErrorReportedByR(scriptOutFilePath))
				{
					bAbort = true;
					break;
				}
				fiResultsFile.Refresh();

				if (sleepTimeMsec < 4000)
					sleepTimeMsec *= 2;
			}

			if (bAbort)
			{
				if (!p.HasExited)
					p.Kill();

				return false;
			}

			fiResultsFile.Refresh();
			if (!fiResultsFile.Exists)
			{
				ShowErrorMessage("R exited without error, but the results file does not exist: " + fiResultsFile.Name + " at\n  " + fiResultsFile.FullName, true);
				return false;
			}

			return true;
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
		/// <summary>
		/// Reads the R Output file to look for errors
		/// </summary>
		/// <param name="scriptOutFilePath"></param>
		/// <returns>True if an error, false if no errors</returns>
		static bool ErrorReportedByR(string scriptOutFilePath)
		{
			if (File.Exists(scriptOutFilePath))
			{

				using (StreamReader srOutFile = new StreamReader(new FileStream(scriptOutFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
				{
					while (srOutFile.Peek() > -1)
					{
						string logText = srOutFile.ReadLine();
						if (logText.Contains("there is no package called 'QCDM'") || logText.Contains("Execution halted"))
						{
							ShowErrorMessage("Error with R: " + logText, false);
							return true;
						}

						if (logText.StartsWith("Error in "))
						{
							ShowErrorMessage("Error with R: " + logText, false);
							return true;
						}

					}
				}

			}

			return false;
		}
    }
}