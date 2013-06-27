using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCDMWrapper
{
    class Posting
    {
        PRISM.DataBase.clsExecuteDatabaseSP _mExecuteSp;
        public static string Connection = "Data Source=gigasax;Initial Catalog=DMS5;Integrated Security=SSPI;";
        public static string Storedpro = "StoreQCDMResults";

        //Posts the QCDM metric to the database
        public void PostToDatabase(int size, List<string> list, List<List<string>> values, string fileloc)
        {
            const int smaqcval = 53;
            const int quamval = 7;
            const int datasetval = 4;
          
            int error = 0;
            for (int i = 0; i < size; i++)
            {
                string smaqc = values[i][smaqcval];
                string quametric = values[i][quamval];
                string dataset = values[i][datasetval];
                string dataId = list[i];
                string qcdmvalue = GetQcdm(i, dataId, error, fileloc);

                if (qcdmvalue.Equals("NA"))
                {
                    Console.WriteLine(dataId + " did not have enough information to get the QCDM");
                    error++;
                }
                else
                {
                    string xml = ConvertQcdmtoXml(qcdmvalue, smaqc, quametric, dataset);
                    int data;
                    int.TryParse(dataId, out data);
                    var po = new Posting();
                    bool tf = po.PostQcdmResultsToDb(data, xml, Connection, Storedpro);
                    if (tf)
                    {
                        Console.WriteLine(dataId + " was successfully Posted to DB");
                    }
                    else
                    {
                        Console.WriteLine("Something went wrong with the PostQCDMResultsToDB function");
                    }
                }
            }
        }

        //gets the QCDM value from the .csv file that is created from the R program
        public static string GetQcdm(int i, string id, int e, string fileloc)
        {
            i++;
            i = i - e;
            string[] lines = File.ReadAllLines(fileloc + "TestingDataset.csv");
            if (lines.Length == 1)
            {
                Console.WriteLine("Nothing was calculated");
                return "NA";
            }
            try
            {
                string info = lines[i];
                var curline = new List<string>(info.Split(','));

                //Test to see if the R program was able to get results//
                if (curline[3] == id)
                {
                    int spot = curline.Count - 1;
                    string qcdm = curline[spot];
                    return qcdm;
                }
                return "NA";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "NA";
            }
        }

        //Converts the QCDM to xml to be used by database
        private static string ConvertQcdmtoXml(string qcdmvalue, string smaqc, string quameter, string dataset)
        {
            var sbXml = new System.Text.StringBuilder();
            string sXmlResults;

            try
            {
                sbXml.Append("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
                sbXml.Append("<QCDM_Results>");

                sbXml.Append("<Dataset>" + dataset + "</Dataset>");
                if (smaqc == "NA")
                {
                    smaqc = "0";
                }
                sbXml.Append("<SMAQC_Job>" + smaqc + "</SMAQC_Job>");
                sbXml.Append("<Quameter_Job>" + quameter + "</Quameter_Job>");

                sbXml.Append("<Measurements>");
                sbXml.Append("<Measurement Name=\"" + "QCDM" + "\">" + qcdmvalue + "</Measurement>");
                sbXml.Append("</Measurements>");

                sbXml.Append("</QCDM_Results>");

                sXmlResults = sbXml.ToString();

            }
            catch (Exception ex)
            {
                var error1 = "Error converting Quameter results to XML" + ex;
                Console.WriteLine(error1);
                return "";
            }

            return sXmlResults;

        }

        //Posts the xml to the database
        public bool PostQcdmResultsToDb(int intDatasetId, string sXmlResults, string sConnectionString, string sStoredProcedure)
        {

            const int maxRetryCount = 3;
            const int secBetweenRetries = 20;

            int intStartIndex = 0;
            int intResult = 0;

            string sXMLResultsClean = null;

            System.Data.SqlClient.SqlCommand objCommand;

            bool blnSuccess = false;

            try
            {
                Console.WriteLine("Posting QCDM Results to the database (using Dataset ID " + intDatasetId.ToString() + ")");

                // We need to remove the encoding line from sXMLResults before posting to the DB
                // This line will look like this:
                //   <?xml version="1.0" encoding="utf-8" standalone="yes"?>

                intStartIndex = sXmlResults.IndexOf("?>");
                if (intStartIndex > 0)
                {
                    sXMLResultsClean = sXmlResults.Substring(intStartIndex + 2).Trim();
                }
                else
                {
                    sXMLResultsClean = sXmlResults;
                }

                // Call stored procedure sStoredProcedure using connection string sConnectionString

                objCommand = new System.Data.SqlClient.SqlCommand();

                {
                    objCommand.CommandType = System.Data.CommandType.StoredProcedure;
                    objCommand.CommandText = sStoredProcedure;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@Return", System.Data.SqlDbType.Int));
                    objCommand.Parameters["@Return"].Direction = System.Data.ParameterDirection.ReturnValue;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@DatasetID", System.Data.SqlDbType.Int));
                    objCommand.Parameters["@DatasetID"].Direction = System.Data.ParameterDirection.Input;
                    objCommand.Parameters["@DatasetID"].Value = intDatasetId;

                    objCommand.Parameters.Add(new System.Data.SqlClient.SqlParameter("@ResultsXML", System.Data.SqlDbType.Xml));
                    objCommand.Parameters["@ResultsXML"].Direction = System.Data.ParameterDirection.Input;
                    objCommand.Parameters["@ResultsXML"].Value = sXMLResultsClean;
                }

                _mExecuteSp = new PRISM.DataBase.clsExecuteDatabaseSP(sConnectionString);
                AttachExecuteSpEvents();

                intResult = _mExecuteSp.ExecuteSP(objCommand, maxRetryCount, secBetweenRetries);

                if (intResult == PRISM.DataBase.clsExecuteDatabaseSP.RET_VAL_OK)
                {
                    // No errors
                    blnSuccess = true;
                }
                else
                {
                    string error1 = "Error storing Quameter Results in database, " + sStoredProcedure + " returned " + intResult.ToString();
                    Console.WriteLine(error1);
                    blnSuccess = false;
                }

            }
            catch (System.Exception ex)
            {
                string error2 = "Exception storing Quameter Results in database" + ex;
                Console.WriteLine(error2);
                blnSuccess = false;
            }
            finally
            {
                DetachExecuteSpEvents();
                _mExecuteSp = null;
            }

            return blnSuccess;
        }

        private void AttachExecuteSpEvents()
        {
            try
            {
                _mExecuteSp.DBErrorEvent += new PRISM.DataBase.clsExecuteDatabaseSP.DBErrorEventEventHandler(mExecuteSP_DBErrorEvent);
            }
            catch
            {
                // Ignore errors here
            }
        }

        private void DetachExecuteSpEvents()
        {
            try
            {
                if (_mExecuteSp != null)
                {
                    _mExecuteSp.DBErrorEvent -= mExecuteSP_DBErrorEvent;
                }
            }
            catch
            {
                // Ignore errors here
            }
        }

        private static void mExecuteSP_DBErrorEvent(string message)
        {
            Console.WriteLine("Stored procedure execution error: " + message);
        }
    }
}
