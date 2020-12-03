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
    public class cfgHelper
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
        private bool DEBUG = false;


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

        public void resetCfgfiles()
        {
            cfgAircraft = null;
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
                    fixedLine = fixedLine.Substring(2).Trim();
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

        public CfgFile parseCfg(string file, List<CfgLine> lines)
        {
            List<CfgSection> cfgSections = new List<CfgSection>();
            List<CfgLine> cfgLines = new List<CfgLine>();

            string currentSection = "";
            bool lastStatus = true;

            foreach (var line in lines)
            {
                // SECTION START CHECK
                if (!String.IsNullOrEmpty(line.Name) || !String.IsNullOrEmpty(line.Comment))
                {
                    if (!String.IsNullOrEmpty(line.Name) && line.Name[0] == '[')
                    {
                        // ADD FINISHED SECTION
                        if (currentSection != "")
                            cfgSections.Add(new CfgSection(lastStatus, currentSection, cfgLines));

                        // PREPARE FOR NEW SECTION
                        currentSection = line.Name.ToUpper();
                        lastStatus = line.Active;
                        cfgLines = new List<CfgLine>();

                    }
                    else if (currentSection != "")
                        cfgLines.Add(line);
                }
            }

            return new CfgFile(true, file, cfgSections);
        }

        public void splitCfg(string aircraftDirectory, string singleFile = "")
        {
            List<CfgFile> cfgFiles = new List<CfgFile>();
            processCfgfiles(aircraftDirectory + "\\", true);

            if (cfgTemplates.Count > 0 && cfgFileExists("aircraft.cfg"))
            {
                foreach (var aircraftSection in cfgAircraft[0].Sections)
                {
                    CfgSection cfgTempSection = null;
                    string cfgTemplateFile = singleFile == "" ? ".unknown.cfg" : "aircraft.cfg";

                    // FIND SECTION MATCH IN TEMPLATES
                    foreach (CfgFile tplfiles in cfgTemplates)
                    {
                        if (singleFile != "" && tplfiles.Name != singleFile)
                            continue;

                        CfgSection cfgTemplateSection = tplfiles.Sections.Find(x => x.Name == aircraftSection.Name);

                        // NUMERUOUS SECTION FIX
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
                        else if (singleFile == "")
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
            bool aircraftCfgSaved = false;

            foreach (string file in files)
            {
                string filename = file;

                if (!cfgFileExists(filename)) { filename = "aircraft.cfg"; }

                if (filename == "aircraft.cfg" && aircraftCfgSaved) // SKIP AIRCRAFT.CFG
                    continue;
                else if (filename == "aircraft.cfg") // SKIP NEXT TIME 
                    aircraftCfgSaved = true;

                saveCfgFile(aircraftDirectory, cfgAircraft.Find(x => x.Name == filename));
            }
        }
        public void saveCfgFile(string aircraftDirectory, CfgFile cfgFile)
        {
            string path = aircraftDirectory + "\\" + cfgFile.Name;
            Console.WriteLine("Saving config file " + cfgFile.Name + " into " + path);
            lastChangeTimestamp = DateTime.UtcNow.Ticks;

            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    MessageBox.Show("Can't update file " + path);
                    return;
                }
            }


            if (!File.Exists(path))
            {
                using (FileStream fs = File.Create(path))
                {
                    byte[] text = new UTF8Encoding(true).GetBytes("");// new UTF8Encoding(true).GetBytes("[VERSION]" + System.Environment.NewLine + "major = 1" + System.Environment.NewLine + "minor = 0" + System.Environment.NewLine);
                    fs.Write(text, 0, text.Length);
                }
            }

            using (FileStream fs = File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Write))
            {
                foreach (CfgSection cfgSection in cfgFile.Sections)
                {
                    byte[] text = new UTF8Encoding(true).GetBytes(System.Environment.NewLine + (!cfgSection.Active ? ";-" : "") + cfgSection.Name + System.Environment.NewLine + System.Environment.NewLine);
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
            CfgFile installedSections = cfgFileExists(filename) ? cfgAircraft.Find(x => x.Name == filename) : cfgAircraft.Find(x => x.Name == "aircraft.cfg");
            string[] sections = new string[100];

            if (availableSections != null)
            {
                int i = 0;
                foreach (var cockpitSection in availableSections.Sections)
                {
                    if (installedSections != null && installedSections.Sections.Find(x => x.Name == cockpitSection.Name) != null )
                        sections[i] = cockpitSection.Name + Environment.NewLine;
                    else
                        sections[i] = "-" + cockpitSection.Name.ToUpper() + Environment.NewLine;
                    i++;
                }
            }

            return sections;
        }

        public void insertSections(string aircraftDirectory, string sourceFilename, string targetFilename, string[] sections, bool active)
        {
            if (cfgFileExists(targetFilename) && sections.Length > 0)
            {
                string message = "";
                CfgFile availableSections = cfgTemplates.Find(x => x.Name == sourceFilename);
                CfgFile cfgFile = cfgAircraft.Find(x => x.Name == targetFilename);

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

            CfgSection section = cfgFileExists("systems.cfg") ? cfgAircraft.Find(x => x.Name == "systems.cfg").Sections.Find(x => x.Name == "[LIGHTS]") :
                cfgAircraft.Find(x => x.Name == "aircraft.cfg").Sections.Find(x => x.Name == "[LIGHTS]");
            if (section != null)
                foreach (CfgLine line in section.Lines)
                    if (line.Active && line.Name.StartsWith("light."))
                    {
                        lightsList[i] = line.Name + " = " + line.Value;
                        i++;
                    }

            return lightsList;
        }

        public int getTaxiLights(string aircraftDirectory)
        {
            int i = 0;

            CfgSection section = cfgFileExists("systems.cfg") ? cfgAircraft.Find(x => x.Name == "systems.cfg").Sections.Find(x => x.Name == "[LIGHTS]") :
                cfgAircraft.Find(x => x.Name == "aircraft.cfg").Sections.Find(x => x.Name == "[LIGHTS]");
            if (section != null)
                foreach (CfgLine line in section.Lines)
                    if (line.Active && line.Name.StartsWith("lightdef.") && (line.Value.ToLower().Contains("type:6") || line.Value.ToLower().Contains("type:6")) )
                        i++;

            return i;
        }

        public List<string> getContactPoints(string aircraftDirectory, string type = "")
        {
            List<string> contactPointsList = new List<string>();

            CfgSection section = cfgFileExists("flight_model.cfg") ? cfgAircraft.Find(x => x.Name == "flight_model.cfg").Sections.Find(x => x.Name == "[CONTACT_POINTS]") :
                cfgAircraft.Find(x => x.Name == "aircraft.cfg").Sections.Find(x => x.Name == "[CONTACT_POINTS]");

            if (section != null)
                foreach (CfgLine line in section.Lines)
                    if (line.Active && line.Name.StartsWith("point.") && ( type == "" || line.Value.Contains(',') && 
                        (line.Value.Split(',')[0].Trim() == type || line.Value.Split(',')[0].Trim().Contains('.') && line.Value.Split(',')[0].Trim().Split('.')[0] == type) ))
                        contactPointsList.Add(line.Name + " = " + line.Value);

            return contactPointsList;
        }

        public void adjustEnginesPower(string aircraftDirectory, double multiplier)
        {
            CfgFile cfgFile = cfgFileExists("engines.cfg") ? cfgAircraft.Find(x => x.Name == "engines.cfg") :
                cfgAircraft.Find(x => x.Name == "aircraft.cfg");

            if (cfgFile != null)
            {
                string attr = "";
                string sect = "";

                string engine_type = getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]");
                if (engine_type.Contains('.'))
                    engine_type = engine_type.Split('.')[0];
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
            if (!cfgFileExists(filename))
                filename = "aircraft.cfg";

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
            if (!cfgFileExists(filename))
                filename = "aircraft.cfg";

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
                            if (DEBUG)
                                Console.WriteLine("getCfgValue: " + filename + " / " + (sectionname != "" ? sectionname : "ANY") + " / " + attrname + " = " + line.Value);
                            return line.Value;
                        }
                    }
                }
            }

            if (DEBUG)
                Console.WriteLine("getCfgValue: " + filename + " / " + (sectionname != "" ? sectionname : "ANY") + " / " + attrname + " NOT FOUND");

            return "";
        }

        public bool setCfgValueStatus(string aircraftDirectory, string attrname, string filename, string sectionname = "", bool active = true)
        {
            if (!cfgFileExists(filename))
                filename = "aircraft.cfg";

            CfgFile cfgFile = cfgAircraft.Find(x => x.Name == filename);
            if (cfgFile != null)
            {
                CfgSection ligtsList = cfgFile.Sections.Find(x => x.Name == sectionname);
                if (ligtsList != null)
                {
                    CfgLine line = ligtsList.Lines.Find(x => x.Name == attrname);
                    if (line != null) {
                        line.Active = active;

                        if (DEBUG)
                            Console.WriteLine("setCfgValueStatus: " + filename + " / " + (sectionname != "" ? sectionname : "ANY") + " / " + attrname + " set to " + active);
                        return true;
                    }
                }
            }

            if (DEBUG)
                Console.WriteLine("setCfgValueStatus: " + filename + " / " + (sectionname != "" ? sectionname : "ANY") + " / " + attrname + " NOT FOUND");
            return false;
        }

        public CfgFile setCfgSectionStatus(CfgFile cfgFile, string sectionname, bool active = true)
        {
            if (cfgFile != null)
            {
                CfgSection ligtsList = cfgFile.Sections.Find(x => x.Name == sectionname);
                if (ligtsList != null)
                {
                    if (DEBUG)
                        Console.WriteLine("setCfgValueStatus: " + cfgFile.Name + " / " + sectionname + " set to " + active);

                    ligtsList.Active = active;
                    foreach (CfgLine line in ligtsList.Lines)
                        line.Active = active;

                    return cfgFile;
                }
            }

            if (DEBUG)
                Console.WriteLine("setCfgValueStatus: " + cfgFile.Name + " / " + sectionname + " NOT FOUND");
            return cfgFile;
        }

        public List<string> getInteriorModels(string aircraftDirectory)
        {
            List<string> models = new List<string>();
            var cfgFiles = Directory.EnumerateFiles(aircraftDirectory, "model.cfg", SearchOption.AllDirectories);
            foreach (string currentFile in cfgFiles)
            {
                if (Path.GetFileName(currentFile)[0] != '.')
                {
                    foreach (var modelString in readCSV(File.ReadAllText(currentFile)))
                    {
                        if (modelString.Name == "interior")
                            models.Add(Path.GetDirectoryName(currentFile) + "\\" + modelString.Value + ".mdl");
                    }
                }
            }

            return models;
        }

        public List<string> getExteriorModels(string aircraftDirectory)
        {
            List<string> models = new List<string>();
            var cfgFiles = Directory.EnumerateFiles(aircraftDirectory, "model.cfg", SearchOption.AllDirectories);
            foreach (string currentFile in cfgFiles)
            {
                if (Path.GetFileName(currentFile)[0] != '.')
                {
                    foreach (var modelString in readCSV(File.ReadAllText(currentFile)))
                    {
                        if (modelString.Name == "normal")
                            models.Add(Path.GetDirectoryName(currentFile) + "\\" + modelString.Value + ".mdl");
                    }
                }
            }

            return models;
        }

        public List<string> getSounds(string aircraftDirectory)
        {
            List<string> sounds = new List<string>();
            var cfgFiles = Directory.EnumerateFiles(aircraftDirectory, "sound.cfg", SearchOption.AllDirectories);
            foreach (string currentFile in cfgFiles)
            {
                if (Path.GetFileName(currentFile)[0] != '.')
                {
                    List<CfgLine> cfgLines = readCSV(File.ReadAllText(currentFile) + "\r\n[]");
                    //Console.WriteLine(cfgLines.First().Name);
                    CfgFile tempFile = parseCfg(currentFile.Replace(aircraftDirectory, "").TrimStart('\\'), cfgLines);

                    foreach (CfgSection section in tempFile.Sections) {
                        if (section.Name.Length > 0)
                            sounds.Add((!section.Active ? "-" : "") + currentFile.Replace(aircraftDirectory, "") + "\\" + section.Name);
                    }
                }
            }

            return sounds;
        }

        public void updateSounds(string aircraftDirectory, List<string> checkboxes)
        {
            string currentFile = "";
            CfgFile tempFile = null;

            foreach (string checkbox in checkboxes)
            {
                if (checkbox.Contains('['))
                {
                    string filename = checkbox.Split('[')[0].TrimEnd('\\');
                    string filepath = aircraftDirectory + filename;
                    if (currentFile != filename && File.Exists(filepath))
                    {
                        if (tempFile != null) // SAVE PREVIOUS FILE
                            saveCfgFile(aircraftDirectory, tempFile);

                        currentFile = filename;
                        List<CfgLine> cfgLines = readCSV(File.ReadAllText(filepath) + "\r\n[]");
                        tempFile = parseCfg(filename.TrimStart('\\'), cfgLines);
                        foreach (CfgSection section in tempFile.Sections)
                            setCfgSectionStatus(tempFile, section.Name, false);
                    }

                    string sectionname =  '[' + checkbox.Split('[')[1];
                    setCfgSectionStatus(tempFile, sectionname, true);
                }
            }

            if (tempFile != null) // SAVE PREVIOUS FILE
                saveCfgFile(aircraftDirectory, tempFile);
        }

        public bool cfgSectionExists(string filename, string sectionName)
        {
            if (!cfgFileExists(filename))
                filename = "aircraft.cfg";

            return cfgAircraft.Find(x => x.Name == filename).Sections.Find(x => x.Name == sectionName) != null;
        }

        public List<string> getMissingLiveryModels(string aircraftDirectory)
        {
            List<string> missingLiveryModels = new List<string>();
            if (cfgFileExists("aircraft.cfg"))
            {
                for (int k = 0; k <= 99; k++)
                {
                    if (cfgSectionExists("aircraft.cfg", "[FLTSIM." + k + "]"))
                    {
                        string texture = getCfgValue("texture", "aircraft.cfg", "[FLTSIM." + k + "]").ToLower().Trim('"').Trim();
                        string model = getCfgValue("model", "aircraft.cfg", "[FLTSIM." + k + "]").ToLower().Trim('"').Trim();

                        if (!String.IsNullOrEmpty(texture) && (String.IsNullOrEmpty(model) || model != texture))
                            missingLiveryModels.Add("[FLTSIM." + k + "] (texture:"+texture+" model:"+ (String.IsNullOrEmpty(model) ? "empty" : model) + ")");
                    }
                }
            }
            
            return missingLiveryModels;
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

        public List<double[]>[] parseCfgDoubleTable(string table)
        {
            List<double[]>[] data = new List<double[]>[] { new List<double[]>(), new List<double[]>() };

            List<double[]> table1 = new List<double[]>();
            List<double[]> table2 = new List<double[]>();

            foreach (string value in table.Split(','))
            {

                string[] values = value.Trim().Split(':');
                if (values.Length == 3)
                {
                    Double.TryParse(values[0], out double res0);
                    Double.TryParse(values[1], out double res1);
                    Double.TryParse(values[2], out double res2);
                    table1.Add(new double[] { res0, res1 });
                    table2.Add(new double[] { res0, res2 });
                }
            }

            data[0] = table1;
            data[1] = table2;

            return data;
        }
    }
}
