﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QCDMWrapper
{
	class LLRC
	{

		protected string mConnectionString;
		protected string mWorkingDirPath;
		protected bool mPostToDB;

		protected string mErrorMessage;

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

		/// <summary>
		/// Constructor
		/// </summary>
		public LLRC()
			: this(Posting.DEFAULT_CONNECTION_STRING)
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public LLRC(string connectionString)
		{
			mErrorMessage = string.Empty;

			if (string.IsNullOrEmpty(connectionString))
				mConnectionString = Posting.DEFAULT_CONNECTION_STRING;
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

				lstDatasetIDs.AddRange(System.Linq.Enumerable.Range(datasetIDStart, datasetIDEnd));
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
		public bool ProcessDatasets(List<int> lstDatasetIDs)
		{
			try
			{

				// Validate that required files are present
				List<string> lstRequiredFiles = new List<string>();
				lstRequiredFiles.Add("Models_paper.Rdata");
				lstRequiredFiles.Add("allData_v3.Rdata");

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
				db.Open();

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
				wf.WriteCsv(lstMetricsByDataset, mWorkingDirPath, db.MetricCount);
				wf.WriteRFile(mWorkingDirPath);
				wf.WriteBatch(mWorkingDirPath);

				bool success = RunLLRC(mWorkingDirPath);

				if (!success)
				{
					return false;
				}

				if (mPostToDB)
				{
					//Posts the data to the database
					var post = new Posting(mConnectionString);
					post.PostToDatabase(lstMetricsByDataset, mWorkingDirPath);
				}

				// Close connection
				db.Close();
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
		protected bool RunLLRC(string WorkingDirPath)
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
				mErrorMessage = "R exited without error, but the results file does not exist: " + fiResultsFile.Name + " at " + fiResultsFile.FullName;
				return false;
			}

			return true;
		}

	}
}
