﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace QCDMWrapper
{
    class InputCheck
    {
        public static string Input;
        public static int Size;

        //Checks to see if there is a commmand line input and if in that input they specify a output folder to put data
        public string CmdInput(string[] args)
        {

            var fileloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\";
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
                    fileloc = args[1];
                    Input = args[2];
                    if (!fileloc.EndsWith(@"/"))
                    {
                        fileloc = fileloc + @"/";
                    }
                }
                else
                {
                    Input = args[0];
                    if (args.Length > 1)
                    {
                        if (args[1].Equals("-o"))
                        {
                            fileloc = args[2];
                            if (!fileloc.EndsWith(@"/"))
                            {
                                fileloc = fileloc + @"/";
                            }
                        }
                        else
                        {
                            Console.WriteLine("Your request is unkown");
                        }
                    }
                }
            }
            return fileloc;
        }

        //Figures out if you entered a single ID or a group of ID's//
        public List<string> Datalist()
        {
            var list = new List<string> { Input };
         
            if (Input != null && Input.Contains(","))
            {
                list = new List<string>(Input.Split(','));
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
            }

            Size = list.Count;
            return list;
        }

        public int GetSize()
        {
            return Size;
        }
    }
}
