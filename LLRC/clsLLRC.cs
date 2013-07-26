using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LLRC
{
	public class LLRCWrapper
	{
		public const string PROGRAM_DATE = "July 26, 2013";

		public const string RDATA_FILE_MODELS = "Models_paper.Rdata";
		public const string RDATA_FILE_ALLDATA = "allData_v3.Rdata";

		protected string mConnectionString;
		protected string mWorkingDirPath;
		protected bool mPostToDB;

		protected string mErrorMessage;

		#region "Properties"
		public string ErrorMessage
		{
			get
			{
				return mErrorMessage;
			}
		}

		public bool PostToDB
		{
			get
			{
				return mPostToDB;
			}
			set
			{
				mPostToDB = value;
			}
		}

		public string WorkingDirectory
		{
			get
			{
				return mWorkingDirPath;
			}
			set
			{
				mWorkingDirPath = value;
			}
		}
		#endregion

		/// <summary>
		/// Constructor
		/// </summary>
		public LLRCWrapper()
			: this(DatabaseMang.DEFAULT_CONNECTION_STRING)
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public LLRCWrapper(string connectionString)
		{
			mErrorMessage = string.Empty;

			if (string.IsNullOrEmpty(connectionString))
				mConnectionString = DatabaseMang.DEFAULT_CONNECTION_STRING;
			else
				mConnectionString = connectionString;

			mWorkingDirPath = GetAppFolderPath();
		}

		/// <summary>
		/// Returns the full path to the folder that contains the currently executing .Exe or .Dll
		/// </summary>
		/// <returns></returns>
		public string GetAppFolderPath()
		{
			// Could use Application.StartupPath, but .GetExecutingAssembly is better
			return System.IO.Path.GetDirectoryName(GetAppPath());
		}

		/// <summary>
		/// Returns the full path to the executing .Exe or .Dll
		/// </summary>
		/// <returns>File path</returns>
		/// <remarks></remarks>
		public string GetAppPath()
		{
			return System.Reflection.Assembly.GetExecutingAssembly().Location;
		}

		/// <summary>
		/// Extracts the DatasetID value from ColIndex 1 in metricsOneDataset
		/// </summary>
		/// <param name="metricsOneDataset"></param>
		/// <returns>The DatasetID as an integer, or 0 if an error</returns>
		public static int GetDatasetIdForMetricRow(List<string> metricsOneDataset)
		{
			int datasetID;

			if (metricsOneDataset == null || metricsOneDataset.Count < DatabaseMang.MetricColumnIndex.DatasetID)
			{
				Console.WriteLine("GetDatasetIdForMetricRow: metricsOneDataset is invalid");
				return 0;
			}

			if (!int.TryParse(metricsOneDataset[DatabaseMang.MetricColumnIndex.DatasetID], out datasetID))
			{
				Console.WriteLine("GetDatasetIdForMetricRow: Dataset ID is not an integer; this is unexpected");
				return 0;
			}

			return datasetID;
		}
			

		/// <summary>
		/// Reads the R Output file to look for errors
		/// </summary>
		/// <param name="scriptOutFilePath"></param>
		/// <returns>True if an error, false if no errors</returns>
		protected bool ErrorReportedByR(string scriptOutFilePath)
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
							mErrorMessage = "Error with R: " + logText;
							return true;
						}

						if (logText.StartsWith("Error in "))
						{
							mErrorMessage = "Error with R: " + logText;
							return true;
						}

					}
				}

			}

			return false;
		}



		/// <summary>
		/// Parses the DatasetID list or range into a valid list of DatasetIDs
		/// </summary>
		/// <returns>True if success; false if an error or no Dataset IDs</returns>
		public static List<int> ParseDatasetIDList(string datasetIDList, out string errorMessage)
		{
			List<int> lstDatasetIDs = new List<int>();
			int value;

			errorMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(datasetIDList))
			{
				errorMessage = "datasetIDList is empty";
				return new List<int>();
			}


			if (datasetIDList.IndexOf(',') > 0)
			{
				// Split datasetIDList on commas
				foreach (string datasetID in datasetIDList.Split(','))
				{
					if (int.TryParse(datasetID, out value))
					{
						lstDatasetIDs.Add(value);
					}
				}
				return lstDatasetIDs;
			}

			if (datasetIDList.IndexOf('-') > 0)
			{
				// Split datasetIDList on the dash
				List<string> lstDatasetIDText = datasetIDList.Split('-').ToList<string>();

				if (lstDatasetIDText.Count != 2)
				{
					errorMessage = "DatasetIDList contains a dash but does not contain two numbers separated by a dash: " + datasetIDList;
					return new List<int>();
				}

				int datasetIDStart;
				int datasetIDEnd;

				if (int.TryParse(lstDatasetIDText[0], out value))
				{
					datasetIDStart = value;
				}
				else
				{
					errorMessage = "Text before the dash is not an integer: " + datasetIDList;
					return new List<int>();
				}

				if (int.TryParse(lstDatasetIDText[1], out value))
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

				lstDatasetIDs.AddRange(System.Linq.Enumerable.Range(datasetIDStart, datasetIDEnd - datasetIDStart));
				return lstDatasetIDs;
			}

			if (int.TryParse(datasetIDList, out value))
			{
				lstDatasetIDs.Add(value);
				return lstDatasetIDs;
			}
			else
			{
				errorMessage = "DatasetIDList must contain an integer, a list of integers, or a range of integers: " + datasetIDList;
				return new List<int>();
			}

		}
       
		/// <summary>
		/// Processes the Dataset IDs in lstDatasetIDs
		/// </summary>
		/// <param name="lstDatasetIDs"></param>
		/// <returns>True if success, otherwise false</returns>
		/// <remarks>Use property ErrorMessage to view any error messages</remarks>
		public bool ProcessDatasets(List<int> lstDatasetIDs)
		{
			try
			{

				// Validate that required files are present
				List<string> lstRequiredFiles = new List<string>();
				lstRequiredFiles.Add(RDATA_FILE_MODELS);
				lstRequiredFiles.Add(RDATA_FILE_ALLDATA);

				foreach (string filename in lstRequiredFiles)
				{
					if (!File.Exists(Path.Combine(mWorkingDirPath, filename)))
					{
						mErrorMessage = "Required input file not found: " + filename + " at " + mWorkingDirPath;
						return false;
					}
				}

				// Open the database
				// Get the data from the database about the dataset Ids
				DatabaseMang db = new DatabaseMang();

				List<List<string>> lstMetricsByDataset = db.GetData(lstDatasetIDs);
			


				//Checks to see if we have any datasets
				if (lstMetricsByDataset.Count == 0)
				{
					mErrorMessage = "No Metrics were found for the given Datasets IDs";
					return false;
				}

				//Deletes Old files so they dont interfere with new ones
				//Writes the data.csv file from the data gathered from database
				//Writes the R file and the batch file to run it
				WriteFiles wf = new WriteFiles();
				wf.DeleteFiles(mWorkingDirPath);
				SortedSet<int> lstValidDatasetIDs = wf.WriteCsv(lstMetricsByDataset, mWorkingDirPath);

				wf.WriteRFile(mWorkingDirPath);
				wf.WriteBatch(mWorkingDirPath);

				if (lstValidDatasetIDs.Count == 0)
				{
					if (lstMetricsByDataset.Count == 1)
						mErrorMessage = "DatasetID " + lstDatasetIDs[0] + " was missing 1 or more required metrics; unable to run LLRC";
					else
						mErrorMessage = "All of the datasets were missing 1 or more required metrics; unable to run LLRC";

					return false;
				}

				bool success = RunLLRC(mWorkingDirPath, lstValidDatasetIDs.Count);

				if (!success)
				{
					// mErrorMessage should have a description of the error
					return false;
				}

				if (mPostToDB)
				{
					//Posts the data to the database
					Posting post = new Posting(mConnectionString);
					success = post.PostToDatabase(lstMetricsByDataset, lstValidDatasetIDs, mWorkingDirPath);

					if (!success)
					{
						if (post.Errors.Count == 0)
						{
							mErrorMessage = "Unknown error posting results to the database";
						}
						else
						{
							foreach (string error in post.Errors)
							{
								if (string.IsNullOrEmpty(mErrorMessage))
									mErrorMessage = string.Copy(error);
								else
									mErrorMessage += "; " + error;
							}
						}

						return false;
					}
						
				}

			}

			//Displays errors if any occur
			catch (Exception e)
			{
				mErrorMessage = "Error processing the datasets: " + e.Message;
				return false;
			}

			return true;
		}

		/// <summary>
		/// Starts R to perform the processing
		/// </summary>
		/// <param name="WorkingDirPath"></param>
		/// <returns>True if success, false if an error</returns>
		protected bool RunLLRC(string WorkingDirPath, int datasetCount)
		{

			string appFolderPath = GetAppFolderPath();

			//Runs the batch program
			System.Diagnostics.Process p = new System.Diagnostics.Process();
			p.StartInfo.FileName = Path.Combine(WorkingDirPath, "RunR.bat");
			p.Start();

			string scriptOutFilePath = Path.Combine(appFolderPath, "QCDMscript.r.Rout");

			// Note that this results file must be named TestingDataset.csv
			FileInfo fiResultsFile = new FileInfo(Path.Combine(WorkingDirPath, "TestingDataset.csv"));
			bool bAbort = false;

			Console.WriteLine("Starting R to compute LLRC for " + datasetCount + " dataset" + (datasetCount > 1 ? "s" : ""));

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

				if (string.IsNullOrEmpty(mErrorMessage))
					mErrorMessage = "Unknown error running R";

				return false;
			}

			fiResultsFile.Refresh();
			if (!fiResultsFile.Exists)
			{
				mErrorMessage = "R exited without error, but the results file does not exist: " + fiResultsFile.Name + " at " + fiResultsFile.FullName;
				return false;
			}
			else
			{
				Console.WriteLine("  LLRC computation complete");
			}

			return true;
		}

	}
}
