using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace QCDMWrapper
{
    class DatabaseMang
    {
        private readonly SqlConnection con = new SqlConnection("user id=dmsreader;" + "password=dms4fun;" + "server=130.20.225.2;" + "Trusted_Connection=yes;" + "database=DMS5;" + "connection timeout=30");

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
                string dataId = list[i];
                var sublist = new List<string>();
                try
                {
                    var command = new SqlCommand(
                            "SELECT [DMS5].[dbo].[V_Dataset_QC_Metrics].* FROM [DMS5].[dbo].[V_Dataset_QC_Metrics] " +
                            "INNER JOIN [DMS5].[dbo].[T_Dataset] ON [DMS5].[dbo].[V_Dataset_QC_Metrics].[Dataset_ID] = " +
                            "[DMS5].[dbo].[T_Dataset].[Dataset_ID] WHERE [DMS5].[dbo].[T_Dataset].[DS_sec_sep] " +
                            "NOT LIKE 'LC-Agilent-2D-Formic%' AND [DMS5].[dbo].[V_Dataset_QC_Metrics].[Dataset_ID] = " +
                            dataId, con);
                    var read = command.ExecuteReader();
                    if (read.HasRows == false)
                    {
                        Console.WriteLine(dataId + " was not found with current SQL Command");
                        size--;
                    }
                    else
                    {
                        while (read.Read())
                        {
                            for (var j = 0; j < 98; j++)
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

    }

    
}
