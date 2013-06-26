using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QCDMWrapper
{
    class DatabaseMang
    {
        private readonly SqlConnection con = new SqlConnection("user id=dmsreader;" + "password=dms4fun;" + "server=130.20.225.2;" + "Trusted_Connection=yes;" + "database=DMS5;" + "connection timeout=30");

        public bool Open()
        {
            //Connection to Database//
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

        public bool Close()
        {
            //Connection to Database//
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
                        Console.WriteLine(dataId + " is a LC-Agilent-2D-Formic seperation type");
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
