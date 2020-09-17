using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace msfsLegacyImporter
{
    class cfgHelper
    {
        // cfg file
        // ---- filename
        // ---- sections
        // --------- sectionname
        // --------- lines
        // ------------- active
        // ------------- parameter
        // ------------- value
        // ------------- comment
        private List<CfgFile> cfgTemplates;
        private List<CfgFile> cfgAircraft;
        public long lastChangeTimestamp;


        public bool processCfgfiles(string sourcesPath, bool isAircraft = false)
        {
            List<CfgFile> cfgFiles = new List<CfgFile>();
            List<CfgLine> cfgLines;

            foreach (var file in new[] { "aircraft.cfg", "cameras.cfg", "cockpit.cfg", "engines.cfg", "flight_model.cfg", "gameplay.cfg", "systems.cfg", "runway.flt" })
            {
                string path = sourcesPath + file;

                if (isAircraft)
                    Console.WriteLine(file + " file " + (File.Exists(path) ? "exists" : "not exists;") + " cfgFile " + (cfgFileExists(file) ? "exists" : "not exists"));

                if (File.Exists(path))
                {
                    string content = File.ReadAllText(path);
                    cfgLines = readCSV(content + "\r\n[]");
                    //Console.WriteLine(cfgLines.First().Name);
                    cfgFiles.Add(parseCfg(file, cfgLines));
                } else if (isAircraft && file.Contains(".flt")) {
                    cfgFiles.Add(new CfgFile(true, file, new List<CfgSection>()));
                }
                else if (!isAircraft)
                {
                    MessageBox.Show("File does not exists: " + path);
                    return false;
                }
            }

            if (isAircraft)
                cfgAircraft = cfgFiles;
            else
                cfgTemplates = cfgFiles;

            return true;

            //Console.WriteLine(cfgTemplates.Last().Sections.Last().Lines.First().Name + " " + cfgTemplates.Last().Sections.Last().Lines.First().Value + " " + cfgTemplates.Last().Sections.Last().Lines.First().Comment);
        }

        public List<CfgLine> readCSV(string content)
        {
            content = content.Replace("//", ";");
            List<CfgLine> list = new List<CfgLine>();
            foreach (string line in Regex.Split(content, "\r\n|\r|\n"))// Split(new string[] { System.Environment.NewLine },StringSplitOptions.None))
            {
                bool active = true;
                string fixedLine = line.Trim();
                string comment = "";

                // STORE ACTIVE FLAG
                if (/*(fixedLine.Contains("=") || fixedLine.Contains("[") && fixedLine.Contains("]")) && fixedLine[0] == ';' ||*/
                    fixedLine.Length >= 2 && fixedLine.Substring(0,2) == ";-")
                {
                    active = false;
                    fixedLine = Regex.Replace(fixedLine, @"^([;-]+)", "").Trim();
                }

                // STORE COMMENTS
                int pos = fixedLine.IndexOf(";");
                if (pos >= 0)
                {
                    comment = fixedLine.Substring(pos + 1).Trim();
                    fixedLine = fixedLine.Substring(0, pos).Trim();
                }

                if (String.IsNullOrEmpty(fixedLine) || !fixedLine.Contains("="))
                {
                    list.Add(new CfgLine(active, fixedLine, "", comment));
                } else
                {
                    list.Add(new CfgLine(active, fixedLine.Split('=')[0].Trim().ToLower(), fixedLine.Split('=')[1].Trim(), comment));
                }
            }

            //foreach (var test in list)
                //Console.WriteLine("<" + test.Active + "> <" + test.Name + "> <" + test.Value + "> <" + test.Comment + ">");

            return list;
        }

        private CfgFile parseCfg(string file, List<CfgLine> lines)
        {
            List<CfgSection> cfgSections = new List<CfgSection>();
            List<CfgLine> cfgLines = new List<CfgLine>();

            string currentSection = "";

            foreach (var line in lines)
            {
                // SECTION START CHECK
                if (!String.IsNullOrEmpty(line.Name) || !String.IsNullOrEmpty(line.Comment))
                {
                    if (!String.IsNullOrEmpty(line.Name) && line.Name[0] == '[')
                    {
                        // ADD FINISHED SECTION
                        if (currentSection != "")
                            cfgSections.Add(new CfgSection(true, currentSection, cfgLines));

                        // PREPARE FOR NEW SECTION
                        currentSection = line.Name.ToUpper();
                        cfgLines = new List<CfgLine>();

                    }
                    else if (currentSection != "")
                        cfgLines.Add(line);
                }
            }

            return new CfgFile(true, file, cfgSections);
        }

        public void splitCfg(string aircraftDirectory)
        {
            List<CfgFile> cfgFiles = new List<CfgFile>();
            processCfgfiles(aircraftDirectory + "\\", true);

            if (cfgTemplates.Count > 0 && cfgFileExists("aircraft.cfg"))
            {
                foreach (var aircraftSection in cfgAircraft[0].Sections)
                {
                    CfgSection cfgTempSection = null;
                    string cfgTemplateFile = ".unknown.cfg";

                    // FIND SECTION MATCH IN TEMPLATES
                    foreach (CfgFile tplfiles in cfgTemplates)
                    {
                        CfgSection cfgTemplateSection = tplfiles.Sections.Find(x => x.Name == aircraftSection.Name);

                        if (cfgTemplateSection == null)
                            cfgTemplateSection = tplfiles.Sections.Find(x => x.Name.Contains(".") && aircraftSection.Name.Contains(".") && x.Name.Split('.')[0] == aircraftSection.Name.Split('.')[0]);

                        if (cfgTemplateSection != null)
                        {
                            cfgTemplateFile = tplfiles.Name;
                            
                            // DEEP COPY FOR COMPARISON PURPOSE
                            cfgTempSection = new CfgSection(cfgTemplateSection.Active, cfgTemplateSection.Name, new List<CfgLine>());
                            foreach (var cfgTempLine in cfgTemplateSection.Lines)
                                cfgTempSection.Lines.Add(new CfgLine(cfgTempLine.Active, cfgTempLine.Name, cfgTempLine.Value, cfgTempLine.Comment));

                            break;
                        }
                    }

                    // BUILD MIXED SECTION
                    List<CfgLine> cfgLines = new List<CfgLine>();

                    // ADD LEGACY LINES
                    cfgLines.Add(new CfgLine(true, "", "", ""));
                    foreach (CfgLine aircraftLine in aircraftSection.Lines)
                    {
                        CfgLine cfgTemplateLine = null;

                        if (cfgTempSection != null)
                        {
                            cfgTemplateLine = cfgTempSection.Lines.Find(x => x.Name == aircraftLine.Name ||
                            x.Name.Contains(".") && aircraftLine.Name.Contains(".") && x.Name.Split('.')[0] == aircraftLine.Name.Split('.')[0]);
                            
                            if (cfgTemplateLine != null) // ATTRIBUTE FOUND IN TEMPLATE
                            {
                                cfgTempSection.Lines.Remove(cfgTemplateLine);
                                if (String.IsNullOrEmpty(aircraftLine.Comment))
                                    aircraftLine.Comment = cfgTemplateLine.Comment;
                            }
                            // NOT FOUND
                            else if (!String.IsNullOrEmpty(aircraftLine.Name) && !String.IsNullOrEmpty(aircraftLine.Value) && !aircraftLine.Name.Contains("."))
                            {
                                //aircraftLine.Active = false;
                                aircraftLine.Comment += " ### ";
                            }
                        }
                        // SECTION NOT FOUND
                        else
                        {
                            aircraftLine.Active = false;
                        }

                        cfgLines.Add(aircraftLine);
                    }

                    // ADD MODERN LINES
                    if (cfgTempSection != null && cfgTempSection.Lines.Count > 0) {
                        cfgLines[0].Comment = "LEGACY";
                        cfgLines.Add(new CfgLine(true, "", "", "MODERN"));
                        foreach (CfgLine cfgTemplateLine in cfgTempSection.Lines)
                        {
                            cfgTemplateLine.Active = false;
                            cfgLines.Add(cfgTemplateLine);
                        }
                    }

                    CfgSection cfgSection = new CfgSection(true, aircraftSection.Name, cfgLines);

                    // ADD SECTION TO FILES LIST
                    if (cfgFiles != null && cfgFiles.Find(x => x.Name == cfgTemplateFile) != null)
                    {
                        cfgFiles.Find(x => x.Name == cfgTemplateFile).Sections.Add(cfgSection);
                    }
                    else
                    {
                        List<CfgSection> cfgSections = new List<CfgSection>();
                        
                        cfgSections.Add(new CfgSection(true, "[VERSION]", new List<CfgLine>()));
                        cfgSections[0].Lines.Add(new CfgLine(true, "major", "1", ""));
                        cfgSections[0].Lines.Add(new CfgLine(true, "minor", "0", ""));

                        cfgSections.Add(cfgSection);
                        cfgFiles.Add(new CfgFile(true, cfgTemplateFile, cfgSections));
                    }

                }

                // RENAME ORIGINAL AIRCRAFT.CFG
                File.Move(aircraftDirectory + "\\aircraft.cfg", aircraftDirectory + "\\.aircraft.cfg");

                // SAVE FILES
                foreach (CfgFile cfgFile in cfgFiles)
                {
                    saveCfgFile(aircraftDirectory, cfgFile);
                }

                //cfgAircraft = cfgFiles;
                processCfgfiles(aircraftDirectory + "\\", true);
            }


        }

        public bool cfgFileExists(string filename)
        {
            return cfgAircraft != null && cfgAircraft.Count > 0 && cfgAircraft.Find(x => x.Name == filename) != null;
        }

        public void saveCfgFiles(string aircraftDirectory, string[] files)
        {
            foreach (CfgFile cfgFile in cfgAircraft)
            {
                if (files.Length > 0 && !files.Contains(cfgFile.Name))
                    continue;

                saveCfgFile(aircraftDirectory, cfgFile);
            }
        }
        void saveCfgFile(string aircraftDirectory, CfgFile cfgFile)
        {
            lastChangeTimestamp = DateTime.UtcNow.Ticks;

            if (File.Exists(aircraftDirectory + "\\" + cfgFile.Name))
            {
                try { File.Delete(aircraftDirectory + "\\" + cfgFile.Name); }
                catch (Exception)
                {
                    MessageBox.Show("Can't update file " + aircraftDirectory + "\\" + cfgFile.Name);
                    return;
                }
            }


            if (!File.Exists(aircraftDirectory + "\\" + cfgFile.Name))
            {
                using (FileStream fs = File.Create(aircraftDirectory + "\\" + cfgFile.Name))
                {
                    byte[] text = new UTF8Encoding(true).GetBytes("");// new UTF8Encoding(true).GetBytes("[VERSION]" + System.Environment.NewLine + "major = 1" + System.Environment.NewLine + "minor = 0" + System.Environment.NewLine);
                    fs.Write(text, 0, text.Length);
                }
            }

            using (FileStream fs = File.Open(aircraftDirectory + "\\" + cfgFile.Name, FileMode.Append, FileAccess.Write, FileShare.Write))
            {
                foreach (CfgSection cfgSection in cfgFile.Sections)
                {
                    byte[] text = new UTF8Encoding(true).GetBytes(System.Environment.NewLine + (!cfgSection.Active ? ";*" : "") + cfgSection.Name + System.Environment.NewLine + System.Environment.NewLine);
                    fs.Write(text, 0, text.Length);


                    foreach (CfgLine cfgLine in cfgSection.Lines)
                    {
                        if (!String.IsNullOrEmpty(cfgLine.Name) || !String.IsNullOrEmpty(cfgLine.Value) || !String.IsNullOrEmpty(cfgLine.Comment))
                        {
                            if (String.IsNullOrEmpty(cfgLine.Name) && String.IsNullOrEmpty(cfgLine.Value))
                                text = new UTF8Encoding(true).GetBytes("; " + cfgLine.Comment + System.Environment.NewLine);
                            else
                                text = new UTF8Encoding(true).GetBytes((!cfgLine.Active ? ";-" : "") + cfgLine.Name + " = " + cfgLine.Value + " ; " + cfgLine.Comment + System.Environment.NewLine);
                            fs.Write(text, 0, text.Length);
                        }
                    }
                }
            }
        }

        public string[] getSectionsList(string aircraftDirectory, string filename)
        {
            CfgFile availableSections = cfgTemplates.Find(x => x.Name == filename);
            CfgFile installedSections = cfgAircraft.Find(x => x.Name == filename);
            string[] sections = new string[100];

            if (availableSections != null)
            {
                int i = 0;
                foreach (var cockpitSection in availableSections.Sections)
                {
                    if (installedSections.Sections.Find(x => x.Name == cockpitSection.Name) != null )
                        sections[i] = cockpitSection.Name + Environment.NewLine;
                    else
                        sections[i] = "-" + cockpitSection.Name.ToUpper() + Environment.NewLine;
                    i++;
                }
            }

            return sections;
        }

        public void insertSections(string aircraftDirectory, string filename, string[] sections, bool active)
        {
            if (cfgFileExists(filename) && sections.Length > 0)
            {
                string message = "";
                CfgFile availableSections = cfgTemplates.Find(x => x.Name == filename);
                CfgFile cfgFile = cfgAircraft.Find(x => x.Name == filename);

                if (availableSections != null)
                {
                    foreach (var section in sections)
                    {
                        if (String.IsNullOrEmpty(section))
                            break;

                        foreach (var sect in availableSections.Sections)
                        {
                            var pattern = @"\[(.*?)\]";

                            if (Regex.Matches(sect.Name, pattern)[0].Groups[1].ToString().Trim() == Regex.Matches(section, pattern)[0].Groups[1].ToString().Trim())
                            {
                                //Console.WriteLine("availableGaugeLines found in section " + sect.Name + " lines " + sect.Lines.Count);
                                List <CfgLine> newLines = new List<CfgLine>();

                                foreach (var cfgLine in sect.Lines)
                                {
                                    // AIRSPEED INDICATOR ADJUSTMENTS
                                    if (sect.Name.ToString() == "[AIRSPEED]") {
                                        string value = getCfgValue("cruise_speed", "flight_model.cfg");
                                        if (value != "" && int.TryParse(value.Contains('.') ? value.Trim('"').Trim().Split('.')[0] : value.Trim('"').Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int cruiseSpeed) && cruiseSpeed > 0)
                                        {
                                            switch (cfgLine.Name.ToString())
                                            {
                                                case "white_start":
                                                    cfgLine.Value = Math.Max(20, cruiseSpeed / 3).ToString();
                                                    break;
                                                case "white_end":
                                                case "green_start":
                                                    cfgLine.Value = Math.Max(30, cruiseSpeed / 2).ToString(); ;
                                                    break;
                                                case "green_end":
                                                case "highlimit":
                                                    cfgLine.Value = (cruiseSpeed).ToString();
                                                    break;
                                                case "max":
                                                    cfgLine.Value = (1.1 * cruiseSpeed).ToString();
                                                    break;
                                            }
                                        }
                                    }

                                    newLines.Add(new CfgLine(active, cfgLine.Name, cfgLine.Value, cfgLine.Comment));
                                }

                                //if (sect.Name.ToString() == "[AIRSPEED]")
                                    //message += "AIRSPEED section values was calculated from cruise_speed, you'll need to adjust them manually";

                                CfgSection newSection = new CfgSection(true, sect.Name, newLines);
                                cfgFile.Sections.Add(newSection);
                                break;
                            }
                        }
                    }

                    saveCfgFile(aircraftDirectory, cfgFile);
                    
                    if (message != "")
                        MessageBox.Show(message);
                }
                else
                {
                    Console.WriteLine("availableSections is null: cockpit");
                }
            }
        }

        public string[] getLights(string aircraftDirectory)
        {
            string[] lightsList = new string[100];
            int i = 0;

            if (cfgFileExists("systems.cfg"))
            {
                CfgSection section = cfgAircraft.Find(x => x.Name == "systems.cfg").Sections.Find(x => x.Name == "[LIGHTS]");
                if (section != null)
                    foreach (CfgLine line in section.Lines)
                        if (line.Active && line.Name.StartsWith("light."))
                        {
                            lightsList[i] = line.Name + " = " + line.Value;
                            i++;
                        }
            }

            return lightsList;
        }

        public string[] getContactPoints(string aircraftDirectory)
        {
            string[] contactPointsList = new string[100];
            int i = 0;

            if (cfgFileExists("flight_model.cfg"))
            {
                CfgSection section = cfgAircraft.Find(x => x.Name == "flight_model.cfg").Sections.Find(x => x.Name == "[CONTACT_POINTS]");

                if (section != null)
                    foreach (CfgLine line in section.Lines)
                        if (line.Active && line.Name.StartsWith("point."))
                        {
                            contactPointsList[i] = line.Name + " = " + line.Value;
                            //Console.WriteLine(contactPointsList[i]);
                            i++;
                        }
            }

            return contactPointsList;
        }

        public void adjustEnginesPower(string aircraftDirectory, double multiplier)
        {
            CfgFile cfgFile = cfgAircraft.Find(x => x.Name == "engines.cfg");

            if (cfgFile != null)
            {
                string attr = "";
                string sect = "";

                string engine_type = getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]");
                if (engine_type == "1" || engine_type == "5") // JET
                {
                    attr = "static_thrust";
                    sect = "[TURBINEENGINEDATA]";
                }
                else if (engine_type == "0") // PISTON
                {
                    attr = "power_scalar";
                    sect = "[PISTON_ENGINE]";
                } else if (engine_type == "5") // TURBOPROP
                {
                    attr = "power_scalar";
                    sect = "[TURBOPROP_ENGINE]";
                }

                if (attr != "" && sect != "") {
                    string thrust = getCfgValue(attr, "engines.cfg", sect);
                    if (thrust != "")
                    {
                        double.TryParse(thrust.Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double num);
                        if (num > 0)
                        {
                            double newThrust = Math.Max((num * multiplier), 0.001);
                            if (attr == "power_scalar")
                                newThrust = Math.Min((num * multiplier), 1.9);
                            string newThrustString = newThrust.ToString("0.###");
                            MessageBoxResult messageBoxResult = MessageBox.Show("Old value: " + num + Environment.NewLine + "New value: " + newThrustString, "Engines power adjustment", System.Windows.MessageBoxButton.YesNo);
                            if (messageBoxResult == MessageBoxResult.Yes)
                            {
                                setCfgValue(aircraftDirectory, attr, newThrustString, "engines.cfg", sect);
                                saveCfgFiles(aircraftDirectory, new string[] {"engines.cfg"});
                            }
                        }
                    }
                }
            }
        }


        // MISC STUFF

        // IF NO ACTIVE FLAG PROVIDED - ATTRIBUTE WILL BE SET TO 'ACTIVE'
        // SECTION CAN BE EMPTY BUT NEW VALUE WILL BE NOT CREATED IF NOT FOUND

        // FILE NAME OCNVERTED TO LOWERCASE
        // SECTION NAME CONVERTED TO UPPERCASE
        // ATTRIBUTE NAME CONVERTED TO LOWERCASE
        // VALUE AND COMMENT - WHITESPACES TRIM ONLY
        public bool setCfgValue(string aircraftDirectory, string attrname, string value, string filename, string sectionname = "", bool active = true, string comment = "")
        {
            // TODO: ADD MISSING SECTION
            if (cfgAircraft.Count >= 1)
            {
                foreach (CfgFile cfgFile in cfgAircraft)
                {
                    if (!String.IsNullOrEmpty(filename) && cfgFile.Name != filename.Trim().ToLower())
                        continue;

                    foreach (CfgSection cfgSection in cfgFile.Sections)
                    {
                        if (!String.IsNullOrEmpty(sectionname) && cfgSection.Name != sectionname.Trim().ToUpper())
                            continue;

                        foreach (CfgLine cfgLine in cfgSection.Lines)
                        {
                            // UPDATE EXISTING ATTR
                            if (cfgLine.Name == attrname.Trim().ToLower())
                            {
                                cfgLine.Active = active;
                                cfgLine.Value = value.Trim();
                                if (!String.IsNullOrEmpty(comment))
                                    cfgLine.Comment = comment.Trim();

                                Console.WriteLine("setCfgExisting: " + cfgFile.Name + "/" + cfgSection.Name + "/" + cfgLine.Name + " = " + cfgLine.Value);
                                //saveCfgFile(aircraftDirectory, cfgFile);

                                return true;
                            }
                        }

                        // CREATE NEW ATTR
                        if (!String.IsNullOrEmpty(filename) && !String.IsNullOrEmpty(sectionname))
                        {
                            CfgLine cfgLine = new CfgLine(active, attrname.Trim().ToLower(), value, comment);
                            cfgSection.Lines.Add(cfgLine);

                            Console.WriteLine("setCfgNew: " + cfgFile.Name + "/" + cfgSection.Name + "/" + cfgLine.Name + " = " + cfgLine.Value);

                            //saveCfgFile(aircraftDirectory, cfgFile);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // RETURN EMPTY IF NOT FOUND
        public string getCfgValue(string attrname, string filename, string sectionname = "", bool ignoreInactive = false)
        {
            if (cfgAircraft.Count >= 1)
            {
                foreach (CfgFile cfgFile in cfgAircraft)
                {
                    if (!String.IsNullOrEmpty(filename) && cfgFile.Name != filename.Trim().ToLower())
                        continue;

                    foreach (CfgSection section in cfgFile.Sections)
                    {
                        if (!String.IsNullOrEmpty(sectionname) && section.Name != sectionname.Trim().ToUpper())
                            continue;

                        CfgLine line = section.Lines.Find(x => x.Name == attrname);
                        if (line != null && (line.Active || ignoreInactive))
                        {
                            Console.WriteLine("getCfgValue: " + filename + " / " + (sectionname != "" ? sectionname : "ANY") + " / " + attrname + " = " + line.Value);
                            return line.Value;
                        }
                    }
                }
            }
            Console.WriteLine("getCfgValue: " + filename + " / " + (sectionname != "" ? sectionname : "ANY") + " / " + attrname + " NOT FOUND");
            return "";
        }

        public bool setCfgValueStatus(string aircraftDirectory, string attrname, string filename, string sectionname = "", bool active = true)
        {
            CfgFile cfgFile = cfgAircraft.Find(x => x.Name == filename);
            if (cfgFile != null)
            {
                List<CfgLine> ligtsList = cfgAircraft.Find(x => x.Name == filename).Sections.Find(x => x.Name == sectionname).Lines;
                if (ligtsList != null)
                {
                    ligtsList.Find(x => x.Name == attrname).Active = active;
                    //saveCfgFile(aircraftDirectory, cfgFile);
                    return true;
                }
            }

            return false;
        }

        public class CfgFile
        {
            public bool Active { get; set; }
            public string Name { get; set; }
            public List<CfgSection> Sections { get; set; }
            public CfgFile(bool active, string name, List<CfgSection> sections)
            {
                Active = active;
                Name = name;
                Sections = sections;
            }
        }
        public class CfgSection
        {
            public bool Active { get; set; }
            public string Name { get; set; }
            public List<CfgLine> Lines { get; set; }
            public CfgSection(bool active, string name, List<CfgLine> lines)
            {
                Active = active;
                Name = name;
                Lines = lines;
            }
        }
        public class CfgLine
        {
            public bool Active { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public string Comment { get; set; }
            public object Sections { get; internal set; }

            public CfgLine(bool active, string name, string value, string comment)
            {
                Active = active;
                Name = name;
                Value = value;
                Comment = comment;
            }
        }
    }
}
