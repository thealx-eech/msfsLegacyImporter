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
        public void insertFsxGauge(string aircraftDirectory, string projectDirectory, string chkContent, Slider GammaSlider, CheckBox ForceBackground, cfgHelper CfgHelper, fsxVarHelper FsxVarHelper, jsonHelper JSONHelper)
        {
            string mainFile = aircraftDirectory + "\\" + (string)chkContent;
            string backupFile = Path.GetDirectoryName(mainFile) + "\\." + Path.GetFileName(mainFile);

            if (!File.Exists(mainFile))
            {
                return;
            }

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
                        MessageBox.Show("Panel " + panelSection.Name + " does not have enough parameters");
                        continue;
                    }
                        
                    string acSlug = Regex.Replace(Path.GetFileName(aircraftDirectory).Replace("\\", "").Trim().ToLower(), @"[^0-9A-Za-z ,\.\-_]", "").Replace(" ", "_");
                    string materialName = texture.Replace("$", "").ToLower();
                    int[] size = pixel_size != null && pixel_size.Contains(',') &&
                        int.TryParse(pixel_size.Split(',')[0].Trim(), out int num1) && int.TryParse(pixel_size.Split(',')[1].Trim(), out int num2) ?
                        new int[] { num1, num2 } : new int[] { 0, 0 };
                    msfsLegacyImporter.cfgHelper.CfgSection newPanelSection = new msfsLegacyImporter.cfgHelper.CfgSection(true, panelSection.Name.Replace("VCOCKPIT", "VPainting"), new List<msfsLegacyImporter.cfgHelper.CfgLine>());

                    // PROCESS GAUGES LIST
                    for (int i = 0; i < 100; i++)
                    {
                        var gauge = panelSection.Lines.Find(x => x.Name == "gauge" + i.ToString("00"));
                        if (gauge != null && gauge.Value.Contains(',') && !gauge.Value.Contains("n_number_plaque"))
                        {
                            string[] gaugeData = gauge.Value.Split(',');
                            if (gaugeData.Length >= 5 && gaugeData[0].Contains('!'))
                            {
                                gaugesList.Add(gaugeData);
                            }
                            else
                            {
                                MessageBox.Show("Unrecognized gauge string: " + gauge.Value);
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
                        string html = "";
                        string css = "";
                        string js = "";

                        string baseFolder = /*projectDirectory.TrimEnd('\\')*/ Path.GetDirectoryName(projectDirectory.TrimEnd('\\')) + "\\legacy-vcockpits-instruments\\";
                        string InstrumentFolder = baseFolder + "html_ui\\Pages\\vlivery\\liveries\\Legacy\\" + acSlug + "\\";

                        if (!Directory.Exists(InstrumentFolder))
                            Directory.CreateDirectory(InstrumentFolder);

                        if (!String.IsNullOrEmpty(file))
                        {
                            if (File.Exists(Path.GetDirectoryName(mainFile) + "\\" + file))
                            {
                                Bitmap bmp = new Bitmap(Path.GetDirectoryName(mainFile) + "\\" + file);
                                bmp = setBitmapGamma(bmp, GammaSlider);
                                bmp.Save(InstrumentFolder + Path.GetFileNameWithoutExtension(file) + ".png", ImageFormat.Png);
                            }
                            else
                                MessageBox.Show("Background image " + Path.GetDirectoryName(mainFile) + "\\" + file + " not found. You can try to copy it from FSX install directory.");
                        }

                        foreach (var gaugeData in gaugesList)
                        {
                            string gaugeGroup = gaugeData[0].Split('!')[0].Trim().ToLower();
                            string gaugeSanitizedGroup = Regex.Replace(gaugeGroup, @"[^0-9A-Za-z ,.-_]", "").Replace(" ", "_");
                            string gaugeName = gaugeData[0].Split('!')[1].Trim();
                            string gaugeSanitizedName = Regex.Replace(gaugeName, @"[^0-9A-Za-z ,]", "").Replace(" ", "_");
                            string xPos = gaugeData[1].Trim();
                            string yPos = gaugeData[2].Trim();
                            string gaugeWidth = gaugeData[3].Trim();
                            string gaugeHeight = gaugeData[4].Trim();

                            // AC CAB
                            string panelDir = Path.GetDirectoryName(mainFile) + "\\." + gaugeGroup;

                            // AC SUBFOLDER
                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml"))
                                panelDir = Path.GetDirectoryName(mainFile) + "\\" + gaugeGroup;

                            // FSX CABS
                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml"))
                                panelDir = Path.GetDirectoryName(projectDirectory.TrimEnd('\\')) + "\\legacy-vcockpits-instruments\\.FSX\\" + gaugeGroup;

                            if (!Directory.Exists(panelDir) || !File.Exists(panelDir + "\\" + gaugeName + ".xml")) {
                                MessageBox.Show("Gauge file " + gaugeName + ".xml" + " not found. You can try to unpack default FSX gauges first.");
                            }
                            else
                            {
                                // SET UP GAUGE
                                try
                                {
                                    Console.WriteLine("Processing " + gaugeGroup + "\\" + gaugeName + ".xml");

                                    XElement gaugeXml = XElement.Load(panelDir + "\\" + gaugeName + ".xml");
                                    var Size = gaugeXml.Descendants("Size").FirstOrDefault();
                                    string[] GaugeSize = getXYvalue(Size);
                                    html += "		<div id=\"" + gaugeName + "\">" + Environment.NewLine;
                                    css += materialName + "-element #Mainframe #" + gaugeName + " {" + Environment.NewLine + "		width: " + GaugeSize[0] + "px;" + Environment.NewLine + "		height: " + GaugeSize[1] + "px;" + Environment.NewLine + "		position: absolute;" + Environment.NewLine + "		overflow: hidden;" + Environment.NewLine + "		left: " + xPos + "px;" + Environment.NewLine + "		top: " + yPos + "px;" + Environment.NewLine;

                                    // SET BG IMAGE IF NECESSARY
                                    if (String.IsNullOrEmpty(Path.GetFileNameWithoutExtension(file)) || ForceBackground != null && ForceBackground.IsChecked == true)
                                    {
                                        XElement mainImage = gaugeXml.Descendants("Image").FirstOrDefault();
                                        if (mainImage != null)
                                        {
                                            string MainImageFilename = gaugeName + "_" + Path.GetFileNameWithoutExtension(mainImage.Attribute("Name").Value).Trim();
                                            if (!String.IsNullOrEmpty(MainImageFilename))
                                            {
                                                if (File.Exists(panelDir + "\\" + mainImage.Attribute("Name").Value))
                                                {
                                                    Bitmap bmp = new Bitmap(panelDir + "\\" + mainImage.Attribute("Name").Value);
                                                    bmp = setBitmapGamma(bmp, GammaSlider);
                                                    bmp.Save(InstrumentFolder + MainImageFilename + ".png", ImageFormat.Png);

                                                    css += "		background-image: url(\"/Pages/VLivery/Liveries/legacy/" + gaugeGroup + "/" + MainImageFilename + ".png\");" + Environment.NewLine; ;
                                                }
                                                else
                                                    MessageBox.Show("Background image " + panelDir + "\\" + mainImage.Attribute("Name").Value + " not found. You can try to copy it from FSX install directory.");
                                            }
                                        }
                                    }
                                    css += "}" + Environment.NewLine;

                                    // SET UP GAUGE ELEMENTS
                                    var gaugeElements = gaugeXml.Descendants("Element");
                                    if (gaugeElements.Count() < 0)
                                    {
                                        MessageBox.Show("No elements in " + gaugeGroup + "\\" + gaugeName + ".xml found!");
                                    }
                                    else
                                    {
                                        int index = -1;
                                        foreach (var gaugeElement in gaugeElements)
                                        {
                                            index++;

                                            XElement Image = gaugeElement.Descendants("Image").FirstOrDefault();
                                            XElement FloatPosition = gaugeElement.Descendants("FloatPosition").FirstOrDefault();
                                            if (FloatPosition == null)
                                                FloatPosition = gaugeElement.Descendants("Position").FirstOrDefault();
                                            XElement Rotation = gaugeElement.Descendants("Rotation").FirstOrDefault();
                                            if (Rotation == null)
                                                Rotation = gaugeElement.Descendants("Rotate").FirstOrDefault();
                                            XElement Shift = gaugeElement.Descendants("Shift").FirstOrDefault();
                                            XElement MaskImage = gaugeElement.Descendants("MaskImage").FirstOrDefault();
                                            int imgWidth = 0;
                                            int imgHeight = 0;

                                            if (FloatPosition == null)
                                            {
                                                Console.WriteLine("Child of " + gaugeGroup + "\\" + gaugeName + ".xml does not have position element");
                                            }
                                            else if (Rotation == null && Shift == null)
                                            {
                                                Console.WriteLine("Child of " + gaugeGroup + "\\" + gaugeName + ".xml does not have rotation or shift element");
                                            }
                                            else if (Image == null || FloatPosition == null)
                                            {
                                                Console.WriteLine("Child of " + gaugeGroup + "\\" + gaugeName + ".xml does not have image or position");
                                            }
                                            else {
                                                string slug = gaugeName + "_" + Path.GetFileNameWithoutExtension(Image.Attribute("Name").Value).Trim();
                                                string sanitizedSlug = Regex.Replace(slug, @"[^0-9A-Za-z ,]", "").Replace(" ", "_") + "_" + index;
                                                XElement Expression = null;
                                                string[] FloatPositionValues = getXYvalue(FloatPosition);

                                                // RENDER MASK FIRST
                                                if (MaskImage != null && !String.IsNullOrEmpty(MaskImage.Attribute("Name").Value))
                                                {
                                                    string maskSlug = slug + "_" + Path.GetFileNameWithoutExtension(MaskImage.Attribute("Name").Value).Trim();
                                                    string sourceFile = panelDir + "\\" + MaskImage.Attribute("Name").Value;
                                                    string targetFile = InstrumentFolder + maskSlug + ".png";

                                                    Bitmap bmp = new Bitmap(sourceFile);
                                                    int maskWidth = bmp.Width;
                                                    int maskHeight = bmp.Height;
                                                    bmp.Save(targetFile, ImageFormat.Png);

                                                    html += "		  <div id=\"" + sanitizedSlug + "_mask\">" + Environment.NewLine;
                                                    css += materialName + "-element #Mainframe #" + sanitizedSlug + "_mask {" + Environment.NewLine + "		background: transparent;" + Environment.NewLine + "		background-image: url(\"/Pages/VLivery/Liveries/legacy/" + gaugeGroup + "/" + maskSlug + ".png\");" + Environment.NewLine + "		background-position: 0px 0px;" + Environment.NewLine + "		background-repeat: no-repeat;" + Environment.NewLine + "		position: absolute;" + Environment.NewLine + "		overflow: hidden;" + Environment.NewLine;
                                                    css += Environment.NewLine + "		width: " + maskWidth + "px;" + Environment.NewLine + "		height: " + maskHeight + "px;" + Environment.NewLine;
                                                    css += "		left: " + FloatPositionValues[0] + "px;" + Environment.NewLine + "		top: " + FloatPositionValues[1] + "px;" + Environment.NewLine + "}" + Environment.NewLine;
                                                }

                                                if (Rotation != null)
                                                {
                                                    Expression = Rotation.Descendants("Expression").FirstOrDefault();
                                                    if (Expression == null)
                                                        Expression = Rotation.Descendants("Value").FirstOrDefault();
                                                }
                                                else if (Shift != null)
                                                {
                                                    Expression = Shift.Descendants("Expression").FirstOrDefault();
                                                    if (Expression == null)
                                                        Expression = Shift.Descendants("Value").FirstOrDefault();
                                                }

                                                // RENDER MOVING ELEMENT
                                                if (Expression != null)
                                                {
                                                    XElement Axis = Image.Descendants("Axis").FirstOrDefault();
                                                    string[] AxisPositionValues = getXYvalue(Axis);
                                                    XElement Transparent = Image.Descendants("Transparent").FirstOrDefault();

                                                    // GENERATE IMAGE
                                                    string sourceFile = panelDir + "\\" + Image.Attribute("Name").Value;
                                                    string targetFile = InstrumentFolder + slug + ".png";

                                                    Bitmap bmp = new Bitmap(sourceFile);
                                                    bmp = setBitmapGamma(bmp, GammaSlider);
                                                    imgWidth = bmp.Width;
                                                    imgHeight = bmp.Height;

                                                    bmp.MakeTransparent(System.Drawing.Color.FromArgb(255, 0, 0, 0));
                                                    bmp.Save(targetFile, ImageFormat.Png);

                                                    // GENERATE CODE
                                                    html += "			<div id=\"" + sanitizedSlug + "\"></div>" + Environment.NewLine;
                                                    css += materialName + "-element #Mainframe #" + sanitizedSlug + " {" + Environment.NewLine + "		background: transparent;" + Environment.NewLine + "		background-image: url(\"/Pages/VLivery/Liveries/legacy/" + gaugeGroup + "/" + slug + ".png\");" + Environment.NewLine + "		background-position: 0px 0px;" + Environment.NewLine + "		background-repeat: no-repeat;" + Environment.NewLine + "		position: absolute;" + Environment.NewLine + "		overflow: hidden;" + Environment.NewLine;
                                                    css += Environment.NewLine + "		width: " + imgWidth + "px;" + Environment.NewLine + "		height: " + imgHeight + "px;" + Environment.NewLine;
                                                    if (MaskImage != null && !String.IsNullOrEmpty(MaskImage.Attribute("Name").Value))
                                                        css += "		left: - " + AxisPositionValues[0] + "px;" + Environment.NewLine + "		top: - " + AxisPositionValues[1] + "px;" + Environment.NewLine;
                                                    else
                                                        css += "		left: calc(" + FloatPositionValues[0] + "px - " + AxisPositionValues[0] + "px);" + Environment.NewLine + "		top: calc(" + FloatPositionValues[1] + "px - " + AxisPositionValues[1] + "px);" + Environment.NewLine;
                                                    css += "		transform-origin: " + AxisPositionValues[0] + "px " + AxisPositionValues[1] + "px;" + Environment.NewLine + "	}" + Environment.NewLine;
                                                    js += Environment.NewLine + "		/* " + slug.ToUpper() + " */" + Environment.NewLine;
                                                    js += "		var " + sanitizedSlug + " = this.querySelector(\"#" + sanitizedSlug + "\");" + Environment.NewLine;
                                                    js += "		if (typeof " + sanitizedSlug + " !== \"undefined\") {" + Environment.NewLine;

                                                    if (Expression.Descendants("Script").FirstOrDefault() != null)
                                                        js += "			" + FsxVarHelper.fsx2msfsSimVar(Expression.Descendants("Script").FirstOrDefault().Value) + Environment.NewLine;
                                                    else if (Expression.Value.Length > 0 && Expression.Value[0] != '<' && Expression.Value.Contains(')') && Expression.Value.Contains('('))
                                                        js += "			" + FsxVarHelper.fsx2msfsSimVar(Expression.Value) + Environment.NewLine;
                                                    else
                                                        js += "			var ExpressionResult = 0; /* NO SCRIPT NODE FOUND!!! */" + Environment.NewLine;

                                                    if (Expression.Descendants("Minimum").FirstOrDefault() != null)
                                                    {
                                                        js += "			var Minimum = " + Expression.Descendants("Minimum").FirstOrDefault().Value + ";" + Environment.NewLine;
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

                                                    if (Expression.Descendants("Maximum").FirstOrDefault() != null)
                                                    {
                                                        js += "			var Maximum = " + Expression.Descendants("Maximum").FirstOrDefault().Value + ";" + Environment.NewLine;
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

                                                // PERFORM ROTATION
                                                if (Rotation != null)
                                                {
                                                    XElement PointsTo = Rotation.Descendants("PointsTo").FirstOrDefault();
                                                    XElement DegreesPointsTo = Rotation.Descendants("DegreesPointsTo").FirstOrDefault();
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
                                                    if (Rotation.Descendants("NonlinearityTable").FirstOrDefault() != null || Rotation.Descendants("Nonlinearity").FirstOrDefault() != null)
                                                    {
                                                        js += getNonlinearityTable(Rotation, FloatPositionValues[0], FloatPositionValues[1]);

                                                        js += "			if (NonlinearityTable.length > 0) {" + Environment.NewLine;
                                                        js += "			    Minimum = NonlinearityTable[0][0];" + Environment.NewLine;
                                                        js += "			    ExpressionResult = Math.max(ExpressionResult, Minimum);" + Environment.NewLine;
                                                        js += "			    Maximum = NonlinearityTable[NonlinearityTable.length-1][0];" + Environment.NewLine;
                                                        js += "			    ExpressionResult = Math.min(ExpressionResult, Maximum);" + Environment.NewLine;

                                                        js += "				var p1 = { x: " + FloatPositionValues[0] + ", y: " + FloatPositionValues[1] + " };" + Environment.NewLine;
                                                        js += "				var prevP2 = { x: 0, y: 0 };" + Environment.NewLine;

                                                        js += "				var result = 0;" + Environment.NewLine;
                                                        js += "				var prevVal = Minimum;" + Environment.NewLine;

                                                        js += "				for (var i = 0; i < NonlinearityTable.length; i++) {" + Environment.NewLine;
                                                        js += "					var NonlinearityEntry = NonlinearityTable[i][0];" + Environment.NewLine;
                                                        js += "					var p2 = { x: NonlinearityTable[i][1], y: NonlinearityTable[i][2] };" + Environment.NewLine;
                                                        js += "					if (ExpressionResult == NonlinearityEntry) {" + Environment.NewLine;
                                                        js += "						result = Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180 / Math.PI + 90;" + Environment.NewLine;
                                                        js += "						break;" + Environment.NewLine;
                                                        js += "					}" + Environment.NewLine;
                                                        js += "					else if (ExpressionResult > prevVal && ExpressionResult < NonlinearityEntry ) {" + Environment.NewLine;
                                                        js += "						var coef = 1 - (NonlinearityEntry - ExpressionResult) / (NonlinearityEntry - prevVal);" + Environment.NewLine + Environment.NewLine;
                                                        js += "						result = Math.atan2(prevP2.y + coef * (p2.y - prevP2.y) - p1.y, prevP2.x + coef * (p2.x - prevP2.x) - p1.x) * 180 / Math.PI + 90;" + Environment.NewLine;
                                                        js += "						break;" + Environment.NewLine;
                                                        js += "					}" + Environment.NewLine;
                                                        js += "					prevVal = NonlinearityEntry;" + Environment.NewLine;
                                                        js += "					prevP2 = p2;" + Environment.NewLine;
                                                        js += "				}" + Environment.NewLine + Environment.NewLine;
                                                        js += "				if (Minimum >= 0)" + Environment.NewLine;
                                                        js += "					while (result < 0)" + Environment.NewLine;
                                                        js += "						result += 360;" + Environment.NewLine + Environment.NewLine;

                                                        js += "				" + sanitizedSlug + ".style.transform = 'rotate(' + (result + PointsTo) + 'deg)';" + Environment.NewLine;
                                                        js += "			}" + Environment.NewLine + Environment.NewLine;
                                                    }
                                                    // LINEAR
                                                    else
                                                    {
                                                        js += "			" + sanitizedSlug + ".style.transform = 'rotate(' + (ExpressionResult * 180 / Math.PI + PointsTo) + 'deg)';" + Environment.NewLine;
                                                    }
                                                }
                                                // PERFORM SHIFT
                                                else if (Image != null && FloatPosition != null && Shift != null)
                                                {
                                                    // NONLINEAR
                                                    if (Shift.Descendants("NonlinearityTable").FirstOrDefault() != null || Shift.Descendants("Nonlinearity").FirstOrDefault() != null)
                                                    {
                                                        js += getNonlinearityTable(Shift, "0", "0");

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
                                                        XElement Scale = Shift.Descendants("Scale").FirstOrDefault();
                                                        string[] ScaleValues = getXYvalue(Scale);
                                                        if (Scale != null && Double.TryParse(ScaleValues[0], out double scaleX) && Double.TryParse(ScaleValues[1], out double scaleY))
                                                            scale = new double[] { scaleX, scaleY };

                                                        js += "			" + sanitizedSlug + ".style.transform = 'translate(' + (ExpressionResult * " + scale[0] + ") + 'px, ' + (ExpressionResult * " + scale[1] + ") + 'px)';" + Environment.NewLine;
                                                    }
                                                }

                                                if (MaskImage != null && !String.IsNullOrEmpty(MaskImage.Attribute("Name").Value))
                                                    html += "		  </div>" + Environment.NewLine;

                                                if (Expression != null)
                                                    js += "		}" + Environment.NewLine;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Failed to process " + gaugeGroup + "\\" + gaugeName + ".xml! You can report it to program author (links in About tab)");
                                    Console.WriteLine(ex.ToString());
                                }

                                html += "		</div>" + Environment.NewLine;
                            }
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

                            js = jsTpl.Replace("[INSTRUMENTS]", js);
                            js = js.Replace("[MATERIALNAME]", materialName);

                            // SAVE FILES
                            File.WriteAllText(InstrumentFolder + materialName + ".html", html);
                            File.WriteAllText(InstrumentFolder + materialName + ".css", css);
                            File.WriteAllText(InstrumentFolder + materialName + ".js", js);

                            JSONHelper.scanTargetFolder(baseFolder);

                            string[] data = new string[] { "", "CORE", "MSFS Legacy Importer Instruments", "MSFS Legacy Importer", "Alex Marko", "1.0.0", "1.0.0", "" };
                            JSONHelper.createInstrumentManifest(baseFolder, data);
                        }
                    }

                    // INSERT NEW PANEL.CFG DATA
                    newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "size_mm", size_mm, ""));
                    newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "pixel_size", pixel_size, ""));
                    newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "texture", texture, ""));
                    newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "location", "interior", ""));
                    newPanelSection.Lines.Add(new msfsLegacyImporter.cfgHelper.CfgLine(true, "painting00", "legacy/" + acSlug + "/" + materialName + ".html?font_color=white,0,0," + (size[0] - 1) + "," + (size[1] - 1), ""));
                    newPanelFile.Sections.Add(newPanelSection);
                }
            }

            CfgHelper.saveCfgFile(aircraftDirectory, newPanelFile);
        }

        public string getNonlinearityTable(XElement parent, string centerX, string centerY)
        {
            string js = "";

            js += "			var NonlinearityTable = [" + Environment.NewLine;
            if (parent.Descendants("NonlinearityTable").FirstOrDefault() != null)
                foreach (var NonlinearityEntry in parent.Descendants("NonlinearityTable").FirstOrDefault().Descendants("NonlinearityEntry"))
                {
                    string NonlinearExpressionResult = NonlinearityEntry.Descendants("ExpressionResult").FirstOrDefault().Value;
                    string[] NonlinearFloatPositionValues = getXYvalue(NonlinearityEntry.Descendants("FloatPosition").FirstOrDefault());
                    js += "				[" + NonlinearExpressionResult + "," + NonlinearFloatPositionValues[0] + "," + NonlinearFloatPositionValues[1] + "]," + Environment.NewLine;
                }
            else if (parent.Descendants("Nonlinearity").FirstOrDefault() != null)
                foreach (var NonlinearityEntry in parent.Descendants("Nonlinearity").FirstOrDefault().Descendants("Item"))
                {
                    // TODO: ADD Degrees ATTRIBUTE
                    if (NonlinearityEntry.Attribute("Value") != null && NonlinearityEntry.Attribute("X") != null && NonlinearityEntry.Attribute("Y") != null)
                    {
                        string NonlinearExpressionResult = NonlinearityEntry.Attribute("Value").Value;
                        js += "				[" + NonlinearExpressionResult + "," + NonlinearityEntry.Attribute("X").Value + "," + NonlinearityEntry.Attribute("Y").Value + "]," + Environment.NewLine;
                    }
                    else if (NonlinearityEntry.Attribute("Value") != null && NonlinearityEntry.Attribute("Degrees") != null)
                    {
                        string NonlinearExpressionResult = NonlinearityEntry.Attribute("Value").Value;
                        string Degrees = NonlinearityEntry.Attribute("Degrees").Value;
                        if (double.TryParse(Degrees, out double angle) && int.TryParse(centerX, out int intX) && int.TryParse(centerY, out int intY))
                        {
                            js += "				[" + NonlinearExpressionResult + "," + (intX + 10 * Math.Sin(angle*Math.PI/180)) + "," + (intY + 10 * Math.Cos(angle * Math.PI / 180)) + "]," + Environment.NewLine;
                        }
                    }
                }

            js += "			];" + Environment.NewLine + Environment.NewLine;

            return js;
        }
        public string[] getXYvalue(XElement Size)
        {
            if (Size != null)
            {
                if (Size.Attribute("X") != null && !String.IsNullOrEmpty(Size.Attribute("X").Value) && Size.Attribute("Y") != null && !String.IsNullOrEmpty(Size.Attribute("Y").Value))
                    return new string[] { Size.Attribute("X").Value.Trim(), Size.Attribute("Y").Value.Trim() };
                else if (!String.IsNullOrEmpty(Size.Value) && Size.Value.Contains(','))
                    return new string[] { Size.Value.Split(',')[0].Trim(), Size.Value.Split(',')[1].Trim() };
            }

            return new string[] { "0", "0" };
        }

        private Bitmap setBitmapGamma(Bitmap bmp, Slider GammaSlider)
        {
            float gamma = 1.0f;
            if (GammaSlider != null)
                gamma = (float)GammaSlider.Value;
            ImageAttributes attributes = new ImageAttributes();
            attributes.SetGamma(gamma);

            System.Drawing.Point[] points =
            {
                new System.Drawing.Point(0, 0),
                new System.Drawing.Point(bmp.Width, 0),
                new System.Drawing.Point(0, bmp.Height),
            };
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            Bitmap newBmp = new Bitmap(bmp.Width, bmp.Height);
            using (Graphics graphics = Graphics.FromImage(newBmp))
            {
                graphics.DrawImage(bmp, points, rect, GraphicsUnit.Pixel, attributes);
            }

            return newBmp;
        }
    }
}
