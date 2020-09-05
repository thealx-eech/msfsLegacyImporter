using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace msfsLegacyImporter
{
    class cfgHelper
    {
        // cfgFiles
        // ---- filename
        // ---- sections
        // --------- sectionname
        // --------- lines
        // ------------- parameter
        // ------------- value
        // ------------- comment
        private List<CfgFile> cfgFiles;


        public void processTemplateCfgs()
        {
            cfgFiles = new List<CfgFile>();
            List<CfgLine> cfgLines;

            foreach (var file in new[] { "aircraft.cfg", "cameras.cfg", "cockpit.cfg", "engines.cfg", "flight_model.cfg", "gameplay.cfg", "systems.cfg" })
            {
                string content = System.IO.File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\cfgTpl\\" + file);
                cfgLines = readCSV(content + "[]");
                //Console.WriteLine(cfgLines.First().Name);
                parseCfg(file.Split('.')[0], cfgLines);
            }

            //Console.WriteLine(cfgFiles.Last().Sections.Last().Lines.First().Name + " " + cfgFiles.Last().Sections.Last().Lines.First().Value + " " + cfgFiles.Last().Sections.Last().Lines.First().Comment);
        }

        public List<CfgLine> readCSV(string content)
        {
            List<CfgLine> list = new List<CfgLine>();
            foreach (var line in content.Split(new string[] { System.Environment.NewLine },StringSplitOptions.None))
            {
                string[] data = line.Split(';');
                if (data.Length >= 2)
                    list.Add(new CfgLine(data[0].Split('=')[0].Trim().ToLower(), data[0].Split('=').Length >= 2 ? data[0].Split('=')[1].Trim().ToLower() : "", data[1].Trim().ToLower()));
                else if (data.Length == 1)
                    list.Add(new CfgLine(data[0].Split('=')[0].Trim().ToLower(), data[0].Split('=').Length >= 2 ? data[0].Split('=')[1].Trim().ToLower() : "", ""));
                else
                    list.Add(new CfgLine("", "", ""));
            }

            //list.Reverse();
            return list;
        }

        private void parseCfg(string file, List<CfgLine> lines)
        {
            //CfgFile currentFile = new CfgFile();
           // currentFile.Name = file;

            List<CfgSection> cfgSections = new List<CfgSection>();
            List<CfgLine> cfgLines = new List<CfgLine>();


            string currentSection = "";

            foreach (var line in lines)
            {
                // SECTION START CHECK
                if (!String.IsNullOrEmpty(line.Name))
                {
                    if (line.Name.Trim()[0] == '[')
                    {
                        // ADD FINISHED SECTION
                        if (currentSection != "")
                        {
                            cfgSections.Add(new CfgSection(currentSection, cfgLines));
                        }

                        // PREPARE FOR NEW SECTION
                        currentSection = line.Name.Trim().ToUpper();
                        cfgLines = new List<CfgLine>();

                    }
                    else if (currentSection != "")
                    {
                        cfgLines.Add(new CfgLine(line.Name, line.Value, line.Comment));
                    }
                }
            }

            cfgFiles.Add(new CfgFile(file, cfgSections));
        }

        public void splitCfg(string aircraftDirectory)
        {
            string content = System.IO.File.ReadAllText(aircraftDirectory + "\\aircraft.cfg");
            content += System.Environment.NewLine + "[]"; // TO FINALIZE PARSING
            content = content.Replace("//", ";"); // REPLACE COMMENT SYMBOLS

            string currentSection = "";

            // RENAME
            File.Move(aircraftDirectory + "\\aircraft.cfg", aircraftDirectory + "\\.aircraft.cfg");

            List<CfgLine> aircraftCfg = readCSV(content);
            List<CfgLine> cfgLines = new List<CfgLine>();

            foreach (var aircraftCfgLine in aircraftCfg)
            {
                // SECTION START CHECK
                if (!String.IsNullOrEmpty(aircraftCfgLine.Name))
                {
                    if (aircraftCfgLine.Name.Trim()[0] == '[')
                    {
                        // ADD MISSING LINES AND FINALIZE SECTION
                        if (currentSection != "")
                        {

                            bool brk = false;
                            CfgFile lastFile = null;
                            CfgSection lastSection = null;

                            foreach (var cfgFile in cfgFiles)
                            {
                                lastFile = cfgFile;
                                foreach (var cfgSection in cfgFile.Sections)
                                {
                                    lastSection = cfgSection;
                                    lastSection.Name = lastSection.Name.Trim().ToUpper();

                                    // MATCH CURRENT VERSION
                                    if (lastSection.Name == currentSection || 
                                        lastSection.Name.Contains(".") && currentSection.Contains(".") && lastSection.Name.Split('.')[0] == currentSection.Split('.')[0])
                                    {
                                        //Console.WriteLine("Section match: " + currentSection + " - " + lastSection.Name);
                                        brk = true;
                                        break;
                                    } else
                                    {
                                        //Console.WriteLine("Section does not match: " + currentSection + " - " + lastSection.Name);
                                    }
                                }

                                if (brk)
                                    break;
                            }

                            // COPY LINES INTO RELATIVE CFG
                            string filename = brk && lastFile != null ? lastFile.Name + ".cfg" : ".unknown.cfg";
                            {
                                FileInfo fi = new FileInfo(aircraftDirectory + "\\" + filename);
                                
                                if (!File.Exists(aircraftDirectory + "\\" + filename))
                                {
                                    using (FileStream fs = File.Create(aircraftDirectory + "\\" + filename))
                                    {
                                        byte[] text = new UTF8Encoding(true).GetBytes("[VERSION]" + System.Environment.NewLine + "major = 1" + System.Environment.NewLine + "minor = 0" + System.Environment.NewLine);
                                        // Add some information to the file.
                                        fs.Write(text, 0, text.Length);
                                    }
                                }

                                using (FileStream fs = File.Open(aircraftDirectory + "\\" + filename, FileMode.Append, FileAccess.Write, FileShare.Write))
                                {
                                    byte[] text = new UTF8Encoding(true).GetBytes(System.Environment.NewLine + currentSection + System.Environment.NewLine + System.Environment.NewLine);
                                    fs.Write(text, 0, text.Length);


                                    foreach (var cfgLine in cfgLines) {
                                        text = new UTF8Encoding(true).GetBytes(cfgLine.Name + " = " + cfgLine.Value + " ; " + cfgLine.Comment + System.Environment.NewLine);
                                        fs.Write(text, 0, text.Length);
                                    }
                                }
                            }

                            // CLEAR LINES LIST
                            cfgLines = new List<CfgLine>();
                        }

                        // PREPARE FOR NEW SECTION
                        currentSection = aircraftCfgLine.Name.Trim().ToUpper();

                    }
                    else if (currentSection != "")
                    {
                        cfgLines.Add(new CfgLine(aircraftCfgLine.Name, aircraftCfgLine.Value, aircraftCfgLine.Comment));
                    }
                }
           }
        }


        public string[] getInstruments(string aircraftDirectory)
        {
            CfgFile availableCockpitSections = cfgFiles.Find(x => x.Name == "cockpit");
            string[] availableSections = new string[100];
            string[] installedSections = new string[100];
            List<CfgLine> installedCockpitSections = new List<CfgLine>();

            if (File.Exists(aircraftDirectory + "\\cockpit.cfg"))
            {
                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\cockpit.cfg");
                installedCockpitSections = readCSV(content + "[]");
            }

            if (availableCockpitSections != null)
            {
                int i = 0;
                foreach (var cockpitSection in availableCockpitSections.Sections)
                {
                    if (installedCockpitSections.Find(x => x.Name.ToUpper() == cockpitSection.Name.ToUpper()) != null )
                        availableSections[i] = cockpitSection.Name.ToUpper() + Environment.NewLine;
                    else
                        availableSections[i] = "-" + cockpitSection.Name.ToUpper() + Environment.NewLine;
                    i++;
                }
            }

            return availableSections;
        }

        public void enableGauges(string aircraftDirectory, string[] gauges)
        {
            if (File.Exists(aircraftDirectory + "\\cockpit.cfg") && gauges.Length > 0)
            {
                using (FileStream fs = File.Open(aircraftDirectory + "\\cockpit.cfg", FileMode.Append, FileAccess.Write, FileShare.Write))
                {
                    CfgFile availableCockpitSections = cfgFiles.Find(x => x.Name == "cockpit");

                    if (availableCockpitSections != null)
                    {

                        foreach (var gauge in gauges)
                        {
                            if (String.IsNullOrEmpty(gauge))
                                break;

                            byte[] text = new UTF8Encoding(true).GetBytes(System.Environment.NewLine + gauge + System.Environment.NewLine);
                            fs.Write(text, 0, text.Length);

                            foreach (var sect in availableCockpitSections.Sections)
                            {
                                var pattern = @"\[(.*?)\]";

                                if (Regex.Matches(sect.Name, pattern)[0].Groups[1].ToString().Trim() == Regex.Matches(gauge, pattern)[0].Groups[1].ToString().Trim())
                                {
                                    //Console.WriteLine("availableGaugeLines found in section " + sect.Name + " lines " + sect.Lines.Count);
                                    foreach (var cfgLine in sect.Lines)
                                    {
                                        text = new UTF8Encoding(true).GetBytes(cfgLine.Name + " = " + cfgLine.Value + " ; " + cfgLine.Comment + System.Environment.NewLine);
                                        fs.Write(text, 0, text.Length);
                                    }

                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("availableCockpitSections is null: cockpit");
                    }
                }
            }
        }

        public string[] getLights(string aircraftDirectory)
        {
            string[] lightsList = new string[100];
            int i = 0;

            if (File.Exists(aircraftDirectory + "\\systems.cfg"))
            {
                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\systems.cfg");
                foreach (string line in Regex.Split(content, "\r\n|\r|\n")) {
                    if (line.ToLower().Trim().StartsWith("lightdef."))
                    {
                        lightsList[i] = line;
                        i++;
                    } else if (line.ToLower().Trim().StartsWith("light."))
                    {
                        lightsList[i] = "-" + line;
                        i++;
                    }
                }
            }

            return lightsList;
        }


        // MISC STUFF
        public class CfgFile
        {
            public string Name { get; set; }
            public List<CfgSection> Sections { get; set; }
            public CfgFile(string name, List<CfgSection> sections)
            {
                Name = name;
                Sections = sections;
            }
        }
        public class CfgSection
        {
            public string Name { get; set; }
            public List<CfgLine> Lines { get; set; }
            public CfgSection(string name, List<CfgLine> lines)
            {
                Name = name;
                Lines = lines;
            }
        }
        public class CfgLine
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Comment { get; set; }

            public CfgLine(string name, string value, string comment)
            {
                Name = name;
                Value = value;
                Comment = comment;
            }
        }
    }
}
