using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SqlClient;
using System.Text;

namespace LLRC
{
	public class LLRCWrapper
	{
		public const string PROGRAM_DATE = "July 30, 2013";

		public const string RDATA_FILE_MODELS = "Models_paper.Rdata";
		public const string RDATA_FILE_ALLDATA = "allData_v4.Rdata";

		protected string mConnectionString;
		protected string mWorkingDirPath;

		protected int mMaxResultsToDisplay;			// Only used if mPostToDB is false
		protected bool mPostToDB;
		protected bool mProcessingTimespan;			// Set this to True if processing a set of DataseIDs from a timespan; when this is true, the code will not report an error if none of the datasets has valid metrics
		protected bool mSkipAlreadyProcessedDatasets;

		protected string mErrorMessage;

		#region "Properties"
		public string ErrorMessage
		{
			get
			{
				return mErrorMessage;
			}
		}

		public int MaxResultsToDisplay
		{
			get
			{
				return mMaxResultsToDisplay;
			}
			set
			{
				mMaxResultsToDisplay = value;
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
		public bool ProcessingTimespan
		{
			get
			{
				return mProcessingTimespan;
			}
			set
			{
				mProcessingTimespan = value;
			}
		}
		
		public bool SkipAlreadyProcessedDatasets
		{
			get
			{
				return mSkipAlreadyProcessedDatasets;
			}
			set
			{
				mSkipAlreadyProcessedDatasets = value;
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

			mMaxResultsToDisplay = 10;
			mPostToDB = false;
			ProcessingTimespan = false;
			mSkipAlreadyProcessedDatasets = false;
		}

		/// <summary>
		/// Looks for datasets with entries in T_Dataset_QC where Quameter_Last_Affected or Last_Affected are within the last x hours, while QCDM is null
		/// </summary>
		/// <param name="hours"></param>
		/// <returns></returns>
		protected static List<int> FindRecentNewDatasets(int hours, string connectionString)
		{
			List<int> datasetIDs = new List<int>();

			try
			{
				using (SqlConnection connection = new SqlConnection(connectionString))
				{
					connection.Open();

					SqlCommand command = new SqlCommand(
						 " SELECT Dataset_ID" +
						 " FROM T_Dataset_QC" +
						 " WHERE (QCDM IS NULL) AND (DATEDIFF(hour, Quameter_Last_Affected, GETDATE()) < 24 OR" +
												   " DATEDIFF(hour, Last_Affected, GETDATE()) < 24)", connection);

					using (SqlDataReader drReader = command.ExecuteReader())
					{
						if (drReader.HasRows)
						{
							while (drReader.Read())
							{
								int datasetID = drReader.GetInt32(0);
								datasetIDs.Add(datasetID);
							}
						}
					}
				}

			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception in FindRecentNewDatasets: " + ex.Message);
			}

			return datasetIDs;
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
		/// <param name="datasetIDList">Dataset ID List or range; alternatively, a timespan in number of hours (e.g. 24h)</param>
		/// <param name="errorMessage">Error message (output parameter)</param>
		/// <returns>True if success; false if an error or no Dataset IDs</returns>
		public static List<int> ParseDatasetIDList(string datasetIDList, string connectionString, out string errorMessage, out bool processingTimespan)
		{
			List<int> lstDatasetIDs = new List<int>();
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

				lstDatasetIDs.AddRange(System.Linq.Enumerable.Range(datasetIDStart, datasetIDEnd - datasetIDStart + 1));

				return lstDatasetIDs;
			}

			if (datasetIDList.EndsWith("h"))
			{
				// Timespan
				int hours;
				if (int.TryParse(datasetIDList.Substring(0, datasetIDList.Length - 1), out hours))
				{
					lstDatasetIDs = FindRecentNewDatasets(hours, connectionString);
					processingTimespan = true;

					if (lstDatasetIDs.Count == 0)
						errorMessage = "No new datasets found with new QC values from the last " + hours + " hours";

					return lstDatasetIDs;
				}
				else
				{
					errorMessage = "Timespan must be of the form \"24h\" or similar: " + datasetIDList;
					return new List<int>();
				}
			}

			if (int.TryParse(datasetIDList, out value))
			{
				lstDatasetIDs.Add(value);
				return lstDatasetIDs;
			}
			else
			{
				errorMessage = "DatasetIDList must contain an integer, a list of integers, a range of integers, or a number of hours: " + datasetIDList;
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

				List<List<string>> lstMetricsByDataset = db.GetData(lstDatasetIDs, mSkipAlreadyProcessedDatasets);

				//Checks to see if we have any datasets
				if (lstMetricsByDataset.Count == 0)
				{
					mErrorMessage = "No Metrics were found for the given Datasets IDs";

					if (mProcessingTimespan)
						return true;
					else
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

					if (mProcessingTimespan)
						return true;
					else
						return false;
				}

				bool success = RunLLRC(mWorkingDirPath, lstValidDatasetIDs.Count);

				if (!success)
				{
					// mErrorMessage should have a description of the error
					return false;
				}

				if (!mPostToDB)
				{
					// Display the results
					Posting post = new Posting();
					Dictionary<int, string> dctResults = post.CacheQCDMResults(mWorkingDirPath);
					int datasetCountDisplayed = 0;

					// Display results for the first 10 datasets
					Console.WriteLine();
					Console.WriteLine("Results:");
					foreach (var item in dctResults)
					{
						Console.WriteLine("DatasetID " + item.Key + ": " + item.Value);
						datasetCountDisplayed += 1;
						if (datasetCountDisplayed >= mMaxResultsToDisplay)
						{
							Console.WriteLine("Results for " + (dctResults.Count - datasetCountDisplayed).ToString() + " additional datasets not displayed");
							break;
						}
					}
				}
				else
				{
					// Post to the database
					// Allow for up to 2 retries

					int retry = 3;

					while (retry > 0)
					{
						// Wait 1 second to let R close
						System.Threading.Thread.Sleep(333);
						PRISM.Processes.clsProgRunner.GarbageCollectNow();

						Posting post = new Posting(mConnectionString);
						success = post.PostToDatabase(lstMetricsByDataset, lstValidDatasetIDs, mWorkingDirPath);

						if (success)
						{
							retry = 0;
						}
						else
						{
							retry -= 1;
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

								Console.WriteLine("Error in ProcessDatasets (retry=" + retry + "): " + mErrorMessage);
							}

						}

						if (post.BadDatasetIDs.Count > 0)
						{
							StringBuilder sbBadDatasetIDs = new StringBuilder();
							foreach (int datasetID in post.BadDatasetIDs)
							{
								if (sbBadDatasetIDs.Length > 0)
									sbBadDatasetIDs.Append(", ");
								sbBadDatasetIDs.Append(datasetID);
							}

							Console.WriteLine("Dataset IDs for which LLRC could not compute a QCDM result: " + sbBadDatasetIDs.ToString());
						}
					}

					if (!success)
						return false;

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

			Console.WriteLine();
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
