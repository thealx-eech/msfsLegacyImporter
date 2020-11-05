using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace msfsLegacyImporter
{
    class csvHelper
    {
        private List<languageFile> langugeFiles;
        private languageFile defaultLanguage;
        private languageFile userLanguage;
        private bool DEBUG = false;

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

                            //Console.WriteLine("AIR TABLE " + result.Last()[0] + " / " + result.Last()[1]);
                        }
                    }
                    return result;
                } catch {
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
                        
                        if (DEBUG)
                            Console.WriteLine("AIR VALUES " + result.Last()[0] + " / " + result.Last()[1] + " / " + result.Last()[2]);

                        //isRecordBad = false;
                    }

                    foreach (string val in bad) {
                        Console.WriteLine("Bad value: " + val);
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

                            if (DEBUG)
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

                                if (DEBUG)
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

        public void languageUpdate(string language)
        {
            string userLanguagefile = AppDomain.CurrentDomain.BaseDirectory + "\\lngFls\\userLanguage";
            foreach (languageFile langugeFile in langugeFiles)
            {
                if (langugeFile.Name == language)
                {
                    Console.WriteLine("Language changed to " + language);
                    userLanguage = langugeFile;
                    try { File.WriteAllText(userLanguagefile, language); } catch { }
                    break;
                }
            }
        }

        public void initializeLanguages(ComboBox LangSelector)
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\lngFls\\"))
            {
                try { Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\lngFls\\"); } catch { MessageBox.Show("Failed to create languages folder"); }
            }


            string userLanguagefile = AppDomain.CurrentDomain.BaseDirectory + "\\lngFls\\userLanguage";
            string language = "English - Default";
            if (File.Exists(userLanguagefile))
            {
                try { language = File.ReadAllText(userLanguagefile); } catch { }
            }


            langugeFiles = new List<languageFile>();
            defaultLanguage = null;
            userLanguage = null;

            foreach (string filepath in Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory + "\\lngFls\\", "*.csv", SearchOption.TopDirectoryOnly))
            {
                string filename = Path.GetFileNameWithoutExtension(filepath);
                languageFile currentLanguage = new languageFile(filename, new List<string[]>());

                try
                {
                    using (var reader = new StreamReader(filepath))
                    {
                        var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                        string[] header = new string[] { "slug", "translation", "original" };
                        csv.Configuration.HasHeaderRecord = true;
                        csv.Configuration.IgnoreQuotes = false;
                        csv.Configuration.Delimiter = ",";

                        var bad = new List<string>();
                        csv.Configuration.BadDataFound = context =>
                        {
                            bad.Add(context.RawRecord);
                        };

                        while (csv.Read())
                        {
                            if (csv.Context.Row == 1)
                            {
                                csv.ReadHeader();
                                continue;
                            }

                            csv.TryGetField(header[0], out string field1);
                            string field2;
                            if (filename == "English - Default")
                                csv.TryGetField(header[2], out field2);
                            else
                                csv.TryGetField(header[1], out field2);

                            if (!string.IsNullOrEmpty(field1) && !string.IsNullOrEmpty(field2))
                                currentLanguage.Rows.Add(new string[] { field1, field2 });
                        }

                        Console.WriteLine("Bad records: " + string.Join(",", bad));
                    }

                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = filename;
                    item.Tag = filename;
                    LangSelector.Items.Add(item);

                    langugeFiles.Add(currentLanguage);

                    if (currentLanguage.Rows.Count > 0 && filename == language)
                    {
                        userLanguage = currentLanguage;
                        LangSelector.Text = filename;
                    }
                    if (currentLanguage.Rows.Count > 0 && filename == "English - Default")
                    {
                        defaultLanguage = currentLanguage;
                        if (string.IsNullOrEmpty(language))
                            LangSelector.Text = filename;
                    }

                    Console.WriteLine("Language file " + filename + " loaded, " + currentLanguage.Rows.Count + " records added");
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error to parse \"\\lngFls\\" + language + ".csv\": " + e.Message);
                }
            }

            if (userLanguage == null)
                MessageBox.Show("Language file " + language + ".csv is empty or does not exists.");
        }

        public string trans(string slug)
        {
            if (userLanguage != null)
            {
                string[] trans = userLanguage.Rows.Find(x => x[0] == slug);
                if (trans != null && !String.IsNullOrEmpty(trans[1]))
                    return trans[1].Replace("\\n", Environment.NewLine);
            }

            if (defaultLanguage != null)
            {
                string[] trans = defaultLanguage.Rows.Find(x => x[0] == slug);
                if (trans != null && !String.IsNullOrEmpty(trans[1]))
                    return trans[1].Replace("\\n", Environment.NewLine);
            }

            return slug;
        }

        public class languageFile
        {
            public string Name { get; set; }
            public List<string[]> Rows { get; set; }
            public languageFile(string name, List<string[]> rows)
            {
                Name = name;
                Rows = rows;
            }
        }
    }
}
