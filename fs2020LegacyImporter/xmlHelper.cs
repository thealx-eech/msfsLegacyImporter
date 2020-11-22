using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace msfsLegacyImporter
{
    class xmlHelper
    {
        private double[] prevPoint;
        private double prevAngle;
        private int rotationDirection;
        string logfile = "";
        string logcontent = "";
        string[] imagesExceptions = new string[]
        {
            "hsi_hsi_reflection",
            "compass_compass_highlight",
            "AN_24_GROZA_BLIK",
            //"HUD_pfd_background"
        };
        string[] imagesForceTransparancy = new string[]
        {
            "pfd_attitude_inner_att_mask",
            "pfd_attitude_sky_ground_strip_mask",
            "compass_strip_mask"
        };

        string[][] positionExceptions = new string[][]
        {
            new string[] { "HUD_pfd_attitude_ladder", "-25", "-198" },
            new string[] { "HUD_Heading_pointer", "95", "12" }
        };

        private string gaugeGroup;
        private string gaugeSanitizedGroup;
        private string gaugeName;
        private string gaugeSanitizedName;
        private string acSlug;
        private string xPos;
        private string yPos;
        private string gaugeWidth;
        private string gaugeHeight;
        private string scaleUp;
        private string panelDir;
        private int index;
        private string html;
        private string css;
        private string js;
        private string materialName;
        private string InstrumentFolder;
        private string errors;


        public void insertFsxGauge(object sender, string aircraftDirectory, string projectDirectory, string chkContent, float[] atts, cfgHelper CfgHelper, fsxVarHelper FsxVarHelper, jsonHelper JSONHelper)
        {
            string mainFile = aircraftDirectory + "\\" + (string)chkContent;
            string backupFile = Path.GetDirectoryName(mainFile) + "\\." + Path.GetFileName(mainFile);

            if (!File.Exists(mainFile))
                return;

            // PREPARE LOG FILE
            logfile = Path.GetDirectoryName(mainFile) + "\\." + Path.GetFileNameWithoutExtension(mainFile) + ".log.txt";
            errors = "";
            logcontent = "";

            string htmlTpl = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "panelTpl\\panel.html");
            string cssTpl = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "panelTpl\\panel.css");
            string jsTpl = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "panelTpl\\panel.js");

            if (!File.Exists(backupFile))
            {
                CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                File.Copy(mainFile, backupFile);
            }

            // LOAD PANEL CFG
            string content = File.ReadAllText(backupFile);
            List<msfsLegacyImporter.cfgHelper.CfgLine> panelLines = CfgHelper.readCSV(content + "\r\n[]");
            msfsLegacyImporter.cfgHelper.CfgFile panelCfg = CfgHelper.parseCfg(backupFile.Replace(aircraftDirectory + "\\", ""), panelLines);

            msfsLegacyImporter.cfgHelper.CfgFile newPanelFile = new msfsLegacyImporter.cfgHelper.CfgFile(true, mainFile.Replace(aircraftDirectory + "\\", ""), new List<msfsLegacyImporter.cfgHelper.CfgSection>());

            // PROCESS PANEL CFG
            foreach (var panelSection in panelCfg.Sections)
            {
                if (panelSection.Name.Contains("VCOCKPIT"))
                {
                    List<String[]> gaugesList = new List<String[]>();

                    string pixel_size = panelSection.Lines.Find(x => x.Name == "pixel_size") != null ? panelSection.Lines.Find(x => x.Name == "pixel_size").Value : "";
                    string size_mm = panelSection.Lines.Find(x => x.Name == "size_mm") != null ? panelSection.Lines.Find(x => x.Name == "size_mm").Value : "";
                    if (String.IsNullOrEmpty(pixel_size) && !String.IsNullOrEmpty(size_mm))
                        pixel_size = size_mm;
                    else if (String.IsNullOrEmpty(size_mm) && !String.IsNullOrEmpty(pixel_size))
                        size_mm = pixel_size;
                    string file = panelSection.Lines.Find(x => x.Name == "file") != null ? panelSection.Lines.Find(x => x.Name == "file").Value.Trim() : "";
                    string texture = panelSection.Lines.Find(x => x.Name == "texture") != null ? panelSection.Lines.Find(x => x.Name == "texture").Value : "";

                    if (String.IsNullOrEmpty(pixel_size) || String.IsNullOrEmpty(texture))
                    {
                        string msg = "Panel " + panelSection.Name + " does not have enough parameters";
                        errors += msg + Environment.NewLine + Environment.NewLine;
                        writeLog("ERROR: " + msg);
                        continue;
                    }

                    acSlug = Regex.Replace(Path.GetFileName(aircraftDirectory).Replace("\\", "").Trim().ToLower() + "_" + Path.GetDirectoryName(chkContent), @"[^0-9A-Za-z ,.\-_]", "").Replace(" ", "_").Replace(".", "_");
                    materialName = "gauges_" + texture.Replace("$", "").ToLower();
                    int[] size = pixel_size != null && pixel_size.Contains(',') &&
                        int.TryParse(pixel_size.Split(',')[0].Trim(), out int num1) && int.TryParse(pixel_size.Split(',')[1].Trim(), out int num2) ?
                        new int[] { num1, num2 } : new int[] { 0, 0 };
                    int[] size2 = size_mm != null && pixel_size.Contains(',') &&
                        int.TryParse(size_mm.Split(',')[0].Trim(), out int num11) && int.TryParse(size_mm.Split(',')[1].Trim(), out int num22) ?
                        new int[] { num11, num22 } : new int[] { 0, 0 };
                    cfgHelper.CfgSection newPanelSection = new msfsLegacyImporter.cfgHelper.CfgSection(true, panelSection.Name.Replace("VCOCKPIT", "VPainting"), new List<msfsLegacyImporter.cfgHelper.CfgLine>());

                    // PROCESS GAUGES LIST
                    for (int i = 0; i < 1000; i++)
                    {
                        var gauge = panelSection.Lines.Find(x => x.Name == "gauge" + i.ToString("00"));
                        if (gauge != null && gauge.Value.Contains(',') && !gauge.Value.Contains("n_number_plaque"))
                        {
                            string[] gaugeData = gauge.Value.Split(',');
                            if (gaugeData.Length >= 4 && gaugeData[0].Contains('!'))
                            {
                                if (gaugeData.Length == 4) // ADD MISSING HEIGHT
                                    gaugeData = gaugeData.Concat(new string[] { gaugeData[3] }).ToArray();

                                gaugesList.Add(gaugeData);
                            }
                            else
                            {
                                string msg = "Unrecognized gauge string: " + gauge.Value;
                                errors += msg + Environment.NewLine + Environment.NewLine;
                                writeLog("ERROR: " + msg);
                            }
                        }
                    }

                    /*if (Directory.Exists(InstrumentFolder))
                    {
                        MessageBoxResult messageBoxResult = MessageBox.Show("Existing files will be overwritten", "Legacy instruments folder \"" + gaugeGroup + "\" already exists", System.Windows.MessageBoxButton.YesNo);
                        if (messageBoxResult != MessageBoxResult.Yes)
                        {
                            continue;
                        }
                    }
                    else*/

                    // INSERT GAUGES
                    if (gaugesList.Count > 0)
                    {
                        html = "";
                        css = "";
                        js = Environment.NewLine;

                        if (atts[4] == 1)
                        {
                            js += "       // TAXI LIGHTS CONTROL" + Environment.NewLine;
                            js += "       if (SimVar.GetSimVarValue(\"IS GEAR RETRACTABLE\", \"Boolean\")) {" + Environment.NewLine;
                            js += "         var gears_extracted = SimVar.GetSimVarValue(\"GEAR HANDLE POSITION\", \"Boolean\");" + Environment.NewLine;
                            js += "         if (SimVar.GetSimVarValue(\"LIGHT TAXI\", \"bool\") && !gears_extracted)" + Environment.NewLine;
                            js += "            SimVar.SetSimVarValue(\"K:TOGGLE_TAXI_LIGHTS\", \"bool\", false)" + Environment.NewLine;
                            js += "         else if (!SimVar.GetSimVarValue(\"LIGHT TAXI\", \"bool\") && gears_extracted)" + Environment.NewLine;
                            js += "            SimVar.SetSimVarValue(\"K:TOGGLE_TAXI_LIGHTS\", \"bool\", true)" + Environment.NewLine;
                            js += "       }" + Environment.NewLine;
                        }

                        string baseFolder = /*projectDirectory.TrimEnd('\\')*/ Path.GetDirectoryName(projectDirectory.TrimEnd('\\')) + "\\legacy-vcockpits-instruments\\";
                        InstrumentFolder = baseFolder + "html_ui\\Pages\\VLivery\\liveries\\Legacy\\" + acSlug + "\\";

                        if (!Directory.Exists(InstrumentFolder))
                            Directory.CreateDirectory(InstrumentFolder);

                        if (!String.IsNullOrEmpty(file) && !imagesExceptions.Contains(Path.GetFileNameWithoutExtension(file)))
                        {
                            if (File.Exists(Path.GetDirectoryName(mainFile) + "\\" + file))
                            {
                                Bitmap bmp = new Bitmap(Path.GetDirectoryName(mainFile) + "\\" + file);
                                bmp = setBitmapGamma(bmp, atts[0], false);
                                try { bmp.Save(InstrumentFolder + Path.GetFileNameWithoutExtension(file) + ".png", ImageFormat.Png); }
                                catch (Exception e) { MessageBox.Show("Can't save image " + Path.GetFileNameWithoutExtension(file) + ".png" + Environment.NewLine + "Error: " + e.Message); }
                            }
                            else
                            {
                                string msg = "Background image " + Path.GetDirectoryName(mainFile) + "\\" + file + " not found";
                                errors += msg + Environment.NewLine + Environment.NewLine;
                                writeLog("ERROR: " + msg);
                            }
                        }

                        foreach (var gaugeData in gaugesList)
                        {
                            gaugeGroup = Path.GetFileName(gaugeData[0].Split('!')[0].Trim().ToLower());
                            gaugeSanitizedGroup = Regex.Replace(gaugeGroup, @"[^0-9A-Za-z ,.-_]", "").Replace(" ", "_");
                            gaugeName = gaugeData[0].Split('!')[1].Trim();
                            gaugeSanitizedName = sanitizeString(gaugeName);
                            if (!Char.IsLetter(gaugeSanitizedName, 0)) // FIX FIRST DIGIT
                                gaugeSanitizedName = "g" + gaugeSanitizedName;
                            xPos = gaugeData[1].Trim().Trim('"');
                            yPos = gaugeData[2].Trim().Trim('"');
                            gaugeWidth = gaugeData.Length >= 4 ? gaugeData[3].Trim().Trim('"') : "0";
                            gaugeHeight = gaugeData.Length >= 5 ? gaugeData[4].Trim().Trim('"') : gaugeWidth;
                            scaleUp = gaugeData.Length >= 7 ? gaugeData[6].Trim().Trim('"') : "1";


                            //SET BUTTON LABEL
                            Application.Current.Dispatcher.Invoke(() => {
                                if (sender.GetType() == typeof(Button))
                                    ((Button)sender).Content = gaugeName;
                            });

                            // AC CAB
                            panelDir = Path.GetDirectoryName(mainFile) + "\\." + gaugeGroup;

                            // AC SUBFOLDER
                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml"))
                                panelDir = Path.GetDirectoryName(mainFile) + "\\" + gaugeGroup;
                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml"))
                                panelDir = Path.GetDirectoryName(mainFile) + "\\." + gaugeGroup;

                            // FSX CABS
                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml"))
                                panelDir = Path.GetDirectoryName(projectDirectory.TrimEnd('\\')) + "\\legacy-vcockpits-instruments\\.FSX\\" + gaugeGroup;

                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml")) {
                                string msg = "Gauge file " + gaugeGroup + "\\" + gaugeName + ".xml not found";
                                errors += msg + Environment.NewLine + Environment.NewLine;
                                writeLog("ERROR: " + msg);

                                try { if (panelDir == Path.GetDirectoryName(mainFile) + "\\" + gaugeGroup)
                                        Directory.Move(panelDir, Path.GetDirectoryName(mainFile) + "\\." + gaugeGroup); }
                                catch { }

                                continue;
                            }

                            // SET UP GAUGE
                            try
                            {
                                writeLog(Environment.NewLine + "### Processing " + gaugeGroup + "\\" + gaugeName + ".xml ###" + Environment.NewLine);

                                string[] GaugeSize = new string[] { gaugeWidth, gaugeHeight };
                                string[] imgSize = new string[] { "0", "0" };

                                XElement gaugeXml = XElement.Load(panelDir + "\\" + gaugeName + ".xml");
                                if (gaugeXml.Element("SimGauge.Gauge") != null)
                                    gaugeXml = gaugeXml.Element("SimGauge.Gauge");
                                else if (gaugeXml.Element("Gauge") != null)
                                    gaugeXml = gaugeXml.Element("Gauge");

                                html += "		<div id=\"" + gaugeSanitizedName + "\">" + Environment.NewLine;
                                css += materialName + "-element #Mainframe #" + gaugeSanitizedName + " {" + Environment.NewLine + "		position: absolute;" + Environment.NewLine + "		overflow: hidden;" + Environment.NewLine + "		left: " + xPos + "px;" + Environment.NewLine + "		top: " + yPos + "px;" + Environment.NewLine;

                                // MACRO
                                foreach (var macro in gaugeXml.Elements("Macro"))
                                {
                                    if (macro.Attribute("Name") != null && !string.IsNullOrEmpty(macro.Value))
                                        js += "		var " + macro.Attribute("Name").Value + " = " + FsxVarHelper.fsx2msfsSimVar(macro.Value, this, false) + ";" + Environment.NewLine;
                                    else if (macro.Attribute("Name") != null)
                                        js += "		var " + macro.Attribute("Name").Value + " = \"\";" + Environment.NewLine;
                                }

                                // SET BG IMAGE IF NECESSARY
                                XElement mainImage = gaugeXml.Element("Image");
                                if (String.IsNullOrEmpty(Path.GetFileNameWithoutExtension(file)) || atts[1] == 1)
                                {
                                    if (mainImage != null && mainImage.Attribute("Name") != null)
                                    {
                                        string MainImageSource = probablyFixBmpName(mainImage.Attribute("Name").Value);
                                        string MainImageFilename = gaugeSanitizedName + "_" + Path.GetFileNameWithoutExtension(MainImageSource).Trim();

                                        if (mainImage.Attribute("ImageSizes") != null)
                                            imgSize = getXYvalue(mainImage, false);
                                            
                                        if (!String.IsNullOrEmpty(MainImageFilename) && !imagesExceptions.Contains(Path.GetFileNameWithoutExtension(MainImageFilename)))
                                        {
                                            string sourceFile = panelDir + "\\" + MainImageSource;
                                            if (!File.Exists(sourceFile)) // TRY 1024 SUBFOLDER
                                                sourceFile = panelDir + "\\1024\\" + MainImageSource;

                                            if (File.Exists(sourceFile))
                                            {
                                                Bitmap bmp = new Bitmap(sourceFile);
                                                bmp = setBitmapGamma(bmp, atts[0],
                                                    mainImage.Element("Transparent") != null && mainImage.Element("Transparent").Value == "True" ||
                                                        mainImage.Attribute("Alpha") != null && mainImage.Attribute("Alpha").Value == "Yes" ||
                                                        imagesForceTransparancy.Contains(Path.GetFileNameWithoutExtension(sourceFile)));

                                                if (imgSize[0] == "0" && imgSize[1] == "0")
                                                    imgSize = new string[] { bmp.Width.ToString(), bmp.Height.ToString() };

                                                try { bmp.Save(InstrumentFolder + MainImageFilename + ".png", ImageFormat.Png); }
                                                catch (Exception e) { MessageBox.Show("Can't save image " + MainImageFilename + ".png" + Environment.NewLine + "Error: " + e.Message); }

                                                css += "		background-image: url(\"/Pages/VLivery/Liveries/legacy/" + acSlug + "/" + MainImageFilename + ".png\");" + Environment.NewLine + "		background-position: 0px 0px;" + Environment.NewLine + "		background-repeat: no-repeat;" + Environment.NewLine; ;
                                            }
                                            else
                                            {
                                                string msg = "Background image " + panelDir + "\\" + MainImageSource + " not found";
                                                errors += msg + Environment.NewLine + Environment.NewLine;
                                                writeLog("ERROR: " + msg);
                                            }
                                        }
                                    }
                                }

                                var ParentSize = gaugeXml.Element("Size");
                                if (ParentSize != null)
                                    GaugeSize = getXYvalue(ParentSize, false);
                                /*else if (mainImage != null)
                                    GaugeSize = getXYvalue(mainImage, false);*/
                                
                                if (GaugeSize[0] == "0" && GaugeSize[1] == "0")
                                    writeLog("ERROR: " + "Gauge with zero size!");

                                if (atts[6] == 1 && GaugeSize[0] != "0" && GaugeSize[1] != "0" && imgSize[0] != "0" && imgSize[1] != "0") // SCALE GAUGE IF NECESSARY
                                {
                                    css += "		width: " + imgSize[0] + "px;" + Environment.NewLine + "		height: " + imgSize[1] + "px;" + Environment.NewLine;
                                    css += Environment.NewLine + "		transform: scale(calc(" + scaleUp + " * " + GaugeSize[0] + " / " + imgSize[0] + "), calc(" + scaleUp + " * " + GaugeSize[1] + " / " + imgSize[1] + "));" + Environment.NewLine + "		transform-origin: 0 0;" + Environment.NewLine;
                                } else
                                {
                                    css += "		width: " + GaugeSize[0] + "px;" + Environment.NewLine + "		height: " + GaugeSize[1] + "px;" + Environment.NewLine;
                                    if (scaleUp != "1")
                                        css += Environment.NewLine + "		transform: scale(" + scaleUp + ");" + Environment.NewLine + "		transform-origin: 0 0;" + Environment.NewLine;
                                }

                                // SCALE UP/DOWN IF GAUGE SIZE NOT MATCH PANEL SIZE
                                if (atts[3] < 1 && (size2[0] != 0 && size2[0].ToString() != gaugeWidth || size2[1] != 0 && size2[1].ToString() != gaugeHeight))
                                {
                                    writeLog("Scaling up panel from " + gaugeWidth + " " + gaugeHeight + " to " + size2[0] + " " + size2[1]);
                                    css += "		transform: scale(calc(" + scaleUp + " * " + size2[0].ToString() + " / " + gaugeWidth + "), calc(" + scaleUp + " * " + size2[1].ToString() + " / " + gaugeHeight + ")); transform-origin: 0 0; " + Environment.NewLine;
                                }

                                css += "}" + Environment.NewLine;

                                // SET UP GAUGE ELEMENTS
                                var gaugeElements = gaugeXml.Elements("Element");
                                
                                if (gaugeElements.Count() < 0)
                                {
                                    string msg = "No elements in " + gaugeGroup + "\\" + gaugeName + ".xml found!";
                                    errors += msg + Environment.NewLine + Environment.NewLine;
                                    writeLog("ERROR: " + msg);
                                }
                                else
                                {
                                    index = -1;
                                    foreach (XElement gaugeElement in gaugeElements)
                                    {
                                        processGaugeElement(gaugeElement, FsxVarHelper, atts, 0 );
                                    }
                                }

                                html += "		</div>" + Environment.NewLine;
                            }
                            catch (Exception ex)
                            {
                                errors += "Failed to process " + gaugeGroup + "\\" + gaugeName + ".xml" + Environment.NewLine + Environment.NewLine;
                                writeLog(ex.ToString());
                            }

                            try { if (panelDir == Path.GetDirectoryName(mainFile) + "\\" + gaugeGroup)
                                    Directory.Move(panelDir, Path.GetDirectoryName(mainFile) + "\\." + gaugeGroup); }
                            catch { }
                        }

                        if (html != "")
                        {
                            html = htmlTpl.Replace("[INSTRUMENTS]", html);
                            html = html.Replace("[MATERIALNAME]", materialName);
                            html = html.Replace("[ACSLUG]", acSlug);

                            css = cssTpl.Replace("[INSTRUMENTS]", css);
                            css = css.Replace("[MATERIALNAME]", materialName);
                            css = css.Replace("[IMAGE]", !String.IsNullOrEmpty(Path.GetFileNameWithoutExtension(file)) ?
                                "/Pages/VLivery/Liveries/legacy/" + acSlug + "/" + Path.GetFileNameWithoutExtension(file) + ".png" : "");
                            css = css.Replace("[BACKGROUND-COLOR]", !String.IsNullOrEmpty(Path.GetFileNameWithoutExtension(file)) ?
                                "#111" : "transparent");

                            js = jsTpl.Replace("[INSTRUMENTS]", js);
                            js = js.Replace("[MATERIALNAME]", materialName);

                            /*
		// BLUR FIX START
		var skipFrames = 1;

		this.querySelector("#Mainframe").style.opacity = "0.99";
		this.curTime += 1;

		if (this.curTime == skipFrames + 1) {
			this.curTime = 0;
			this.querySelector("#Mainframe").style.opacity = "1";
		} else {
			return;
		} 
		// BLUR FIX END
                            */

                            // SAVE FILES
                            try
                            {
                                File.WriteAllText(InstrumentFolder + materialName + ".html", html);
                                File.WriteAllText(InstrumentFolder + materialName + ".css", css);
                                File.WriteAllText(InstrumentFolder + materialName + ".js", js);
                            }
                            catch (Exception e) { MessageBox.Show("Can't save HTML files" + Environment.NewLine + "Error: " + e.Message); }

                            JSONHelper.scanTargetFolder(baseFolder);

                            string[] data = new string[] { "", "CORE", "MSFS Legacy Importer Instruments", "MSFS Legacy Importer", "Alex Marko", "1.0.0", "1.0.0", "" };
                            JSONHelper.createInstrumentManifest(baseFolder, data);

                            // INSERT NEW PANEL.CFG DATA
                            newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "size_mm", size_mm, ""));
                            newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "pixel_size", pixel_size, ""));
                            newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "texture", texture, ""));
                            newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "location", "interior", ""));
                            newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "painting00", "legacy/" + acSlug + "/" + materialName + ".html?font_color=white,0,0," + (size[0] - 1) + "," + (size[1] - 1), ""));
                            newPanelFile.Sections.Add(newPanelSection);
                        }

                        if (atts[2] != 1 && errors != "")
                            MessageBox.Show(errors + "You can try to unpack FSX gauges resources first or report it to program author (links in About tab)", materialName + " conversion errors");
                    }
                }
            }

            saveLog();

            CfgHelper.saveCfgFile(aircraftDirectory, newPanelFile);
        }

        private void processGaugeElement(XElement gaugeElement, fsxVarHelper FsxVarHelper, float[] atts, int depth)
        {
            index++;

            XElement Image = gaugeElement.Element("Image");
            XElement FloatPosition = gaugeElement.Element("FloatPosition");
            if (FloatPosition == null)
                FloatPosition = gaugeElement.Element("Position");
            XElement Rotation = gaugeElement.Element("Rotation");
            if (Rotation == null)
                Rotation = gaugeElement.Element("Rotate");
            XElement Shift = gaugeElement.Element("Shift");
            XElement GaugeText = gaugeElement.Element("GaugeText");
            XElement MaskImage = gaugeElement.Element("MaskImage");
            XElement Axis = null;
            XElement Transparent = null;

            string visibilityCond = "";
            XElement Visibility = gaugeElement.Element("Visibility");
            if (Visibility == null)
                Visibility = gaugeElement.Element("Visible");
            if (Visibility != null)
                visibilityCond = FsxVarHelper.fsx2msfsSimVar(Visibility.Value, this, false);

            string slug = gaugeElement.Attribute("id") != null ? gaugeSanitizedName + "_" + gaugeElement.Attribute("id").Value : "";

            if (Image != null && Image.Attribute("Name") != null)
                slug = gaugeSanitizedName + "_" + Path.GetFileNameWithoutExtension(Image.Attribute("Name").Value).Trim();
            else if (GaugeText != null && GaugeText.Attribute("id") != null)
                slug = gaugeSanitizedName + "_" + GaugeText.Attribute("id").Value.Trim();

            if (String.IsNullOrEmpty(slug))
                slug = gaugeSanitizedName + "_unknownGauge";

            string sanitizedSlug = sanitizeString(slug) + "_" + index;
            if (!Char.IsLetter(sanitizedSlug, 0)) // FIX FIRST DIGIT
                sanitizedSlug = "g" + sanitizedSlug;

            string[] FloatPositionValues = getXYvalue(FloatPosition);

            // RENDER MASK FIRST
            if (MaskImage != null && !String.IsNullOrEmpty(MaskImage.Attribute("Name").Value))
            {
                string maskSlug = slug + "_" + Path.GetFileNameWithoutExtension(MaskImage.Attribute("Name").Value).Trim();
                string MainImageSource = probablyFixBmpName(MaskImage.Attribute("Name").Value);
                string sourceFile1 = panelDir + "\\" + MainImageSource;
                if (!File.Exists(sourceFile1)) // TRY 1024 SUBFOLDER
                    sourceFile1 = panelDir + "\\1024\\" + MainImageSource;
                string targetFile1 = InstrumentFolder + maskSlug + ".png";

                if (File.Exists(sourceFile1) && !imagesExceptions.Contains(Path.GetFileNameWithoutExtension(targetFile1)))
                {
                    Bitmap bmp = new Bitmap(sourceFile1);
                    bmp = setBitmapGamma(bmp, atts[0], atts[5] == 1 ||
                        MaskImage.Element("Transparent") != null && MaskImage.Element("Transparent").Value == "True" ||
                            MaskImage.Attribute("Alpha") != null && MaskImage.Attribute("Alpha").Value == "Yes" ||
                            imagesForceTransparancy.Contains(Path.GetFileNameWithoutExtension(sourceFile1)));
                    string[] maskSize = new string[] { bmp.Width.ToString(), bmp.Height.ToString() };
                    if (MaskImage.Attribute("ImageSizes") != null)
                        maskSize = getXYvalue(MaskImage, false);

                    try { bmp.Save(targetFile1, ImageFormat.Png); }
                    catch (Exception e) { MessageBox.Show("Can't save image " + maskSlug + ".png" + Environment.NewLine + "Error: " + e.Message); }

                    html += "			<div id=\"" + sanitizedSlug + "_mask\">" + Environment.NewLine;
                    css += materialName + "-element #Mainframe #" + sanitizedSlug + "_mask {" + Environment.NewLine + "		background: transparent;" + Environment.NewLine + "		background-image: url(\"/Pages/VLivery/Liveries/legacy/" + acSlug + "/" + maskSlug + ".png\");" + Environment.NewLine + "		background-position: 0px 0px;" + Environment.NewLine + "		background-repeat: no-repeat;" + Environment.NewLine + "		position: absolute;" + Environment.NewLine + "		overflow: hidden;" + Environment.NewLine;
                    css += Environment.NewLine + "		width: " + maskSize[0] + "px;" + Environment.NewLine + "		height: " + maskSize[1] + "px;" + Environment.NewLine;
                    //if (FloatPosition != null)
                        css += "		left: " + FloatPositionValues[0] + "px;" + Environment.NewLine + "		top: " + FloatPositionValues[1] + "px;" + Environment.NewLine + "}" + Environment.NewLine;
                    //else
                        //css += "		left: 50%;" + Environment.NewLine + "		top: 50%" + Environment.NewLine + "}" + Environment.NewLine;

                    depth++;
                }
                else
                {
                    string msg = "Mask image " + panelDir + "\\" + MainImageSource + " not found";
                    //errors += msg + Environment.NewLine + Environment.NewLine;
                    writeLog("ERROR: " + msg);
                    MaskImage = null;
                }
            }

            // READ MACRO
            js += Environment.NewLine + "		/* " + sanitizedSlug.ToUpper() + " */" + Environment.NewLine;
            foreach (var macro in gaugeElement.Elements("Macro"))
            {
                if (macro.Attribute("Name") != null && !string.IsNullOrEmpty(macro.Value))
                    js += "		var " + macro.Attribute("Name").Value + " = "+ FsxVarHelper.fsx2msfsSimVar(macro.Value, this, false) +";" + Environment.NewLine;
                else if (macro.Attribute("Name") != null)
                    js += "		var " + macro.Attribute("Name").Value + " = \"\";" + Environment.NewLine;
            }


            // START ELEMENT CONTAINER
            for (int i = 0; i < depth; i++) { html += "	"; }
            html += "			<div id=\"" + sanitizedSlug + "\">" + Environment.NewLine;


            string[] imgSize = null;
            string[] AxisPositionValues = new string[] { "0", "0" };

            // IMAGE
            if (Image != null)
            {
                if (Image.Attribute("ImageSizes") != null)
                    imgSize = getXYvalue(Image, true);

                Axis = Image.Elements("Axis").FirstOrDefault();
                Transparent = Image.Element("Transparent");
                AxisPositionValues = getXYvalue(Axis);

                // GENERATE IMAGE
                if (Image.Attribute("Name") != null)
                {
                    string ImageSource = probablyFixBmpName(Image.Attribute("Name").Value);
                    string sourceFile = panelDir + "\\" + ImageSource;
                    if (!File.Exists(sourceFile)) // TRY 1024 SUBFOLDER
                        sourceFile = panelDir + "\\1024\\" + ImageSource;
                    string targetFile = InstrumentFolder + slug + ".png";

                    if (!imagesExceptions.Contains(Path.GetFileNameWithoutExtension(targetFile)))
                    {
                        if (File.Exists(sourceFile))
                        {
                            Bitmap bmp = new Bitmap(sourceFile);
                            bmp = setBitmapGamma(bmp, atts[0],
                                (Transparent == null && MaskImage == null) ||
                                Transparent != null && Transparent.Value != "False" ||
                                MaskImage != null && (MaskImage.Attribute("Alpha") == null || MaskImage.Attribute("Alpha").Value != "No") ||
                                imagesForceTransparancy.Contains(Path.GetFileNameWithoutExtension(sourceFile)));

                            if (imgSize == null)
                                imgSize = new string[] { bmp.Width.ToString(), bmp.Height.ToString() };

                            try { bmp.Save(targetFile, ImageFormat.Png); }
                            catch (Exception e) { MessageBox.Show("Can't save image " + slug + ".png" + Environment.NewLine + "Error: " + e.Message); }
                        }
                        else
                        {
                            string msg = "Gauge image " + panelDir + "\\" + ImageSource + " not found";
                            //errors += msg + Environment.NewLine + Environment.NewLine;
                            writeLog("ERROR: " + msg);

                            Image = null;
                        }

                    }
                }
            }

            // GENERATE CODE
            css += materialName + "-element #Mainframe #" + sanitizedSlug + " {" + Environment.NewLine + "		background: transparent;" + Environment.NewLine;
            if (Image != null)
                css += "		background-image: url(\"/Pages/VLivery/Liveries/legacy/" + acSlug + "/" + slug + ".png\");" + Environment.NewLine + "		background-position: 0px 0px;" + Environment.NewLine + "		background-repeat: no-repeat;" + Environment.NewLine;
            css += "		position: absolute;" + Environment.NewLine + "		overflow: hidden;" + Environment.NewLine;
            if (imgSize != null)
                css += Environment.NewLine + "		width: " + imgSize[0] + "px;" + Environment.NewLine + "		height: " + imgSize[1] + "px;" + Environment.NewLine;
            else
                css += Environment.NewLine + "		width: 100%;" + Environment.NewLine + "		height: 100%;" + Environment.NewLine;

            if (getPosException(slug) != null)
                css += "		left: " + getPosException(slug)[0] + "px;" + Environment.NewLine + "		top: " + getPosException(slug)[1] + "px;" + Environment.NewLine;
            else if (MaskImage != null)
                css += "		left: - " + AxisPositionValues[0] + "px;" + Environment.NewLine + "		top: - " + AxisPositionValues[1] + "px;" + Environment.NewLine;
            else if (FloatPosition == null && AxisPositionValues[0] != "0" && AxisPositionValues[1] != "0")
                css += "		left: calc(50% - " + AxisPositionValues[0] + "px);" + Environment.NewLine + "		top: calc(50% - " + AxisPositionValues[1] + "px);" + Environment.NewLine;
            else
                css += "		left: calc(" + FloatPositionValues[0] + "px - " + AxisPositionValues[0] + "px);" + Environment.NewLine + "		top: calc(" + FloatPositionValues[1] + "px - " + AxisPositionValues[1] + "px);" + Environment.NewLine;
            css += "		transform-origin: " + AxisPositionValues[0] + "px " + AxisPositionValues[1] + "px;" + Environment.NewLine;
            js += "		var " + sanitizedSlug + " = this.querySelector(\"#" + sanitizedSlug + "\");" + Environment.NewLine;
            js += "		if (typeof " + sanitizedSlug + " !== \"undefined\") {" + Environment.NewLine;
            js += "		  var transform = '';" + Environment.NewLine + Environment.NewLine;

            if (!String.IsNullOrEmpty(visibilityCond)) { js += "		  " + sanitizedSlug + ".style.display = " + visibilityCond + " ? \"block\" : \"none\";" + Environment.NewLine + Environment.NewLine; }

            // APPLY ANIM
            XElement Expression = null;

            // PERFORM ROTATION
            if (Rotation != null)
            {
                Expression = Rotation.Elements("Expression").FirstOrDefault();
                if (Expression == null)
                    Expression = Rotation.Elements("Value").FirstOrDefault();

                if (Expression != null)
                {
                    js += "		  {" + Environment.NewLine + Environment.NewLine;

                    expressionRoutin(Expression, FsxVarHelper);

                    XElement PointsTo = Rotation.Elements("PointsTo").FirstOrDefault();
                    XElement DegreesPointsTo = Rotation.Elements("DegreesPointsTo").FirstOrDefault();
                    if (PointsTo != null)
                        switch (PointsTo.Value)
                        {
                            case "SOUTH":
                                js += "			var PointsTo = 180;" + Environment.NewLine;
                                break;
                            case "WEST":
                                js += "			var PointsTo = 90;" + Environment.NewLine;
                                break;
                            case "EAST":
                                js += "			var PointsTo = (-90);" + Environment.NewLine;
                                break;
                            case "NORTH":
                                js += "			var PointsTo = 0;" + Environment.NewLine;
                                break;
                            default:
                                js += "			var PointsTo = 0;" + Environment.NewLine;
                                break;
                        }
                    else if (DegreesPointsTo != null && Double.TryParse(DegreesPointsTo.Value, out double num))
                        js += "			var PointsTo = " + (num - 90.0) + "; " + Environment.NewLine;
                    else
                        js += "			var PointsTo = 0;" + Environment.NewLine;

                    // NONLINEAR
                    if (Rotation.Elements("NonlinearityTable").FirstOrDefault() != null || Rotation.Elements("Nonlinearity").FirstOrDefault() != null)
                    {
                        js += getNonlinearityTable(Rotation, FloatPositionValues[0], FloatPositionValues[1], true);

                        js += "			if (NonlinearityTable.length > 0) {" + Environment.NewLine;
                        js += "			    Minimum = NonlinearityTable[0][0];" + Environment.NewLine;
                        js += "			    ExpressionResult = Math.max(ExpressionResult, Minimum);" + Environment.NewLine;
                        js += "			    Maximum = NonlinearityTable[NonlinearityTable.length-1][0];" + Environment.NewLine;
                        js += "			    ExpressionResult = Math.min(ExpressionResult, Maximum);" + Environment.NewLine;

                        js += "				var prevAngle = 0;" + Environment.NewLine;

                        js += "				var result = 0;" + Environment.NewLine;
                        js += "				var prevVal = Minimum;" + Environment.NewLine;

                        js += "				for (var i = 0; i < NonlinearityTable.length; i++) {" + Environment.NewLine;
                        js += "					var NonlinearityEntry = NonlinearityTable[i][0];" + Environment.NewLine;
                        js += "					var NonlinearityAngle = NonlinearityTable[i][1];" + Environment.NewLine;
                        js += "					if (NonlinearityAngle < 0) { NonlinearityAngle += 360 };" + Environment.NewLine;
                        js += "					if (ExpressionResult == NonlinearityEntry || prevAngle == NonlinearityAngle && ExpressionResult > prevVal && ExpressionResult < NonlinearityEntry) {" + Environment.NewLine;
                        js += "						result = NonlinearityAngle;" + Environment.NewLine;
                        js += "						break;" + Environment.NewLine;
                        js += "					}" + Environment.NewLine;
                        js += "					else if (ExpressionResult > prevVal && ExpressionResult < NonlinearityEntry ) {" + Environment.NewLine;
                        js += "						var coef = 1 - (NonlinearityEntry - ExpressionResult) / (NonlinearityEntry - prevVal);" + Environment.NewLine + Environment.NewLine;
                        js += "						result = prevAngle + coef * (NonlinearityAngle - prevAngle);" + Environment.NewLine;
                        js += "						break;" + Environment.NewLine;
                        js += "					}" + Environment.NewLine;
                        js += "					prevVal = NonlinearityEntry;" + Environment.NewLine;
                        js += "					prevAngle = NonlinearityAngle;" + Environment.NewLine;
                        js += "				}" + Environment.NewLine + Environment.NewLine;
                        js += "				if (Minimum >= 0)" + Environment.NewLine;
                        js += "					while (result < 0)" + Environment.NewLine;
                        js += "						result += 360;" + Environment.NewLine + Environment.NewLine;

                        js += "				transform += 'rotate(' + (result + PointsTo) + 'deg) ';" + Environment.NewLine;
                        js += "			}" + Environment.NewLine + Environment.NewLine;
                    }
                    // LINEAR
                    else
                    {
                        js += "			transform += 'rotate(' + (ExpressionResult * 180 / Math.PI + PointsTo) + 'deg) ';" + Environment.NewLine;
                    }
                    js += "		  }" + Environment.NewLine;
                }
            }
                            
            // PERFORM SHIFT
            if (Shift != null)
            {
                Expression = Shift.Elements("Expression").FirstOrDefault();
                if (Expression == null)
                    Expression = Shift.Elements("Value").FirstOrDefault();

                if (Expression != null)
                {
                    js += "		  {" + Environment.NewLine;

                    expressionRoutin(Expression, FsxVarHelper);

                    // NONLINEAR
                    if (Shift.Elements("NonlinearityTable").FirstOrDefault() != null || Shift.Elements("Nonlinearity").FirstOrDefault() != null)
                    {
                        js += getNonlinearityTable(Shift, "0", "0", false);

                        js += "			if (NonlinearityTable.length > 0) {" + Environment.NewLine;
                        js += "			    Minimum = NonlinearityTable[0][0];" + Environment.NewLine;
                        js += "			    ExpressionResult = Math.max(ExpressionResult, Minimum);" + Environment.NewLine;
                        js += "			    Maximum = NonlinearityTable[NonlinearityTable.length-1][0];" + Environment.NewLine;
                        js += "			    ExpressionResult = Math.min(ExpressionResult, Maximum);" + Environment.NewLine;

                        js += "				var prevP2 = { x: 0, y: 0 };" + Environment.NewLine;
                        js += "				var result = { x: 0, y: 0 };" + Environment.NewLine;

                        js += "				var prevVal = Minimum;" + Environment.NewLine;

                        js += "				for (var i = 0; i < NonlinearityTable.length; i++) {" + Environment.NewLine;
                        js += "					var NonlinearityEntry = NonlinearityTable[i][0];" + Environment.NewLine;
                        js += "					var p2 = { x: NonlinearityTable[i][1], y: NonlinearityTable[i][2] };" + Environment.NewLine;
                        js += "					if (ExpressionResult == NonlinearityEntry) {" + Environment.NewLine;
                        js += "						result = p2;" + Environment.NewLine;
                        js += "						break;" + Environment.NewLine;
                        js += "					}" + Environment.NewLine;
                        js += "					else if (ExpressionResult > prevVal && ExpressionResult < NonlinearityEntry ) {" + Environment.NewLine;
                        js += "						var coef = 1 - (NonlinearityEntry - ExpressionResult) / (NonlinearityEntry - prevVal);" + Environment.NewLine + Environment.NewLine;
                        js += "						result = { y: prevP2.y + coef * (p2.y - prevP2.y), x: prevP2.x + coef * (p2.x - prevP2.x) };" + Environment.NewLine;
                        js += "						break;" + Environment.NewLine;
                        js += "					}" + Environment.NewLine;
                        js += "					prevVal = NonlinearityEntry;" + Environment.NewLine;
                        js += "					prevP2 = p2;" + Environment.NewLine;
                        js += "				}" + Environment.NewLine + Environment.NewLine;

                        js += "				" + sanitizedSlug + ".style.left = result.x + 'px';" + Environment.NewLine;
                        js += "				" + sanitizedSlug + ".style.top = result.y +'px';" + Environment.NewLine;
                        js += "			}" + Environment.NewLine + Environment.NewLine;
                    }
                    // LINEAR
                    else
                    {
                        // GET SCALE 
                        double[] scale = new double[] { 1.0, 0.0 };
                        XElement Scale = Shift.Elements("Scale").FirstOrDefault();
                        string[] ScaleValues = getXYvalue(Scale);
                        if (Scale != null && Double.TryParse(ScaleValues[0], out double scaleX) && Double.TryParse(ScaleValues[1], out double scaleY))
                            scale = new double[] { scaleX, scaleY };

                        js += "			transform += 'translate(' + (ExpressionResult * " + scale[0] + ") + 'px, ' + (ExpressionResult * " + scale[1] + ") + 'px) ';" + Environment.NewLine;
                    }

                    js += "		  }" + Environment.NewLine;
                }
            }

            // TEXT
            if (GaugeText != null)
            {
                XElement id = GaugeText.Element("id");
                XElement Alpha = GaugeText.Element("Alpha");
                XElement TextAxis = GaugeText.Element("Axis");
                XElement BackgroundColor = GaugeText.Element("BackgroundColor");
                XElement BackgroundColorScript = GaugeText.Element("BackgroundColorScript");
                XElement BlinkCode = GaugeText.Element("BlinkCode");
                XElement Bold = GaugeText.Element("Bold");
                XElement Bright = GaugeText.Element("Bright");
                XElement Caption = GaugeText.Element("Caption");
                XElement Charset = GaugeText.Element("Charset");
                XElement DegreesPointsTo = GaugeText.Element("DegreesPointsTo");
                XElement Fixed = GaugeText.Element("Fixed");
                XElement FontFace = GaugeText.Element("FontFace");
                XElement FontFaceLocalize = GaugeText.Element("FontFaceLocalize");
                XElement FontColor = GaugeText.Element("FontColor");
                XElement FontColorScript = GaugeText.Element("FontColorScript");
                XElement FontHeight = GaugeText.Element("FontHeight");
                XElement FontHeightScript = GaugeText.Element("FontHeightScript");
                XElement FontWeight = GaugeText.Element("FontWeight");
                XElement GaugeString = GaugeText.Element("GaugeString");
                XElement HilightColor = GaugeText.Element("HilightColor");
                XElement HorizontalAlign = GaugeText.Element("HorizontalAlign");
                XElement Italic = GaugeText.Element("Italic");
                XElement Length = GaugeText.Element("Length");
                XElement LineSpacing = GaugeText.Element("LineSpacing");
                XElement Luminous = GaugeText.Element("Luminous");
                XElement Multiline = GaugeText.Element("Multiline");
                XElement PointsTo = GaugeText.Element("PointsTo");
                XElement ScrollY = GaugeText.Element("ScrollY");
                XElement Size = GaugeText.Element("Size");
                XElement HeightScript = GaugeText.Element("HeightScript");
                XElement WidthScript = GaugeText.Element("WidthScript");
                XElement StrikeThrough = GaugeText.Element("StrikeThrough");
                XElement Tabs = GaugeText.Element("Tabs");
                XElement TextTransparent = GaugeText.Element("Transparent");
                XElement Underline = GaugeText.Element("Underline");
                XElement VerticalAlign = GaugeText.Element("VerticalAlign");
                XElement WidthFit = GaugeText.Element("WidthFit");

                string value = FsxVarHelper.fsx2msfsGaugeString(GaugeString.Value.Trim(), this);

                string[] GaugeSize = getXYvalue(Size, false);

                //if (id != null) { css += "		rule: " + id.Value + ";" + Environment.NewLine; }
                //if (Alpha != null) { css += "		rule: " + Alpha.Value + ";" + Environment.NewLine; }
                //if (Axis != null) { css += "		transform-origin: " + getXYvalue(Axis)[0] + "px " + getXYvalue(Axis)[1] + "px;" + Environment.NewLine; }
                if (BackgroundColor != null) { css += "		background-color: " + BackgroundColor.Value.Replace("0x", "#") + ";" + Environment.NewLine; }
                if (Bold != null && Bold.Value == "True") { css += "		font-weight: bold;" + Environment.NewLine; }
                //if (Bright != null) { css += "		rule: " + Bright.Value + ";" + Environment.NewLine; }
                //if (Caption != null) { css += "		rule: " + Caption.Value + ";" + Environment.NewLine; }
                //if (Charset != null) { css += "		rule: " + Charset.Value + ";" + Environment.NewLine; }
                //if (DegreesPointsTo != null) { css += "		rule: " + DegreesPointsTo.Value + ";" + Environment.NewLine; }
                if (Fixed != null && Fixed.Value == "True" || Multiline == null || Multiline.Value == "False") { css += "		white-space: nowrap; overflow: hidden;" + Environment.NewLine; }
                if (FontFace != null) { css += "		font-family: \"" + FontFace.Value + "\";" + Environment.NewLine; }
                //if (FontFaceLocalize != null) { css += "		rule: " + FontFaceLocalize.Value + ";" + Environment.NewLine; }
                if (FontColor != null) { css += "		color: " + FontColor.Value.Replace("0x", "#") + ";" + Environment.NewLine; }
                if (FontHeight != null) { css += "		font-size: " + FontHeight.Value + "px;" + Environment.NewLine; }
                if (FontHeightScript != null) { css += "		font-size: " + FontHeightScript.Value + "px;" + Environment.NewLine; }
                if (FontWeight != null) { css += "		font-weight: " + FontWeight.Value + ";" + Environment.NewLine; }
                //if (HilightColor != null) { css += "		rule: " + HilightColor.Value + ";" + Environment.NewLine; }
                if (HorizontalAlign != null) { css += "		text-align: " + HorizontalAlign.Value + ";" + Environment.NewLine; }
                if (Italic != null && Italic.Value == "True") { css += "		font-style: italic;" + Environment.NewLine; }
                if (Length != null) { css += "		overflow: hidden; max-width: " + Length.Value + "ch;" + Environment.NewLine; }
                if (LineSpacing != null) { css += "		line-height: " + LineSpacing.Value + "px;" + Environment.NewLine; }
                //if (Luminous != null) { css += "		rule: " + Luminous.Value + ";" + Environment.NewLine; }
                //if (Multiline != null) { css += "		rule: " + Multiline.Value + ";" + Environment.NewLine; }
                //if (PointsTo != null) { css += "		rule: " + PointsTo.Value + ";" + Environment.NewLine; }
                //if (ScrollY != null) { css += "		rule: " + ScrollY.Value + ";" + Environment.NewLine; }
                if (GaugeSize[0] != "0" && GaugeSize[1] != "0")
                    css += "		width: " + GaugeSize[0] + "px;" + Environment.NewLine + "		height: " + GaugeSize[1] + "px;" + Environment.NewLine;
                if (StrikeThrough != null && StrikeThrough.Value == "True") { css += "		text-decoration: line-through;" + Environment.NewLine; }
                //if (Tabs != null) { css += "		rule: " + Tabs.Value + ";" + Environment.NewLine; }
                if (TextTransparent != null && TextTransparent.Value == "True") { css += "		background: transparent;" + Environment.NewLine; }
                if (Underline != null && Underline.Value == "True") { css += "		text-decoration: underline;" + Environment.NewLine; }
                if (VerticalAlign != null) { css += "		vertical-align: " + VerticalAlign.Value + ";" + Environment.NewLine; }
                if (WidthFit != null && WidthFit.Value == "True") { css += "		font-size: 4vw;;" + Environment.NewLine; }

                //* if (BlinkCode != null) { css += "		rule: " + BlinkCode.Value + ";" + Environment.NewLine; }
                if (HeightScript != null) { js += "			" + sanitizedSlug + ".style.height = " + FsxVarHelper.fsx2msfsGaugeString(HeightScript.Value.Trim(), this) + " + \"px\";" + Environment.NewLine; }
                if (WidthScript != null) { js += "			" + sanitizedSlug + ".style.width = " + FsxVarHelper.fsx2msfsGaugeString(WidthScript.Value.Trim(), this) + " + \"px\";" + Environment.NewLine; }
                if (FontColorScript != null) { js += "			" + sanitizedSlug + ".style.color = " + hexToString(FsxVarHelper.fsx2msfsGaugeString(FontColorScript.Value.Trim(), this)) + ";" + Environment.NewLine; }
                if (BackgroundColorScript != null) { js += "			" + sanitizedSlug + ".style.backgroundColor = " + hexToString(FsxVarHelper.fsx2msfsGaugeString(BackgroundColorScript.Value.Trim(), this)) + ";" + Environment.NewLine; }
                if (GaugeString != null && !string.IsNullOrEmpty(value)) { js += "			" + sanitizedSlug + ".innerHTML = " + value + ";" + Environment.NewLine + Environment.NewLine; }


            }

            js += "		  if (transform != '')" + Environment.NewLine;
            js += "		    " + sanitizedSlug + ".style.transform = transform; " + Environment.NewLine + Environment.NewLine;
            js += "		}" + Environment.NewLine;
            css += "	}" + Environment.NewLine;

            // PROCESS CHILDREN
            var gaugeElements = gaugeElement.Elements("Element");
            foreach (XElement childElement in gaugeElements)
            {
                processGaugeElement(childElement, FsxVarHelper, atts, depth + 1);
            }

            // END ELEMENT CONTAINER
            for (int i = 0; i < depth; i++) { html += "	"; }
            html += "			</div>" + Environment.NewLine;

            if (MaskImage != null)
            {
                depth--;
                for (int i = 0; i < depth; i++) { html += "	"; }
                html += "			</div>" + Environment.NewLine;
            }
        }

        private string hexToString(string hex)
        {
            hex = hex.Replace("0x", "#").Trim();
            if (hex[0] == '#' && hex.Length == 7)
                hex = "\"" + hex + "\"";
            //hex = Regex.Replace(hex.Replace("0x", "#"), @"(#[A-Fa-f0-9]{6})", "\"$1\""); 
            return hex;
        }

        private void expressionRoutin(XElement Expression, fsxVarHelper FsxVarHelper)
        {
            if (Expression.Elements("Script").FirstOrDefault() != null)
                js += "			" + FsxVarHelper.fsx2msfsSimVar(Expression.Elements("Script").FirstOrDefault().Value, this) + Environment.NewLine;
            else if (Expression.Value.Length > 0 && Expression.Value[0] != '<' && Expression.Value.Contains(')') && Expression.Value.Contains('('))
                js += "			" + FsxVarHelper.fsx2msfsSimVar(Expression.Value, this) + Environment.NewLine;
            else
                js += "			var ExpressionResult = 0; /* NO SCRIPT NODE FOUND!!! */" + Environment.NewLine;

            if (Expression.Elements("Minimum").FirstOrDefault() != null)
            {
                js += "			var Minimum = " + Expression.Elements("Minimum").FirstOrDefault().Value + ";" + Environment.NewLine;
                js += "			ExpressionResult = Math.max(ExpressionResult, Minimum);" + Environment.NewLine;
            }
            else if (Expression.Attribute("Minimum") != null)
            {
                js += "			var Minimum = " + Expression.Attribute("Minimum").Value + ";" + Environment.NewLine;
                js += "			ExpressionResult = Math.max(ExpressionResult, Minimum);" + Environment.NewLine;
            }
            else
            {
                js += "			var Minimum = 0;" + Environment.NewLine;
            }

            if (Expression.Elements("Maximum").FirstOrDefault() != null)
            {
                js += "			var Maximum = " + Expression.Elements("Maximum").FirstOrDefault().Value + ";" + Environment.NewLine;
                js += "			ExpressionResult = Math.min(ExpressionResult, Maximum);" + Environment.NewLine;
            }
            else if (Expression.Attribute("Maximum") != null)
            {
                js += "			var Maximum = " + Expression.Attribute("Maximum").Value + ";" + Environment.NewLine;
                js += "			ExpressionResult = Math.min(ExpressionResult, Maximum);" + Environment.NewLine;
            }
            else
            {
                js += "			var Maximum = 999999999;" + Environment.NewLine;
            }

        }

        public string getNonlinearityTable(XElement parent, string centerX, string centerY, bool isAngle)
        {
            string js = "";
            prevPoint = new double[2];
            prevAngle = -9999;
            rotationDirection = 0;

            js += "			var NonlinearityTable = [" + Environment.NewLine;

            if (parent.Elements("NonlinearityTable").FirstOrDefault() != null)

                foreach (var NonlinearityEntry in parent.Elements("NonlinearityTable").FirstOrDefault().Elements("NonlinearityEntry"))
                {
                    string NonlinearExpressionResult = NonlinearityEntry.Elements("ExpressionResult").FirstOrDefault().Value;
                    string[] NonlinearFloatPositionValues = getXYvalue(NonlinearityEntry.Elements("FloatPosition").FirstOrDefault());
                    if (isAngle)
                    {
                        if (double.TryParse(NonlinearFloatPositionValues[0], out double xVal) && double.TryParse(NonlinearFloatPositionValues[1], out double yVal) && double.TryParse(centerX, out double xCen) && double.TryParse(centerY, out double yCen))
                        {
                            js += getNonlinearityAngle(NonlinearExpressionResult, xVal, xCen, yVal, yCen);
                        }
                    }
                    else
                        js += "				[" + NonlinearExpressionResult + "," + NonlinearFloatPositionValues[0] + "," + NonlinearFloatPositionValues[1] + "]," + Environment.NewLine;
                }
            else if (parent.Elements("Nonlinearity").FirstOrDefault() != null)
                foreach (var NonlinearityEntry in parent.Elements("Nonlinearity").FirstOrDefault().Elements("Item"))
                {
                    // XY
                    if (NonlinearityEntry.Attribute("Value") != null && NonlinearityEntry.Attribute("X") != null && NonlinearityEntry.Attribute("Y") != null)
                    {
                        string NonlinearExpressionResult = NonlinearityEntry.Attribute("Value").Value;
                        string X = NonlinearityEntry.Attribute("X").Value;
                        string Y = NonlinearityEntry.Attribute("Y").Value;

                        if (isAngle)
                        {
                            if (double.TryParse(X, out double xVal) && double.TryParse(Y, out double yVal) && double.TryParse(centerX, out double xCen) && double.TryParse(centerY, out double yCen))
                                js += getNonlinearityAngle(NonlinearExpressionResult, xVal, xCen, yVal, yCen);
                        } else
                            js += "				[" + NonlinearExpressionResult + "," + NonlinearityEntry.Attribute("X").Value + "," + NonlinearityEntry.Attribute("Y").Value + "]," + Environment.NewLine;
                    }
                    // DEGREES
                    else if (NonlinearityEntry.Attribute("Value") != null && NonlinearityEntry.Attribute("Degrees") != null)
                    {
                        string NonlinearExpressionResult = NonlinearityEntry.Attribute("Value").Value;
                        string Degrees = NonlinearityEntry.Attribute("Degrees").Value;
                        if (isAngle)
                        {
                            js += "				[" + NonlinearExpressionResult + ", " + Degrees + "]," + Environment.NewLine;
                        }
                        else
                        {
                            if (double.TryParse(Degrees, out double angle) && int.TryParse(centerX, out int intX) && int.TryParse(centerY, out int intY))
                            {
                                js += "				[" + NonlinearExpressionResult + "," + (intX + 10 * Math.Sin(angle * Math.PI / 180)) + "," + (intY + 10 * Math.Cos(angle * Math.PI / 180)) + "]," + Environment.NewLine;
                            }
                        }
                    }
                }

            js += "			];" + Environment.NewLine + Environment.NewLine;

            return js;
        }

        private string getNonlinearityAngle(string NonlinearExpressionResult, double xVal, double xCen, double yVal, double yCen) {
            string js = "";

            double currAngle = Math.Atan2(yVal - yCen, xVal - xCen) * 180 / Math.PI + 90;
            currAngle += 360;

            // FIND OUT DIRECTION
            if (prevAngle != -9999 && prevAngle != currAngle && rotationDirection == 0)
            {
                double transformedX = (xVal - xCen) * Math.Cos(prevAngle / 180 * Math.PI) + (yVal - yCen) * Math.Sin(prevAngle / 180 * Math.PI);
                double transformedY = (yVal - yCen) * Math.Cos(prevAngle / 180 * Math.PI) + (xVal - xCen) * Math.Sin(prevAngle / 180 * Math.PI);
                rotationDirection = transformedX >= 0 ? 1 : -1;
            }

            if (rotationDirection != 0)
            {
                while (currAngle > prevAngle && rotationDirection == -1)
                    currAngle -= 360;

                while (currAngle < prevAngle && rotationDirection == 1)
                    currAngle += 360;
            }

            js += "				[" + NonlinearExpressionResult + ", " + currAngle + "]," + Environment.NewLine;

            prevAngle = currAngle;
            prevPoint = new double[] { xVal, yVal };

            return js;
        }

        public string[] getXYvalue(XElement Size, bool returnEmpty = true)
        {
            if (Size != null)
            {
                if (Size.Attribute("X") != null && !String.IsNullOrEmpty(Size.Attribute("X").Value) || Size.Attribute("Y") != null && !String.IsNullOrEmpty(Size.Attribute("Y").Value))
                {
                    return new string[] {
                        Size.Attribute("X") != null && !String.IsNullOrEmpty(Size.Attribute("X").Value) ? Size.Attribute("X").Value.Trim() : "0",
                        Size.Attribute("Y") != null && !String.IsNullOrEmpty(Size.Attribute("Y").Value) ? Size.Attribute("Y").Value.Trim() : "0"
                    };
                }
                else if (Size.Attribute("ImageSizes") != null && !String.IsNullOrEmpty(Size.Attribute("ImageSizes").Value) && Size.Attribute("ImageSizes").Value.Contains(','))
                    return new string[] { Size.Attribute("ImageSizes").Value.Split(',')[0].Trim(), Size.Attribute("ImageSizes").Value.Split(',')[1].Trim() };
                else if (!String.IsNullOrEmpty(Size.Value) && Size.Value.Contains(','))
                    return new string[] { Size.Value.Split(',')[0].Trim(), Size.Value.Split(',')[1].Trim() };
            }

            //if (returnEmpty)
                return new string[] { "0", "0" };
            //else
                //return new string[] { "500", "500" };
        }

        private Bitmap setBitmapGamma(Bitmap oldBmp, float gamma, bool transparent)
        {
            // ARGB CONVERSION
            Bitmap newBmp;
            Bitmap bmp;
            newBmp = new Bitmap(oldBmp);
            bmp = newBmp.Clone(new Rectangle(0, 0, newBmp.Width, newBmp.Height), PixelFormat.Format32bppArgb);

            // TRANSPARENCY CHECK
            if (transparent)
                bmp = MakeTransparent(bmp);

            // SET GAMMA
            if (gamma != 1.0f)
            {
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetGamma(gamma);

                System.Drawing.Point[] points =
                {
                    new System.Drawing.Point(0, 0),
                    new System.Drawing.Point(bmp.Width, 0),
                    new System.Drawing.Point(0, bmp.Height),
                };
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                newBmp = new Bitmap(bmp.Width, bmp.Height);
                using (Graphics graphics = Graphics.FromImage(newBmp))
                {
                    graphics.DrawImage(bmp, points, rect, GraphicsUnit.Pixel, attributes);
                }
            }

            return bmp;
        }

        private string probablyFixBmpName(string name)
        {
            if (Path.GetFileName(name) == Path.GetFileNameWithoutExtension(name))
                return name + ".bmp";

            return name;
        }

        public void writeLog(string lines)
        {
            logcontent += lines + Environment.NewLine;
        }

        public void saveLog()
        {
            Console.WriteLine("Saving log content into " + logfile);

            if (!String.IsNullOrEmpty(logfile) && !String.IsNullOrEmpty(logcontent))
            {
                try { File.WriteAllText(logfile, logcontent); }
                catch (Exception e) { MessageBox.Show("Can't save logfile" + Environment.NewLine + "Error: " + e.Message); }
            }
            else
            {
                Console.WriteLine(logcontent);
            }
        }


        public string sanitizeString(string val)
        {
            return Regex.Replace(val, @"[^0-9A-Za-z ,_\-]", "").Replace(" ", "_").Replace("-", "_").Replace(",", "_");
        }

        private Bitmap MakeTransparent(Bitmap bmp/*, string sourceFile*/)
        {
            //            writeLog("Processing image: " + Path.GetFileNameWithoutExtension(sourceFile) + " (" + bmp.GetPixel(0, 0).ToString() + ")");

            if (bmp.GetPixel(0, 0) == Color.FromArgb(255, 255, 255, 255))
                bmp.MakeTransparent(Color.FromArgb(255, 255, 255, 255));
            else {
                bmp.MakeTransparent(Color.FromArgb(255, 1, 1, 1));
                bmp.MakeTransparent(Color.FromArgb(255, 0, 0, 0));
            }

            return bmp;
        }

        private string[] getPosException (string slug)
        {
            for (int x = 0; x < positionExceptions.GetLength(0); x++)
            {
                if (Equals(positionExceptions[x][0], slug))
                {
                    return new string[] { positionExceptions[x][1], positionExceptions[x][2] };
                }
            }

            return null;
        }
    }
}
