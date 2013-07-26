using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace QCDMWrapper
{
    class InputCheck
    {
		// Could have a single datasetID, a comma-separated list of IDs, or a range of DatasetIDs
        protected string mDatasetIDList;
		protected List<int> mDatasetIDs;

		public List<int> DatasetIDs
		{
			get
			{
				return mDatasetIDs;
			}
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public InputCheck()
		{
			mDatasetIDList = string.Empty;
			mDatasetIDs = new List<int>();
		}

        /// <summary>
		/// Checks to see if there is a commmand line input and if in that input they specify a output folder to put data
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Output folder path</returns>
        public string CmdInput(string[] args)
        {
            //gets the location that the program is in
            string outputFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			mDatasetIDList = string.Empty;

            // Examine the arguments
            if (args.Length == 0)
            {
				ShowSyntax();
				return string.Empty;
            }
            else
            {
				// Parse the arguments
				mDatasetIDList = args[0];
                if (args.Length > 1)
                {
                    if (args.Length > 2 && args[1].Equals("-o"))
                    {
                        outputFolderPath = args[2];
                    }
                    else
                    {
						ShowSyntax();
						return string.Empty;
                    }
                }

				ParseDatasetIDList();
            }

            return outputFolderPath;
        }

		protected void ShowSyntax()
		{
			string exeName = Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			Console.WriteLine();
			Console.WriteLine("Program Syntax:");
			Console.WriteLine(exeName + " DatasetIDList [-o OutputFolderPath]");

			Console.WriteLine();
			Console.WriteLine("DatasetIDList can be a single DatasetID,\n  a list of DatasetIDs separated by commas,\n  or a range of DatasetIDs separated with a dash");
			Console.WriteLine("Mode 1: 325145");
			Console.WriteLine("Mode 2: 325145,325146,325150");
			Console.WriteLine("Mode 3: 325145-325150");
			
			Console.WriteLine();
			Console.WriteLine("The -o switch is optional");

		}

        /// <summary>
		/// Parses the DatasetID list or range into a valid list of DatasetIDs
        /// </summary>
        /// <returns>True if success; false if an error or no Dataset IDs</returns>
        protected bool ParseDatasetIDList()
        {
			List<string> lstDatasetIDText = new List<string>();
			bool listProcessed = false;

			if (string.IsNullOrWhiteSpace(mDatasetIDList))
				return false;

			mDatasetIDs.Clear();

			if (!listProcessed && mDatasetIDList.IndexOf(',') > 0)
			{
				// Split mDatasetIDList on commas
				lstDatasetIDText = new List<string>(mDatasetIDList.Split(','));

				foreach (string datasetID in lstDatasetIDText)
				{
					int value;
					if (int.TryParse(datasetID, out value))
					{
						mDatasetIDs.Add(value);
					}
				}
				listProcessed = true;
			}

			if (!listProcessed && mDatasetIDList.IndexOf('-') > 0)
			{
				// Split mDatasetIDList on the dash
				lstDatasetIDText = new List<string>(mDatasetIDList.Split('-'));

				if (lstDatasetIDText.Count != 2)
				{
					Console.WriteLine("Error: DatasetIDList contains a dash but does not contain two numbers separated by a dash: " + mDatasetIDList);
					ShowSyntax();
					return false;
				}

				int datasetIDStart;
				int datasetIDEnd;

				int value;
				if (int.TryParse(lstDatasetIDText[0], out value))
				{
					datasetIDStart = value;

					
					if (int.TryParse(lstDatasetIDText[1], out value))
					{
						datasetIDEnd = value;

						mDatasetIDs.AddRange(System.Linq.Enumerable.Range(datasetIDStart, datasetIDEnd));					
					}
				}

				listProcessed = true;
			}

			if (!listProcessed)
			{
				int value;
				if (int.TryParse(mDatasetIDList, out value))
				{
					mDatasetIDs.Add(value);
				}
				listProcessed = true;
			}

			if (!listProcessed || mDatasetIDs.Count == 0)
			{
				ShowSyntax();
				return false;
			}
			else
			{
				return true;
			}

        }
       
    }
}
