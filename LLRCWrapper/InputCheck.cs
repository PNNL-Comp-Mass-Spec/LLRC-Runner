using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace QCDMWrapper
{
    class InputCheck
    {
        public static string Fileloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\";
        public static string Input;

        public void CmdInput(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine(
                    "Please enter a single Data ID ,a group seperated by commas, or a range seperated by - : ");
                Input = Console.ReadLine();
            }
            else
            {
                if (args[0].Equals("-o") && args.Length > 1)
                {
                    Fileloc = args[1];
                    Input = args[2];
                    if (!Fileloc.EndsWith(@"/"))
                    {
                        Fileloc = Fileloc + @"/";
                    }
                }
                else
                {
                    Input = args[0];
                    if (args.Length > 1)
                    {
                        if (args[1].Equals("-o"))
                        {
                            Fileloc = args[2];
                            if (!Fileloc.EndsWith(@"/"))
                            {
                                Fileloc = Fileloc + @"/";
                            }
                        }
                        else
                        {
                            Console.WriteLine("Your request is unkown");
                        }
                    }
                }
            }
        }

        //Figures out if you entered a single ID or a group of ID's//
        public void Datalist()
        {
            var skip = false;
            var list = new List<string> { Input };
         
            if (Input != null && Input.Contains(","))
            {
                list = List<string>(Input.Split(','));
                skip = true;
            }

            if (Input != null && Input.Contains("-"))
            {
                list = new List<string>(Input.Split('-'));
                int num1;
                int.TryParse(list[0], out num1);
                int num2;
                int.TryParse(list[1], out num2);
                while (num1 + 1 < num2)
                {
                    num2--;
                    list.Insert(1, num2.ToString(CultureInfo.InvariantCulture));
                }
                skip = true;
            }

            if (skip == false)
            {
                list = new List<string> { Input };
            }

            return list;
        }
    }
}
