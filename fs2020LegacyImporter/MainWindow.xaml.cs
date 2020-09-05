using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Reflection;
using static msfsLegacyImporter.cfgHelper;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace msfsLegacyImporter
{
    public partial class MainWindow : Window
    {
        public string EXE_PATH = "msfsLegacyImporter.exe";
        public string TEMP_FILE = "temp.zip";
        public string updatedirectory = "http://eech.online/msfslegacyimporter/";

        public string updateVersion = "";
        public string updateURL = "";
        public string projectDirectory = "";
        public string aircraftDirectory = "";
        private cfgHelper CfgHelper;
        private jsonHelper JSONHelper;

        string SourceFolder = "";
        string TargetFolder = "";
        public MainWindow()
        {
            try
            {
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + EXE_PATH + ".BAK");
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + TEMP_FILE);
            }
            catch
            {
                //MessageBox.Show("Can't delete temporary files");
            }

            InitializeComponent();

            btnOpenFile = SetButtonAtts(btnOpenFile);
            btnSourceFolder = SetButtonAtts(btnSourceFolder);
            btnTargetFolder = SetButtonAtts(btnTargetFolder);
            btnScan = SetButtonAtts(btnScan);
            btnImportSubmit = SetButtonAtts(btnImportSubmit);

            int k = 0;
            foreach (TabItem item in fsTabControl.Items)
            {
                if (k > 0 && k < fsTabControl.Items.Count - 1)
                    item.Visibility = Visibility.Collapsed;
                k++;
            }

            JSONHelper = new jsonHelper();
            CfgHelper = new cfgHelper();
            // TRY TO LOAD CFGTPL FILES
            if (!CfgHelper.processTemplateCfgs())
                System.Environment.Exit(1);

            _ = CheckUpdateAsync();
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath",
                new string[] { "C:\\\\" });
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                setAircraftDirectory(dialog.FileName);
                btnOpenFilePath.Text = dialog.FileName;
            }
        }

        public void setAircraftDirectory(string directory)
        {
            if (File.Exists(directory + "\\layout.json") &&
                File.Exists(directory + "\\manifest.json") &&
                Directory.Exists(directory + "\\SimObjects"))
            {
                try
                {
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath", Path.GetDirectoryName(directory));
                }
                catch (Exception) { }

                projectDirectory = directory;
                aircraftDirectory = Get_aircraft_directory();

                Header.Text = "Current aircraft: " + new DirectoryInfo(projectDirectory).Name;
                btnOpenFile.Content = "Select another aircraft";

                SummaryUpdate();
            }
            else
            {
                MessageBox.Show("Folder " + directory + " does not contain any aircraft");
            }
        }

        private void SummaryUpdate()
        {
            SummaryAircraft();
            SummaryEngines();
            SummaryCockpit();
            SummarySystems();
            SummaryTextures();
            SummaryAir();

            foreach (TabItem item in fsTabControl.Items)
            {
                switch (item.Name)
                {
                    case "tabAircraft":
                        if (File.Exists(aircraftDirectory + "\\aircraft.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabCockpit":
                        if (File.Exists(aircraftDirectory + "\\cockpit.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabSystems":
                        if (File.Exists(aircraftDirectory + "\\systems.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabEngines":
                        if (File.Exists(aircraftDirectory + "\\engines.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        item.Visibility = Visibility.Visible;
                        break;
                }
            }
        }
        private void BtnAircraftProcess_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(aircraftDirectory + "\\aircraft.cfg"))
            {
                MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("aircraft.cfg not found in aircraft directory", "", MessageBoxButton.OK);
            }
            else
            {
                // PROCESS AIRCRAFT FILE
                if (File.Exists(aircraftDirectory + "\\.aircraft.cfg"))
                {
                    MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Backup of aircraft.cfg already exists", "Are you sure it can be removed?", System.Windows.MessageBoxButton.YesNo);
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        File.Delete(aircraftDirectory + "\\.aircraft.cfg");
                        CfgHelper.splitCfg(aircraftDirectory);
                    }
                } else
                {
                    CfgHelper.splitCfg(aircraftDirectory);
                }

                JSONHelper.scanTargetFolder(projectDirectory);
                SummaryUpdate();
            }
        }

        public void SummaryAircraft()
        {
            int status = 2;

            AircraftProcess.Children.Clear();
            btnAircraftProcess.IsEnabled = false;

            StackPanel myPanel = new StackPanel();

            // DESCRIPTION FIX
            if (File.Exists(aircraftDirectory + "\\aircraft.cfg"))
            {
                int criticalIssues = 4;
                string[] requiredValues = new string[] { "ui_certified_ceiling", "ui_max_range", "ui_autonomy", "cruise_speed" };

                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\aircraft.cfg");
                List<CfgLine> acLines = CfgHelper.readCSV(content);
                foreach (var line in acLines)
                {
                    if (requiredValues.Contains(line.Name))
                    {
                        requiredValues = requiredValues.Where(val => val != line.Name).ToArray();
                        criticalIssues++;
                    }
                }

                if (criticalIssues > 0)
                {
                    foreach (var requiredValue in requiredValues)
                    {
                        CheckBox checkBox = new CheckBox();
                        checkBox.Content = requiredValue + " description parameter missing";
                        checkBox.Foreground = new SolidColorBrush(Colors.DarkOrange);

                        myPanel.Children.Add(checkBox);
                    }


                    Button btn = new Button();
                    btn = SetButtonAtts(btn);
                    btn.Content = "Add missing desciption parameters";
                    btn.Click += AddDescriptionClick;
                    myPanel.Children.Add(btn);
                }
            }

            // AC performance = 

            // AC ui_certified_ceiling
            // AC ui_max_range
            // AC ui_max_range
            // FM cruise_speed

            //AircraftProcess
            if (aircraftDirectory != "" && File.Exists(aircraftDirectory + @"\aircraft.cfg"))
            {

                foreach (var file in new[] { "aircraft.cfg", "cameras.cfg", "cockpit.cfg", "engines.cfg", "flight_model.cfg", "gameplay.cfg", "systems.cfg", ".unknown.cfg" })
                {
                    TextBlock myBlock;
                    if (file == ".unknown.cfg" && File.Exists(aircraftDirectory + "\\" + file))
                    {
                        myBlock = AddTextBlock(file + " is presented", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkOrange);
                    }
                    else if (!File.Exists(aircraftDirectory + "\\" + file))
                    {
                        myBlock = AddTextBlock(file + " is missing", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkRed);
                        //cfgsList += "<Run Foreground=\"Red\">" + file + " is missing" + "</ Run >"  + Environment.NewLine;
                        status = 0;
                    }
                    else
                    {
                        myBlock = AddTextBlock(file + " is set", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
                    }
                    myPanel.Children.Add(myBlock);
                }

                // SHOW PROCESS INSTRUMENTS
                if (status == 0)
                {
                    btnAircraftProcess.IsEnabled = true;
                }

                //AircraftContent.Text = @"List of CFG files:" + Environment.NewLine + Environment.NewLine + cfgsList;
                tabAircraft.Foreground = new SolidColorBrush(status > 1 ? Colors.DarkGreen : Colors.DarkRed);
                //AircraftContent.Foreground = new SolidColorBrush(Colors.Black);
            }
            else
            {
                //AircraftContent.Text = "aircraft.cfg file not exists!";
                tabAircraft.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            AircraftProcess.Children.Add(myPanel);
        }

        public void AddDescriptionClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(aircraftDirectory + "\\aircraft.cfg"))
            {
                int i = 0;
                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\aircraft.cfg");

                // GET LEGACY DESCRIPTION VALUES
                // AC ui_certified_ceiling
                // AC ui_max_range
                // AC ui_autonomy
                // FM cruise_speed

                foreach (StackPanel panel in AircraftProcess.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        foreach (var chkbx in panel.Children)
                        {
                            CheckBox b = new CheckBox();
                            if (chkbx.GetType() == b.GetType())
                            {
                                CheckBox a = (CheckBox)chkbx;
                                if (a.IsChecked == true)
                                {
                                    string val = a.Content.ToString().Split(' ')[0].ToLower().Trim();

                                    if (val == "ui_certified_ceiling")
                                    {
                                        Regex regex = new Regex(@"(?i)(.*)ceiling\\t\\n([\d,]*)(.+)(?-i)");
                                        Match match = regex.Match(content);
                                        if (match.Success && match.Groups.Count >= 3)
                                        {
                                            content = content.Replace("ui_createdby", val + " = \"" + match.Groups[2].Value + "\"" + Environment.NewLine + "ui_createdby");
                                        }
                                    }
                                    else if (val == "ui_max_range")
                                    {
                                        Regex regex2 = new Regex(@"(?i)(.*)maximum range\\t\\n([\d,]*)(.+)(?-i)");
                                        Match match2 = regex2.Match(content);
                                        if (match2.Success && match2.Groups.Count >= 3)
                                        {
                                            content = content.Replace("ui_createdby", val + " = \"" + match2.Groups[2].Value + "\"" + Environment.NewLine + "ui_createdby");
                                        }
                                    }
                                    else if (val == "ui_autonomy")
                                    {
                                        Regex regex3 = new Regex(@"(?i)(.*)endurance\\t\\n([\d,]*)(.+)(?-i)");
                                        Match match3 = regex3.Match(content);
                                        if (match3.Success && match3.Groups.Count >= 3)
                                        {
                                            content = content.Replace("ui_createdby", val + " = \"" + match3.Groups[2].Value + "\"" + Environment.NewLine + "ui_createdby");
                                        }
                                    }
                                    else if (val == "cruise_speed")
                                    {
                                        Regex regex4 = new Regex(@"(?i)(.*)cruise speed\\t\\n([\d,]*)(.+)(?-i)");
                                        Match match4 = regex4.Match(content);
                                        if (match4.Success && match4.Groups.Count >= 3)
                                        {
                                            content = content.Replace("ui_createdby", val + " = \"" + match4.Groups[2].Value + "\"" + Environment.NewLine + "ui_createdby");
                                        }
                                    }


                                    //gauges[i] = a.Content.ToString();
                                    i++;
                                }
                            }
                        }
                    }
                }

                if (i > 0)
                {
                    try { File.WriteAllText(aircraftDirectory + "\\aircraft.cfg", content); }
                    catch (Exception)
                    {
                        MessageBox.Show("Can't write into file " + aircraftDirectory + "\\aircraft.cfg");
                    }
                }
            }

            SummaryUpdate();
        }

        public void SummaryAir()
        {

        }

        public void SummaryEngines()
        {
            EnginesData.Children.Clear();

            if (File.Exists(aircraftDirectory + "\\engines.cfg"))
            {
                int criticalIssues = 0;

                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\engines.cfg");
                List<CfgLine> engineLines = CfgHelper.readCSV(content);
                foreach (var line in engineLines)
                {
                    if (line.Name == "engine_type" && (line.Value == "2" || line.Value == "3" || line.Value == "4") ||
                        line.Name == "afterburner_available" && line.Value == "1")
                    {
                        StackPanel myPanel = new StackPanel();
                        myPanel.Height = 16;
                        myPanel.VerticalAlignment = VerticalAlignment.Top;

                        CheckBox checkBox = new CheckBox();
                        checkBox.Content = line.Name + " = " + line.Value;
                        checkBox.Foreground = new SolidColorBrush(Colors.DarkRed);

                        myPanel.Children.Add(checkBox);
                        EnginesData.Children.Add(myPanel);

                        criticalIssues++;
                    }
                }

                StackPanel myPanel2 = new StackPanel();
                if (criticalIssues > 0)
                {
                    Button btn = new Button();
                    btn = SetButtonAtts(btn);
                    btn.Content = "Fix engine issues";
                    btn.Click += FixengineClick;
                    myPanel2.Children.Add(btn);
                    tabEngines.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    TextBlock myBlock = AddTextBlock("No engine issues found", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
                    myPanel2.Children.Add(myBlock);
                    tabEngines.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }
                EnginesData.Children.Add(myPanel2);
            }
        }

        private void FixengineClick(object sender, RoutedEventArgs e) {
            if (File.Exists(aircraftDirectory + "\\engines.cfg"))
            {
                int i = 0;
                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\engines.cfg");

                foreach (StackPanel panel in EnginesData.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if (a.IsChecked == true)
                            {
                                string[] val = a.Content.ToString().Split('=');

                                content = Regex.Replace(content, "(?i)" + val[0].Trim() + "(.?)=(.?)" + val[1].Trim() + "(?-i)", val[0].Trim() + " = 0");

                                //gauges[i] = a.Content.ToString();
                                i++;
                            }
                        }
                    }
                }

                if (i > 0)
                {
                    try { File.WriteAllText(aircraftDirectory + "\\engines.cfg", content); }
                    catch (Exception)
                    {
                        MessageBox.Show("Can't write into file " + aircraftDirectory + "\\engines.cfg");
                    }
                }
            }

            SummaryUpdate();
        }

        public void SummarySystems()
        {
            SystemsData.Children.Clear();

            if (aircraftDirectory != "" && File.Exists(aircraftDirectory + "\\systems.cfg"))
            {
                int lightsBroken = 0;
                foreach (var light in CfgHelper.getLights(aircraftDirectory))
                {
                    if (!String.IsNullOrEmpty(light))
                    {
                        StackPanel myPanel = new StackPanel();
                        myPanel.Height = 16;
                        myPanel.VerticalAlignment = VerticalAlignment.Top;

                        if (light[0] == '-')
                        {
                            CheckBox checkBox = new CheckBox();

                            checkBox.Content = light.Substring(1, light.Length - 1);
                            checkBox.Foreground = new SolidColorBrush(Colors.DarkRed);
                            myPanel.Children.Add(checkBox);

                            lightsBroken++;
                        }
                        else
                        {
                            TextBlock myBlock = AddTextBlock(light, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
                            myPanel.Children.Add(myBlock);
                        }

                        SystemsData.Children.Add(myPanel);
                    }

                }

                if (lightsBroken > 0)
                {
                    StackPanel myPanel = new StackPanel();
                    Button btn = new Button();
                    btn = SetButtonAtts(btn);
                    btn.Content = "Fix broken lights";
                    btn.Click += FixLightsClick;
                    myPanel.Children.Add(btn);
                    SystemsData.Children.Add(myPanel);
                }

                if (lightsBroken > 0)
                {
                    tabSystems.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    tabSystems.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }

                //SystemContent.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void FixLightsClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(aircraftDirectory + "\\systems.cfg"))
            {
                int i = 0;
                string content = System.IO.File.ReadAllText(aircraftDirectory + "\\systems.cfg");

                foreach (StackPanel panel in SystemsData.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if (a.IsChecked == true)
                            {
                                string val = a.Content.ToString();

                                if (val.ToLower().Trim().StartsWith("light.")) {
                                    // CONVERTS LIGHT DATA
                                    // light\.(\d+)(.*)=(.*)(\d+),(.*)(\d+),(.*)(\d+),(.*)(fx_[A-Za-z]+)(.*)
                                    // MSFS lightdef.0 = Type:1#Index:1#LocalPosition:-11.5,0,11.6#LocalRotation:0,0,0#EffectFile:LIGHT_ASOBO_BeaconTop#Node:#PotentiometerIndex:1#EmMesh:LIGHT_ASOBO_BeaconTop
                                    // FSX light.0 = 3,  -39.00, -23.6,  -0.25, fx_navredm ,
                                    string[] fsxLight = val.Split('=');
                                    if (fsxLight.Length >= 2) {
                                        string fsxNum = fsxLight[0].Replace("light.","");
                                        string[] fsxData = fsxLight[1].Split(',');
                                        if (fsxData.Length >= 5)
                                        {
                                            string type = fsxData[0].Replace(" ", "");
                                            string x = fsxData[1].Replace(" ", "");
                                            string y = fsxData[2].Replace(" ", "");
                                            string z = fsxData[3].Replace(" ", "");
                                            Regex regex = new Regex(@"(fx_[A-Za-z]*)");
                                            Match match = regex.Match(fsxData[4]);
                                            Regex digRregex = new Regex("[0-9.-]+");
                                            if (match.Success && digRregex.IsMatch(fsxNum) && digRregex.IsMatch(type) && digRregex.IsMatch(x) && digRregex.IsMatch(y) && digRregex.IsMatch(z))
                                            {
                                                string msfsLight = "lightdef."+ fsxNum + " = Type:"+getMfsfLightType(type) +"#Index:0#LocalPosition:"+x+","+y+","+z+"#LocalRotation:0,0,0#EffectFile:"+ getMfsfLightEff(match.Value) + "#Node:#PotentiometerIndex:1#EmMesh:" + getMfsfLightEff(match.Value);
                                                content = content.Replace(val, msfsLight);
                                                i++;

                                                Console.WriteLine(val);
                                                Console.WriteLine(msfsLight);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (i > 0)
                {
                    try { File.WriteAllText(aircraftDirectory + "\\systems.cfg", content); }
                    catch (Exception)
                    {
                        MessageBox.Show("Can't write into file " + aircraftDirectory + "\\systems.cfg");
                    }
                }
            }

            SummaryUpdate();
        }

        public string getMfsfLightEff(string fsxEff)
        {
            switch (fsxEff)
            {
                case "fx_beacon":
                    return "LIGHT_ASOBO_BeaconTop";
                case "fx_beaconb":
                    return "LIGHT_ASOBO_BeaconBelly";
                case "fx_beaconh":
                    return "LIGHT_ASOBO_BeaconTop_POS";
                case "fx_landing":
                    return "LIGHT_ASOBO_Landing";
                case "fx_navgre":
                    return "LIGHT_ASOBO_NavigationGreen";
                case "fx_navgrem":
                    return "LIGHT_ASOBO_Navigation_Green";
                case "fx_navred":
                    return "LIGHT_ASOBO_NavigationRed";
                case "fx_navredm":
                    return "LIGHT_ASOBO_Navigation_Red";
                case "fx_navwhi":
                    return "LIGHT_ASOBO_NavigationWhite";
                case "fx_navwhih":
                    return "LIGHT_ASOBO_NavigationWhite";
                case "fx_recog":
                    return "LIGHT_ASOBO_RecognitionLeft";
                case "fx_strobe":
                    return "LIGHT_ASOBO_StrobeSimple";
                case "fx_strobeh":
                    return "LIGHT_ASOBO_StrobeBelly";
                case "fx_vclight":
                    return "LIGHT_ASOBO_CabinBounceLarge";//"LIGHT_ASOBO_Gauge";
                case "fx_vclighth":
                    return "LIGHT_ASOBO_CabinBounceSmall";//"LIGHT_ASOBO_GaugeSmall";
                case "fx_vclightwhi":
                    return "LIGHT_ASOBO_CabinBounce";//"LIGHT_ASOBO_GaugeSmall";
                default:
                    return "LIGHT_ASOBO_BeaconTop";
            }
        }

        public string getMfsfLightType(string fsxType)
        {
            return fsxType;
        }

        public void SummaryCockpit()
        {
            CockpitData.Children.Clear();

            if (File.Exists(aircraftDirectory + "\\cockpit.cfg"))
            {
                int gaugesMissing = 0;
                int gaugesTotal = 0;
                if (aircraftDirectory != "" && File.Exists(aircraftDirectory + @"\cockpit.cfg"))
                {
                    foreach (var secton in CfgHelper.getInstruments(aircraftDirectory))
                    {
                        if (!String.IsNullOrEmpty(secton) && !secton.Contains("VERSION"))
                        {
                            StackPanel myPanel = new StackPanel();
                            myPanel.Height = 16;
                            myPanel.VerticalAlignment = VerticalAlignment.Top;

                            if (secton[0] == '-')
                            {
                                CheckBox checkBox = new CheckBox();

                                checkBox.Content = secton.Replace("-", "");
                                if (secton.Contains("FUEL_QUANTITY") || secton.Contains("AIRSPEED") || secton.Contains("RPM") || 
                                    secton.Contains("THROTTLE_LEVELS") || secton.Contains("FLAPS_LEVELS"))
                                    checkBox.Foreground = new SolidColorBrush(Colors.DarkRed);
                                else
                                    checkBox.Foreground = new SolidColorBrush(Colors.DarkOrange);
                                myPanel.Children.Add(checkBox);

                                gaugesMissing++;
                            }
                            else
                            {
                                TextBlock myBlock = AddTextBlock(secton, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
                                myPanel.Children.Add(myBlock);
                            }

                            CockpitData.Children.Add(myPanel);

                            gaugesTotal++;
                        }
                            
                    }
                }

                if (gaugesMissing > 0)
                {
                    StackPanel myPanel = new StackPanel();
                    Button btn = new Button();
                    btn = SetButtonAtts(btn);
                    btn.Content = "Enable selected gauges";
                    btn.Click += EnableGaugesClick;
                    myPanel.Children.Add(btn);
                    CockpitData.Children.Add(myPanel);
                }

                if (gaugesTotal - gaugesMissing < 5)
                {
                    tabCockpit.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else if (gaugesMissing > 0)
                {
                    tabCockpit.Foreground = new SolidColorBrush(Colors.DarkOrange);
                }
                else
                {
                    tabCockpit.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }

                //CockpitContent.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void EnableGaugesClick(object sender, RoutedEventArgs e)
        {
            string[] gauges = new string[100];
            int i = 0;

            foreach (StackPanel panel in CockpitData.Children)
            {
                if (panel.Children.Count > 0)
                {
                    CheckBox b = new CheckBox();
                    if (panel.Children[0].GetType() == b.GetType())
                    {
                        CheckBox a = (CheckBox)panel.Children[0];
                        if (a.IsChecked == true)
                        {
                            gauges[i] = a.Content.ToString();
                            i++;
                        }
                    }
                }
            }

            CfgHelper.enableGauges(aircraftDirectory, gauges);
            SummaryUpdate();
        }

        public void SummaryTextures()
        {
            TexturesList.Children.Clear();

            int texturesToConvert = 0;
            if (aircraftDirectory != "")
            {
                foreach (var subdir in Directory.GetDirectories(aircraftDirectory))
                {
                    string folderName = subdir.Split('\\').Last().ToLower().Trim();
                    if (folderName[0] != '.' && folderName.Contains("texture"))
                    {
                        var txtFiles = Directory.EnumerateFiles(subdir, "*.bmp", SearchOption.TopDirectoryOnly);

                        foreach (string currentFile in txtFiles)
                        {
                            string fileName = currentFile.Replace(projectDirectory, "");

                            if (Path.GetFileName(fileName)[0] != '.')
                            {
                                StackPanel myPanel = new StackPanel();
                                myPanel.Height = 15;
                                myPanel.VerticalAlignment = VerticalAlignment.Top;

                                CheckBox checkBox = new CheckBox();
                                checkBox.Content = fileName;
                                checkBox.Foreground = new SolidColorBrush(Colors.DarkRed);
                                myPanel.Children.Add(checkBox);
                                TexturesList.Children.Add(myPanel);

                                texturesToConvert++;
                            }
                        }
                    }
                }
            }

            StackPanel myPanel2 = new StackPanel();
            Button btn = new Button();
            btn = SetButtonAtts(btn);

            if (texturesToConvert > 0)
            {
                btn.Content = "Convert selected textures";
                btn.Click += ConvertTexturesClick;
                tabTextures.Foreground = new SolidColorBrush(Colors.DarkRed);
            }
            else
            {
                btn.Content = "All textures converted";
                btn.IsEnabled = false;
                tabTextures.Foreground = new SolidColorBrush(Colors.DarkGreen);
            }

            myPanel2.Children.Add(btn);
            TexturesList.Children.Add(myPanel2);
        }

        private void ConvertTexturesClick(object sender, RoutedEventArgs e)
        {
            foreach (StackPanel panel in TexturesList.Children)
            {
                if (panel.Children.Count > 0)
                {
                    CheckBox b = new CheckBox();
                    if (panel.Children[0].GetType() == b.GetType())
                    {
                        CheckBox a = (CheckBox)panel.Children[0];
                        if (a.IsChecked == true)
                        {
                            string bmp = projectDirectory + a.Content.ToString().ToLower();
                            string dds = projectDirectory + a.Content.ToString().ToLower().Replace("bmp", "dds");
                            if (File.Exists(dds))
                                File.Delete(dds);

                            Process process = new Process();
                            process.StartInfo.FileName = "cmd.exe";
                            //process.StartInfo.WorkingDirectory = "c:\temp";
                            //process.StartInfo.Arguments = "somefile.txt";

                            Process.Start(AppDomain.CurrentDomain.BaseDirectory + "nvdxt.exe", "-dxt5 -quality_highest -flip -file \"" + bmp + "\" -output \"" + dds + "\"");

                            Stopwatch sw = new Stopwatch();
                            sw.Start();

                            while (true)
                            {
                                if (sw.ElapsedMilliseconds > 500) break;
                            }

                            if (File.Exists(dds))
                            {
                                File.Move(Path.GetDirectoryName(bmp) + "\\" + Path.GetFileName(bmp),
                                    Path.GetDirectoryName(bmp) + "\\." + Path.GetFileName(bmp));
                            }
                        }
                    }
                }
            }

            JSONHelper.scanTargetFolder(projectDirectory);
            SummaryUpdate();
        }


        public string Get_aircraft_directory()
        {
            String[] childFolders = Directory.GetDirectories(projectDirectory + @"\SimObjects\Airplanes\");
            if (childFolders.Length > 0)
            {
                return childFolders[0];
            }
            else
            {
                return "";
            }
        }

        public Button SetButtonAtts(Button btn)
        {
            btn.MinHeight = 30;
            btn.FontSize = 20;
            btn.Margin = new Thickness(5);
            btn.FontFamily = new FontFamily("Arial Black");
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            btn.VerticalAlignment = VerticalAlignment.Top;

            return btn;
        }

        public async System.Threading.Tasks.Task CheckUpdateAsync()
        {
            /*WebClient wc = new WebClient();
            wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler(wc_dc);
            wc.DownloadStringAsync(new Uri(updatedirectory));*/

            //void wc_dc(object sender, DownloadStringCompletedEventArgs e)
            {
                string pubVer = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                //if (e.Error == null)
                {
                    //Assembly assembly = Assembly.GetExecutingAssembly();
                    //FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                    //string productVersion = fileVersionInfo.ProductVersion;

                    //string data = e.Result;
                    var client = new HttpClient();
                    string data = await client.GetStringAsync(updatedirectory);

                    Version nullVer = Version.Parse("0.0.0.0");

                    var regex = new Regex("href\\s*=\\s*(?:\"(?<1>[^\"]*)\"|(?<1>\\S+))");
                    foreach (var match in regex.Matches(data))
                    {
                        string url = match.ToString().Replace("href=", "").Replace("\"", "");

                        if (url.Contains(".exe") || url.Contains(".zip"))
                        {
                            // COMPARE VERSIONS
                            if (Version.TryParse(Regex.Replace(url, "[^0-9.]", "").TrimEnd('.'), out nullVer))
                            {
                                Version ver = Version.Parse(Regex.Replace(url, "[^0-9.]", "").TrimEnd('.'));
                                if (ver > Version.Parse(pubVer))
                                {
                                    updateVersion = ver.ToString();
                                    updateURL = updatedirectory + url;
                                }
                                Console.WriteLine(ver + " " + pubVer);
                            }
                        }
                    }

                    Button btn = null;
                    Button btn2 = null;
                    StackPanel myPanel = new StackPanel();
                    TextBlock myBlock = AddTextBlock("", HorizontalAlignment.Center, VerticalAlignment.Top, Colors.DarkGreen);

                    if (updateVersion != "")
                    {
                        btn = new Button();
                        btn2 = new Button();
                        btn = SetButtonAtts(btn);
                        btn.Content = "Update automatically";
                        btn.Click += UpdateAutomaticallyClick;

                        btn2 = SetButtonAtts(btn2);
                        btn2.Content = "Update manually";
                        btn2.Click += UpdateManuallyClick;

                        myBlock.Text = "Update ver" + updateVersion + " available";
                        myBlock.Foreground = new SolidColorBrush(Colors.DarkRed);

                        tabAbout.Foreground = new SolidColorBrush(Colors.DarkRed);
                    }
                    else
                    {
                        myBlock.Text = "You are using latest program version";
                        myBlock.Foreground = new SolidColorBrush(Colors.Black);
                    }

                    myPanel.Children.Add(myBlock);

                    if (btn != null)
                        myPanel.Children.Add(btn);
                    if (btn2 != null)
                        myPanel.Children.Add(btn2);

                    AboutContent.Children.Add(myPanel);

                }
            }
        }

        public void UpdateAutomaticallyClick(object sender, RoutedEventArgs e)
        {
            if (updateURL != "")
            {
                WebClient _webClient = new WebClient();
                //_webClient.DownloadProgressChanged += OnDownloadProgressChanged;
                _webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(OnDownloadCompleted);

                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = AddTextBlock("Applying update ver" + updateVersion, HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black);
                myPanel.Children.Add(myBlock);
                AboutContent.Children.Add(myPanel);

                _webClient.DownloadFileAsync(new Uri(updateURL), AppDomain.CurrentDomain.BaseDirectory + "\\" + TEMP_FILE);
            }
        }

        public void UpdateManuallyClick(object sender, RoutedEventArgs e)
        {
            if (updateURL != "")
            {
                Process.Start(updateURL);
            }
        }
        private void OnDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if ((e == null || !e.Cancelled && e.Error == null) && File.Exists(TEMP_FILE) && new System.IO.FileInfo(TEMP_FILE).Length > 10)
            {
                // CHECK EXE BACKUP
                if (File.Exists(EXE_PATH + ".BAK"))
                    File.Delete(EXE_PATH + ".BAK");

                File.Move(EXE_PATH, EXE_PATH + ".BAK");
                Extract extract = new Extract();
                extract.Run(TEMP_FILE, EXE_PATH);
            }
        }

        public void SetUpdateReady()
        {
            if (File.Exists(EXE_PATH))
            {
                Process.Start(EXE_PATH);
                Environment.Exit(0);

                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = AddTextBlock("Update failed", HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black);
                myPanel.Children.Add(myBlock);
                AboutContent.Children.Add(myPanel);
            }
        }

        private void AircraftEditClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(aircraftDirectory + "\\aircraft.cfg"))
            {
                Process.Start(aircraftDirectory + "\\aircraft.cfg");
            }
        }

        private void CockpitEditClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(aircraftDirectory + "\\cockpit.cfg"))
            {
                Process.Start(aircraftDirectory + "\\cockpit.cfg");
            }
        }

        private void SystemsEditClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(aircraftDirectory + "\\systems.cfg"))
            {
                Process.Start(aircraftDirectory + "\\systems.cfg");
            }
        }

        private void EnginesEditClick(object sender, RoutedEventArgs e)
        {
            if (File.Exists(aircraftDirectory + "\\engines.cfg"))
            {
                Process.Start(aircraftDirectory + "\\engines.cfg");
            }
        }

        public TextBlock AddTextBlock(string text, HorizontalAlignment ha, VerticalAlignment va, Color clr)
        {
            TextBlock myBlock = new TextBlock();
            myBlock.HorizontalAlignment = ha;
            myBlock.VerticalAlignment = va;
            myBlock.Text = text;
            myBlock.Foreground = new SolidColorBrush(clr);

            return myBlock;
        }

        private void BtnOpenTargetFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0", "CommunityPath",
                new string[] { "C:\\\\" });
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                TargetFolder = dialog.FileName + "\\";
                btnTargetFolderPath.Text = TargetFolder;
            }
        }

        private void BtnOpenSourceFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\10.0", "SetupPath",
                new string[] { "C:\\\\" });
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (File.Exists(dialog.FileName + "\\aircraft.cfg"))
                {
                    SourceFolder = dialog.FileName + "\\";
                    btnSourceFolderPath.Text = SourceFolder;
                }
                else
                {
                    SourceFolder = "";
                    MessageBox.Show("Folder " + dialog.FileName + " does not contain aircraft.cfg");
                }
            }

        }

        private void BtnImportSubmit_Click(object sender, RoutedEventArgs e)
        {
            // VALIDATE FIELDS
            if (TargetFolder == "" || SourceFolder == "")
                MessageBox.Show("You have to select both FSX ans MSFS folders");
            else if (String.IsNullOrWhiteSpace(PackageTitle.Text) || String.IsNullOrWhiteSpace(PackageDir.Text) || String.IsNullOrWhiteSpace(PackageManufacturer.Text) || String.IsNullOrWhiteSpace(PackageAuthor.Text) ||
                String.IsNullOrWhiteSpace(PackageVer1.Text) || String.IsNullOrWhiteSpace(PackageVer2.Text) || String.IsNullOrWhiteSpace(PackageVer3.Text) ||
                String.IsNullOrWhiteSpace(PackageMinVer1.Text) || String.IsNullOrWhiteSpace(PackageMinVer2.Text) || String.IsNullOrWhiteSpace(PackageMinVer3.Text))
                MessageBox.Show("You have to fill in all fields");
            else if (Directory.Exists(TargetFolder + PackageDir.Text + "\\"))
            {
                MessageBox.Show("Directory " + TargetFolder + PackageDir.Text + " already exists");
                return;
            }

            string[] data = new string[] { "", "AIRCRAFT", PackageTitle.Text, PackageManufacturer.Text, PackageAuthor.Text,
            PackageVer1.Text + "." + PackageVer2.Text + "." + PackageVer3.Text, PackageMinVer1.Text + "." + PackageMinVer2.Text + "." + PackageMinVer2.Text, "" };

            JSONHelper.createManifest(this, SourceFolder, TargetFolder + PackageDir.Text + "\\", data);
    }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            JSONHelper.scanTargetFolder(projectDirectory);
        }

        private void Hyperlink_RequestNavigate(object sender,
                                               System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString().Contains("//") ? e.Uri.AbsoluteUri : e.Uri.ToString());
        }
    }

}
