using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace QCDMWrapper
{
    class DatabaseMang
    {
        private readonly SqlConnection con = new SqlConnection("user id=dmsreader;" + "password=dms4fun;" + "server=130.20.225.2;" + "Trusted_Connection=yes;" + "database=DMS5;" + "connection timeout=30");
        public int Substringsize;
        //Connection to Database
        public bool Open()
        {
            try
            {
                con.Open();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        //Closes Database
        public bool Close()
        {
            try
            {
                con.Close();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        //Retrieves Data from database on the datasets Ids that were given by user
        public List<List<string>> GetData(int size, List<string> list)
        {
            var values = new List<List<string>>();

            for (var i = 0; i < size; i++)
            {
                var dataId = list[i];
                var sublist = new List<string>();
                try
                {
                    //uses are massive SQL command to get the data to come out in the order we want it too
                    var command = new SqlCommand(
                            "SELECT [DMS5].[dbo].[V_Dataset_QC_Metrics].[Instrument Group],[DMS5].[dbo].[V_Dataset_QC_Metrics].[Dataset_ID],[Instrument],[Dataset],[XIC_WideFrac]" +
                            ",[MS1_TIC_Change_Q2],[MS1_TIC_Q2],[MS1_Density_Q1],[MS1_Density_Q2],[MS2_Density_Q1],[DS_2A],[DS_2B],[MS1_2B]" +
                            ",[P_2A],[P_2B],[P_2C],[SMAQC_Job],[Quameter_Job],[XIC_FWHM_Q1],[XIC_FWHM_Q2],[XIC_FWHM_Q3],[XIC_Height_Q2],[XIC_Height_Q3]" +
                            ",[XIC_Height_Q4],[RT_Duration],[RT_TIC_Q1],[RT_TIC_Q2],[RT_TIC_Q3],[RT_TIC_Q4],[RT_MS_Q1],[RT_MS_Q2],[RT_MS_Q3],[RT_MS_Q4]" +
                            ",[RT_MSMS_Q1],[RT_MSMS_Q2],[RT_MSMS_Q3],[RT_MSMS_Q4],[MS1_TIC_Change_Q3],[MS1_TIC_Change_Q4],[MS1_TIC_Q3],[MS1_TIC_Q4]" +
                            ",[MS1_Count],[MS1_Freq_Max],[MS1_Density_Q3],[MS2_Count],[MS2_Freq_Max],[MS2_Density_Q2],[MS2_Density_Q3],[MS2_PrecZ_1]" +
                            ",[MS2_PrecZ_2],[MS2_PrecZ_3],[MS2_PrecZ_4],[MS2_PrecZ_5],[MS2_PrecZ_more],[MS2_PrecZ_likely_1],[MS2_PrecZ_likely_multi],[Quameter_Last_Affected]" +
                            ",[C_1A],[C_1B],[C_2A],[C_2B],[C_3A],[C_3B],[C_4A],[C_4B],[C_4C],[DS_1A],[DS_1B],[DS_3A],[DS_3B],[IS_1A],[IS_1B],[IS_2],[IS_3A]" +
                            ",[IS_3B],[IS_3C],[MS1_1],[MS1_2A],[MS1_3A],[MS1_3B],[MS1_5A],[MS1_5B],[MS1_5C],[MS1_5D],[MS2_1],[MS2_2],[MS2_3],[MS2_4A]" +
                            ",[MS2_4B],[MS2_4C],[MS2_4D],[P_1A],[P_1B],[P_3],[Smaqc_Last_Affected],[PSM_Source_Job]" +
                            "FROM [DMS5].[dbo].[V_Dataset_QC_Metrics] INNER JOIN [DMS5].[dbo].[T_Dataset]" +
                            "ON [DMS5].[dbo].[V_Dataset_QC_Metrics].[Dataset_ID] = [DMS5].[dbo].[T_Dataset].[Dataset_ID]" +
                            "WHERE [DMS5].[dbo].[T_Dataset].[DS_sec_sep] NOT LIKE 'LC-Agilent-2D-Formic%'" +
                            "AND [DMS5].[dbo].[V_Dataset_QC_Metrics].[Dataset_ID] =" +
                            dataId, con);
                    var read = command.ExecuteReader();

                    //If the sql command doesnt find anything
                    if (read.HasRows == false)
                    {
                        Console.WriteLine(dataId + " was not found with current SQL Command");
                        size--;
                    }
                    else
                    {
                        //gets the data from the results and adds them to the list
                        while (read.Read())
                        {
                            Substringsize = read.FieldCount;
                            for (var j = 0; j < read.FieldCount; j++)
                            {
                                sublist.Add(read[j].ToString() == "" ? "NA" : read[j].ToString());
                            }
                        }
                        values.Add(sublist);
                    }

                    read.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            return values;
        }

        //Gets the size of the string
        public int Getsubstringsize()
        {
            return Substringsize;
        }

    }

    
}
