using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace QCDMWrapper
{
    internal class Program
    {
        public static string Title = "Instrument_Category,Dataset_ID,Instrument,Dataset,XIC_WideFrac, MS1_TIC_Change_Q2 , MS1_TIC_Q2 , MS1_Density_Q1 , MS1_Density_Q2 , MS2_Density_Q1 , DS_2A , DS_2B , MS1_2B , P_2A , P_2B , P_2C , SMAQC_Job , Quameter_Job , XIC_FWHM_Q1 , XIC_FWHM_Q2 , XIC_FWHM_Q3 , XIC_Height_Q2 , XIC_Height_Q3 , XIC_Height_Q4 , RT_Duration , RT_TIC_Q1 , RT_TIC_Q2 , RT_TIC_Q3 , RT_TIC_Q4 , RT_MS_Q1 , RT_MS_Q2 , RT_MS_Q3 , RT_MS_Q4 , RT_MSMS_Q1 , RT_MSMS_Q2 , RT_MSMS_Q3 , RT_MSMS_Q4 , MS1_TIC_Change_Q3 , MS1_TIC_Change_Q4 , MS1_TIC_Q3 , MS1_TIC_Q4 , MS1_Count , MS1_Freq_Max , MS1_Density_Q3 , MS2_Count , MS2_Freq_Max , MS2_Density_Q2 , MS2_Density_Q3 , MS2_PrecZ_1 , MS2_PrecZ_2 , MS2_PrecZ_3 , MS2_PrecZ_4 , MS2_PrecZ_5 , MS2_PrecZ_more , MS2_PrecZ_likely_1 , MS2_PrecZ_likely_multi , Quameter_Last_Affected , C_1A , C_1B , C_2A , C_2B , C_3A , C_3B , C_4A , C_4B , C_4C , DS_1A , DS_1B , DS_3A , DS_3B , IS_1A , IS_1B , IS_2 , IS_3A , IS_3B , IS_3C , MS1_1 , MS1_2A , MS1_3A , MS1_3B , MS1_5A , MS1_5B , MS1_5C , MS1_5D , MS2_1 , MS2_2 , MS2_3 , MS2_4A , MS2_4B , MS2_4C , MS2_4D , P_1A , P_1B , P_3 , Smaqc_Last_Affected , PSM_Source_Job , year";
        public static string Connection = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";
        public static string Storedpro = "StoreQCDMResults";
        public static string Fileloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\";

        private static int Main(string[]  args)
        {
            try
            {
                //Checking the input data to make sure it is okay
                //Also getting the size and orginizing the dataset Ids
                var inp = new InputCheck();
                var fileloc = inp.CmdInput(args);
                var list = new List<string>(inp.Datalist());
                var size = inp.GetSize();

                //Opening the database
                //Getting the data from the database about the dataset Ids
                var db = new DatabaseMang();
                db.Open();
                var values = db.GetData(size, list);
                var subsize = db.Getsubstringsize();
                //Checks to see if we have any datasets
                if (values.Count == 0)
                {
                    Console.WriteLine("No Datasets were found");
                    Console.ReadKey();

                    Console.ReadKey();
                    return 1;
                }

                //Deletes Old files so they dont interfer with new ones
                //Writes the data.csv file from the data gathered from database
                //Writes the R file and the batch file to run it
                var wf = new WriteFiles();
                wf.DeleteFiles(fileloc);
                wf.WriteCsv(values, Title, size, fileloc, subsize);
                wf.WriteRFile(fileloc);
                wf.WriteBatch(fileloc);

                //Runs the batch program
                var p = new Process {StartInfo = {FileName = Fileloc + "RunR.bat"}};
                p.Start();

				string scriptOutFilePath = Path.Combine(Fileloc, "QCDMscript.r.Rout");
				bool bAbort = false;

                //Checks to see if the files have been made
                while (File.Exists(Fileloc + "TestingDataset.csv") == false)
                {
                    System.Threading.Thread.Sleep(5000);

					if (ErrorReportedByR(scriptOutFilePath))
					{
						bAbort = true;
						break;
					}
                }

				if (bAbort)
				{					
                    Console.ReadKey();
					return 1;
				}
            
				    //Posts the data to the database
                var post = new Posting();
                post.PostToDatabase(size, list, values, fileloc);

                //Closes Connection and Waits for response to close//
                db.Close();
                Console.ReadKey();
                return 0;
            }

            //Displays errors if any occur
            catch (Exception e)
            {
                Console.WriteLine(e);

                Console.ReadKey();
                return 1;
            }

        }

		static bool ErrorReportedByR(string scriptOutFilePath)
		{
			if (File.Exists(scriptOutFilePath))
			{
				Console.WriteLine("Check");

				using (StreamReader srOutFile = new StreamReader(new FileStream(scriptOutFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
				{
					while (srOutFile.Peek() > -1)
					{
						string logText = srOutFile.ReadLine();
						if (logText.Contains("there is no package called 'QCDM'") || logText.Contains("Execution halted"))
						{
							Console.WriteLine("Error with R: " + logText);
							return true;
						}
					}
				}

			}

			return false;
		}
    }
}