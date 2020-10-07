using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace msfsLegacyImporter
{
    class csvHelper
    {
        public List<string[]> processAirTable(string path, string[] header)
        {
            if (File.Exists(path))
            {
                try
                {
                    List<string[]> result = new List<string[]>();
                    using (var reader = new StreamReader(path))
                    {
                        string prev_id = "";
                        int sub_counter = 0;

                        var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                        csv.Configuration.HasHeaderRecord = true;
                        csv.Configuration.IgnoreQuotes = true;
                        csv.Configuration.Delimiter = ",";

                        while (csv.Read())
                        {
                            if (csv.Context.Row == 1)
                            {
                                csv.ReadHeader();
                                continue;
                            }

                            csv.TryGetField(header[0], out string field1);
                            csv.TryGetField(header[1], out string field2);

                            if (!String.IsNullOrEmpty(field1))
                                field1 = field1.Trim().TrimStart('0');
                            if (!String.IsNullOrEmpty(field2))
                                field2 = field2.Trim();

                            if (!String.IsNullOrEmpty(field1) && field1 != prev_id)
                            {
                                sub_counter = 0;
                                prev_id = field1;
                                result.Add(new string[] { prev_id + "-" + sub_counter, field2 });
                                sub_counter++;
                            }
                            else //if (!String.IsNullOrEmpty(field2))
                            {
                                result.Add(new string[] { prev_id + "-" + sub_counter, field2 });
                                sub_counter++;
                            }

                            Console.WriteLine("AIR TABLE " + result.Last()[0] + " / " + result.Last()[1]);
                        }
                    }
                    return result;
                } catch (Exception e) {
                    MessageBox.Show("File " + path + " is locked");
                }
            }

            return null;
        }

        public List<string[]> processAirFile(string path)
        {
            if (File.Exists(path))
            {
                List<string[]> result = new List<string[]>();
                using (var reader = new StreamReader(path))
                {
                    var good = new List<Test>();
                    var bad = new List<string>();
                    //var isRecordBad = false;

                    string prev_id = "";
                    int sub_counter = 0;

                    var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                    csv.Configuration.HasHeaderRecord = false;
                    csv.Configuration.IgnoreQuotes = true;
                    csv.Configuration.Delimiter = ",";

                    while (csv.Read())
                    {
                        csv.TryGetField(0, out string field1);
                        csv.TryGetField(1, out string field2);
                        csv.TryGetField(2, out string field3);

                        if (!String.IsNullOrEmpty(field1))
                        {
                            field1 = field1.Trim();
                            field1 = Regex.Replace(field1, @".*([0-9]{4})$", "$1", RegexOptions.Singleline);
                            //field1.Substring(Math.Max(0, field1.Length - 4), field1.Length);
                        }
                        if (!String.IsNullOrEmpty(field2))
                            field2 = field2.Trim();
                        if (!String.IsNullOrEmpty(field3)) 
                            field3 = field3.Trim();

                        if (!String.IsNullOrEmpty(field1) && String.IsNullOrEmpty(field3)) {
                            // CHECK EXPORTED TABLE DATA FILE (-0.349066:1, -0.232711:1)
                            string tablePath = path.Replace("_air.txt", ".air_TAB" + field1 + ".txt");
                            if (File.Exists(tablePath))
                            {
                                Console.WriteLine("Reading " + tablePath);
                                string tableData = "";
                                string content = File.ReadAllText(tablePath);
                                int i = 0;
                                foreach (string line in Regex.Split(content, "\r\n|\r|\n"))
                                {
                                    if (i > 0 && !String.IsNullOrEmpty(line) && line.Contains(","))
                                        tableData += line.Replace(",", ":") + ",";

                                    i++;
                                }

                                // USE TABLE DATA AS VALUE
                                if (!String.IsNullOrEmpty(tableData))
                                    field3 = tableData.Trim(',');
                            }

                            sub_counter = 0;
                            result.Add(new string[] { field1.TrimStart('0') + (!String.IsNullOrEmpty(field3) ? "-" + sub_counter : ""), field2, field3 });
                            prev_id = field1.TrimStart('0');
                        }
                        else if (!String.IsNullOrEmpty(prev_id) && (String.IsNullOrEmpty(field2) || !field2.Contains("---") )/* && !String.IsNullOrEmpty(field3)*/) {
                            result.Add(new string[] { prev_id + "-" + sub_counter, field2, field3 });
                            sub_counter++;
                        }
                        Console.WriteLine("AIR VALUES " + result.Last()[0] + " / " + result.Last()[1] + " / " + result.Last()[2]);

                        //isRecordBad = false;
                    }

                    foreach (string val in bad) {
                        Console.WriteLine("Bad value: " + bad);
                    }


                }
                return result;
            }

            return null;
        }

        public List<string[]> processAirDump(string path)
        {
            string currentToken = "";
            string tableData = "";
            int sub_counter = 0;

            if (File.Exists(path))
            {
                List<string[]> result = new List<string[]>();
                string content = File.ReadAllText(path);
                foreach (string line in Regex.Split(content, "\r\n|\r|\n"))
                {

                    Regex regex = new Regex(@"Record:(\s+)(\d*)");
                    Match match = regex.Match(line);

                    if (String.IsNullOrEmpty(line.Trim())) // STORE COLLECTED DATA
                    {
                        if (currentToken != "" && tableData != "")
                        {
                            result.Add(new string[] { currentToken + "-" + sub_counter, "", tableData.TrimEnd(',') });
                            Console.WriteLine("AIR VALUES " + result.Last()[0] + " / " + result.Last()[1] + " / " + result.Last()[2]);
                        }

                        currentToken = "";
                        tableData = "";
                        sub_counter = 0;
                    }
                    else if (match.Success && match.Groups.Count >= 3) // CAPTURE TOKEN NUMBER
                    {
                        currentToken = match.Groups[2].Value.Trim().TrimStart('0');
                    }
                    else // COLLECT TABLE DATA
                    {
                        if (line.StartsWith("FIELD"))
                        {
                            string[] arr = line.Replace("FIELD ", "").Split('\t');
                            if (arr.Length >= 4 && !String.IsNullOrEmpty(arr[3].Trim()))
                            {
                                string val = arr[3].Trim();
                                if (currentToken == "1101") // INT -> DBL CONVERSION
                                {
                                    double dbl;
                                    if (Double.TryParse(val, out dbl))
                                    {
                                        val = (dbl / 2048).ToString("0.0#####");
                                    }
                                }

                                result.Add(new string[] { currentToken + "-" + sub_counter, "", val });
                                tableData = "";
                                Console.WriteLine("AIR VALUES " + result.Last()[0] + " / " + result.Last()[1] + " / " + result.Last()[2].ToString());
                            }

                            sub_counter++;
                        }
                        else if (!line.StartsWith("Points:") && !line.StartsWith("Found") && !line.StartsWith("columns:"))
                        {
                            tableData += line.Replace("\t", ":").TrimEnd(':') + ",";
                        }
                    }
                }

                return result;
            }

            return null;
        }

        public class Test
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }
    }
}
