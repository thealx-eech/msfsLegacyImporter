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
using System.Collections.Generic;
using System.Net.Http;
using System.Globalization;

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
        private csvHelper CsvHelper;

        string SourceFolder = "";
        string TargetFolder = "";

        FileSystemWatcher fileTrackWatcher = null;

        public MainWindow()
        {
            CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            // REMOVE TEMP FILES
            try
            {
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + EXE_PATH + ".BAK");
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + TEMP_FILE);
            }
            catch { /*MessageBox.Show("Can't delete temporary files");*/ }

            InitializeComponent();

            // STYLE BUTTONS
            btnOpenFile = SetButtonAtts(btnOpenFile);
            btnSourceFolder = SetButtonAtts(btnSourceFolder);
            btnTargetFolder = SetButtonAtts(btnTargetFolder);
            btnScan = SetButtonAtts(btnScan);
            btnImportSubmit = SetButtonAtts(btnImportSubmit);
            btnAircraftProcess = SetButtonAtts(btnAircraftProcess);

            // HIDE TABS
            int k = 0;
            foreach (TabItem item in fsTabControl.Items)
            {
                if (k > 0 && k < fsTabControl.Items.Count - 1)
                    item.Visibility = Visibility.Collapsed;
                k++;
            }

            JSONHelper = new jsonHelper();
            CfgHelper = new cfgHelper();
            CsvHelper = new csvHelper();
            // TRY TO LOAD CFGTPL FILES
            if (!CfgHelper.processCfgfiles(AppDomain.CurrentDomain.BaseDirectory + "\\cfgTpl\\"))
                Environment.Exit(1);

            _ = CheckUpdateAsync();
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            string defaultPath = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            dialog.InitialDirectory = defaultPath;
            dialog.IsFolderPicker = true;
            dialog.RestoreDirectory = (String.IsNullOrEmpty(defaultPath) || defaultPath == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) ? true : false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                setAircraftDirectory(dialog.FileName);
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
                    Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath", System.IO.Path.GetDirectoryName(directory));
                }
                catch (Exception) { }

                // CLEAN FIELDS
                PackageTitle.Text = "";
                PackageDir.Text = "";
                PackageManufacturer.Text = "";
                PackageAuthor.Text = "";

                projectDirectory = directory;
                aircraftDirectory = Get_aircraft_directory();

                if (aircraftDirectory != "")
                {
                    Header.Text = "Current aircraft: " + new DirectoryInfo(projectDirectory).Name;
                    btnOpenFile.Content = "Select another aircraft";

                    btnOpenFilePath.Text = projectDirectory;

                    CfgHelper.processCfgfiles(aircraftDirectory + "\\", true);

                    // TRACK CFG FILES
                    if (fileTrackWatcher != null)
                        fileTrackWatcher.Dispose();
                    fileTrackWatcher = new FileSystemWatcher();

                    fileTrackWatcher.Filter = "*.cfg";
                    fileTrackWatcher.Changed += new FileSystemEventHandler(trackCfgEdit);
                    fileTrackWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    fileTrackWatcher.Path = aircraftDirectory + "\\";
                    fileTrackWatcher.EnableRaisingEvents = true;

                    // CHECK FOR DLL FILES
                    if (Directory.GetFiles(aircraftDirectory, "*.dll", SearchOption.AllDirectories).Length > 0)
                    {
                        LoadLabel.Text = "Aircraft folder contains DLL files - anvionics or even animation may not work in the game";
                        LoadLabel.Foreground = new SolidColorBrush(Colors.DarkRed);
                    }
                    else {
                        LoadLabel.Text = "Click on tabs to discover available features";
                        LoadLabel.Foreground = new SolidColorBrush(Colors.Black);
                    }

                    SummaryUpdate();
                }
            }
            else
            {
                MessageBox.Show("Selected folder " + directory + " should contain SimObjects folder, and layout.json + manifest.json files");
            }
        }

        public void trackCfgEdit(object sender, FileSystemEventArgs e)
        {
            if (DateTime.UtcNow.Ticks - CfgHelper.lastChangeTimestamp > 10000000)
            {
                Dispatcher.Invoke(() => fsTabControl.IsEnabled = false);
                MessageBoxResult messageBoxResult = MessageBox.Show("File " + System.IO.Path.GetFileName(e.FullPath) + " was edited outside of the program" + Environment.NewLine + "Aircraft data should be reloaded", "CFG file change detected", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    CfgHelper.processCfgfiles(aircraftDirectory + "\\", true);
                    Dispatcher.Invoke(() => SummaryUpdate());
                    Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
                    CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                } else
                {
                    CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                    Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
                }
            }
        }

        private void SummaryUpdate()
        {
            // RESET TABS COLOR
            foreach (var item in fsTabControl.Items)
                ((TabItem)item).Foreground = new SolidColorBrush(Colors.Black);

            SummaryAircraft();
            SummaryEngines();
            SummarySections();
            SummarySystems();
            SummaryFlightModel();
            SummaryTextures();
            SummaryModels();

            foreach (TabItem item in fsTabControl.Items)
            {
                switch (item.Name)
                {
                    case "tabAircraft":
                        if (CfgHelper.cfgFileExists("aircraft.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabCockpit":
                        if (CfgHelper.cfgFileExists("cockpit.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabSystems":
                        if (CfgHelper.cfgFileExists("systems.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabFlightModel":
                        if (CfgHelper.cfgFileExists("flight_model.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabEngines":
                        if (CfgHelper.cfgFileExists("engines.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;
                    case "tabRunway":
                        if (CfgHelper.cfgFileExists("engines.cfg") && CfgHelper.cfgFileExists("flight_model.cfg"))
                            item.Visibility = Visibility.Visible;
                        else
                            item.Visibility = Visibility.Collapsed;
                        break;

                    default:
                        item.Visibility = Visibility.Visible;
                        break;
                }
            }

            // SET BACKUP BUTTONS LABEL
            foreach (Button btn in new Button[] { AircraftBackupButton, EnginesBackupButton, CockpitBackupButton, SystemsBackupButton, FlightModelBackupButton, RunwayBackupButton, RunwayBackupButton, ModelBackupButton })
                if (!String.IsNullOrEmpty(btn.Tag.ToString())) {
                    string mainFile = aircraftDirectory + "\\" + btn.Tag.ToString();
                    string backupFile = Path.GetDirectoryName(mainFile) + "\\." + Path.GetFileName(mainFile);

                    if (File.Exists(backupFile))
                        btn.Content = "Restore Backup";
                    else
                        btn.Content = "Make Backup";
                }
        }

        // AIRCRAFT START
        public void SummaryAircraft()
        {
            int status = 2;
            StackPanel myPanel = new StackPanel();

            AircraftProcess.Children.Clear();
            btnAircraftProcess.IsEnabled = false;

            //AircraftProcess
            if (aircraftDirectory != "" && File.Exists(aircraftDirectory + @"\aircraft.cfg"))
            {

                foreach (var file in new[] { "aircraft.cfg", "cameras.cfg", "cockpit.cfg", "engines.cfg", "flight_model.cfg", "gameplay.cfg", "systems.cfg", ".unknown.cfg" })
                {
                    TextBlock myBlock;
                    if (file == ".unknown.cfg" && File.Exists(aircraftDirectory + "\\" + file))
                    {
                        myBlock = addTextBlock(file, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkOrange);
                    } else if (file == ".unknown.cfg")
                    {
                        myBlock = addTextBlock("", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
                    }
                    else if (!File.Exists(aircraftDirectory + "\\" + file))
                    {
                        myBlock = addTextBlock(file + " is missing", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkRed);
                        //cfgsList += "<Run Foreground=\"Red\">" + file + " is missing" + "</ Run >"  + Environment.NewLine;
                        status = 0;
                    }
                    else
                    {
                        myBlock = addTextBlock(file + " is set", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
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
            }
            else
            {
                //AircraftContent.Text = "aircraft.cfg file not exists!";
                tabAircraft.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            AircraftProcess.Children.Add(myPanel);

            StackPanel myPanel2 = new StackPanel();
            AircraftPerformance.Children.Clear();

            // DESCRIPTION FIX
            if (CfgHelper.cfgFileExists("flight_model.cfg"))
            {
                int criticalIssues = 4;
                string[] requiredValues = new string[] { "ui_certified_ceiling", "ui_max_range", "ui_autonomy", "cruise_speed" };

                foreach (var requiredValue in requiredValues)
                {
                    string value = CfgHelper.getCfgValue(requiredValue, "aircraft.cfg", "[FLTSIM.0]");
                    if (value != "")
                    {
                        if (int.TryParse(value.Contains('.') ? value.Trim('"').Trim().Split('.')[0] : value.Trim('"').Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int num) && num > 0)
                        {
                            if (num != 0)
                            {
	                            requiredValues = requiredValues.Where(val => val != requiredValue).ToArray();
	                            criticalIssues--;
                            }
                        }
                    }
                }

                if (criticalIssues > 0)
                {
                    int i = 0;
                    foreach (var requiredValue in requiredValues)
                        AircraftPerformance = AddCheckBox(AircraftPerformance, requiredValue + " performance parameter missing", Colors.DarkOrange, i++);


                    Button btn = new Button();
                    btn = SetButtonAtts(btn);
                    btn.Content = "Try to add missing performance parameters";
                    btn.Click += AddDescriptionClick;
                    myPanel2.Children.Add(btn);
                }
            }

            // AC performance = 

            // AC ui_certified_ceiling
            // AC ui_max_range
            // AC ui_max_range
            // FM cruise_speed

            AircraftPerformance.Children.Add(myPanel2);
        }

        private void BtnAircraftProcess_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(aircraftDirectory + "\\aircraft.cfg"))
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("aircraft.cfg not found in aircraft directory", "", MessageBoxButton.OK);
            }
            else
            {
                // PROCESS AIRCRAFT FILE
                if (File.Exists(aircraftDirectory + "\\.aircraft.cfg"))
                {
                    MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure it can be removed?", "Backup of aircraft.cfg already exists", System.Windows.MessageBoxButton.YesNo);
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(aircraftDirectory + "\\.aircraft.cfg");
                            CfgHelper.splitCfg(aircraftDirectory);
                        }
                        catch (Exception) {
                            MessageBox.Show("CFG split failed");
                        }
                        
                    }
                }
                else
                {
                    CfgHelper.splitCfg(aircraftDirectory);
                }

                JSONHelper.scanTargetFolder(projectDirectory);
                SummaryUpdate();
            }
        }

        public void AddDescriptionClick(object sender, RoutedEventArgs e)
        {
            int i = 0;

            foreach (StackPanel panel in AircraftPerformance.Children)
            {
                if (panel.Children.Count > 0)
                {
                    foreach (var chkbx in panel.Children)
                    {
                        CheckBox b = new CheckBox();
                        if (chkbx.GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)chkbx;
                            if (a.IsChecked == true && (string)a.Content != "Toggle all")
                            {
                                string val = a.Content.ToString().Split(' ')[0].ToLower().Trim();
                                string name = "";

                                if (val == "ui_certified_ceiling")
                                    name = "ceiling";
                                else if (val == "ui_max_range")
                                    name = "range";
                                else if (val == "ui_autonomy")
                                    name = "endurance";
                                else if (val == "cruise_speed")
                                    name = "cruise speed";

                                //Console.WriteLine(name);

                                if (name != "")
                                {
                                    string value = "";

                                    if (val != "cruise_speed")
                                    {
                                        Regex regex = new Regex(@"(?i)(.*)" + name + @"(\\t\\n|\\n)([\d,]+)(.+)(?-i)");
                                        Match match = regex.Match(CfgHelper.getCfgValue("performance", "aircraft.cfg", "[GENERAL]"));

                                        if (match.Success && match.Groups.Count >= 3)
                                            value = match.Groups[3].Value.Replace(",", "");
                                    } else
                                    {
                                        value = CfgHelper.getCfgValue(val, "flight_model.cfg", "[REFERENCE SPEEDS]");
                                    }

                                    if (value != "")
                                    {
                                        CfgHelper.setCfgValue(aircraftDirectory, val, value, "aircraft.cfg", "[FLTSIM.0]");
                                        i++;
                                    }
                                }

                            }
                        }
                    }

                    if (i > 0)
                        CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "aircraft.cfg" });
                }
            }

            SummaryUpdate();
        }
        // AIRCRAFT END

        // ENGINES START
        public void SummaryEngines()
        {
            EnginesData.Children.Clear();

            if (CfgHelper.cfgFileExists("engines.cfg"))
            {
                int criticalIssues = 0;

                string engine_type = CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]");
                if (engine_type == "2" || engine_type == "3" || engine_type == "4" || engine_type == "")
                    EnginesData = AddCheckBox(EnginesData, "engine_type = " + (engine_type != "" ? engine_type : "missing"), Colors.DarkRed, criticalIssues++);

                string afterburner_available = CfgHelper.getCfgValue("afterburner_available", "engines.cfg", "[TURBINEENGINEDATA]");
                if (afterburner_available == "1")
                    EnginesData = AddCheckBox(EnginesData, "afterburner_available = " + afterburner_available, Colors.DarkRed, criticalIssues++);

                StackPanel myPanel2 = new StackPanel();

                Button btn = new Button();
                btn = SetButtonAtts(btn);

                if (criticalIssues > 0)
                {
                    btn.Content = "Fix engines issues";
                    btn.Click += FixengineClick;

                    tabEngines.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn.Content = "No engines issues";
                    btn.IsEnabled = false;

                    tabEngines.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }

                myPanel2.Children.Add(btn);
                EnginesData.Children.Add(myPanel2);

                getAirCheckboxes(EnginesAir, "engines.cfg");
            }
        }

        private void FixengineClick(object sender, RoutedEventArgs e) {
            int i = 0;

            if (CfgHelper.cfgFileExists("engines.cfg"))
            {
                foreach (StackPanel panel in EnginesData.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if (a.IsChecked == true && (string)a.Content != "Toggle all" && a.Content.ToString().Contains('='))
                            {
                                string[] val = a.Content.ToString().Split('=');

                                CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "0", "engines.cfg");
                                i++;
                            }
                        }
                    }
                }
            }

            if (i > 0)
                CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "engines.cfg" });

            SummaryUpdate();
        }

        private void BtnEnginePowerClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("engines.cfg"))
            {
                Button btn = (Button)sender;

                CfgHelper.adjustEnginesPower(aircraftDirectory, double.Parse(btn.Tag.ToString()));
            }
        }
        // ENGINES END

        // AIR START
        private void getAirCheckboxes(StackPanel parent, string filename)
        {
            string airFilename = Path.GetFileName(aircraftDirectory).Trim('\\') + ".air";

            int values = 0;
            StackPanel myPanel = new StackPanel();

            string airExported = AppDomain.CurrentDomain.BaseDirectory + "airTbls\\" + airFilename.Replace(".air", ".txt");
            string conversionTable = AppDomain.CurrentDomain.BaseDirectory + "\\airTbls\\AIR to CFG Master Sheet - " + filename + ".csv";
            string buttonLabel = "";
            string toolTip = "";

            parent.Children.Clear();

            if (!File.Exists(conversionTable))
            {
                buttonLabel = "No AIR conversion table found";
                toolTip = "File " + conversionTable + " does not exists." + Environment.NewLine + "Extract folder \"airTbls\" from program archive into same directory, where EXE is stored.";
            }
            else if (!File.Exists(airExported))
            {
                buttonLabel = "No exported AIR table data found";
                toolTip = "File " + airExported + " does not exists." + Environment.NewLine + "You can generate this file from AircraftAirfileManager - File - ASCII export menu item";
            }
            else
            {
                buttonLabel = "No AIR values available for import";
                toolTip = "AIR and CFG values identical. You can try to insert" + Environment.NewLine + "missing sections without default values activation.";
                // GET FSX->ASOBO TABLE
                // NAME VALUE
                var fsx2msfsTable = CsvHelper.processAirTable(conversionTable, new string[] { "Table/Record", "Asobo" });

                // GET AIR DUMP
                // NAME COMMENT VALUE
                //var fsxAirTable = CsvHelper.processAirFile(airExported);
                var fsxAirTable = CsvHelper.processAirDump(airExported);

                // compute_aero_center = 0


                if (fsx2msfsTable != null && fsx2msfsTable.Count > 0 && fsxAirTable != null && fsxAirTable.Count > 0)
                {
                    foreach (string[] asoboLine in fsx2msfsTable)
                    {
                        string id = asoboLine[0];
                        string attr = asoboLine[1];

                        foreach (string[] airLine in fsxAirTable)
                        {
                            if (airLine[0] == id && !String.IsNullOrEmpty(attr))
                            {
                                string oldVal = CfgHelper.getCfgValue(attr, filename, "", false);
                                string defVal = CfgHelper.getCfgValue(attr, filename, "", true);
                                if (defVal != "" && airLine[2] != oldVal)
                                {
                                    parent = AddCheckBox(parent, attr, Colors.Black, values++, attr + "=" + airLine[2]);

                                    Grid DynamicGrid = new Grid();
                                    ColumnDefinition gridCol1 = new ColumnDefinition();
                                    ColumnDefinition gridCol2 = new ColumnDefinition();
                                    RowDefinition gridRow1 = new RowDefinition();
                                    RowDefinition gridRow2 = new RowDefinition();
                                    DynamicGrid.ColumnDefinitions.Add(gridCol1);
                                    DynamicGrid.ColumnDefinitions.Add(gridCol2);

                                    TextBlock myBlock2 = addTextBlock("New val: " + airLine[2], HorizontalAlignment.Left, VerticalAlignment.Top,
                                        airLine[2] == "0" || airLine[2] == "0.0" || Regex.IsMatch(airLine[2], @"(0,){8,}") || String.IsNullOrWhiteSpace(Regex.Replace(airLine[2], @"[0-9-.]+:([1][.0]+)[,]*", ""))
                                            ? Colors.DarkRed : Colors.Black);
                                    myBlock2.ToolTip = airLine[2].Replace(",", Environment.NewLine);
                                    Grid.SetColumn(myBlock2, 0);
                                    Grid.SetRow(myBlock2, 0);
                                    DynamicGrid.Children.Add(myBlock2);

                                    TextBlock myBlock3;
                                    if (oldVal != "")
                                    {
                                        myBlock3 = addTextBlock("Old val: " + oldVal, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black);
                                        myBlock3.ToolTip = oldVal.Replace(",", Environment.NewLine);
                                    } else
                                    {
                                        myBlock3 = addTextBlock("Default val: " + defVal, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black);
                                        myBlock3.ToolTip = defVal.Replace(",", Environment.NewLine);
                                    }

                                    myBlock2.MaxWidth = myBlock3.MaxWidth = 295;
                                    Grid.SetColumn(myBlock3, 1);
                                    Grid.SetRow(myBlock3, 0);
                                    DynamicGrid.Children.Add(myBlock3);

                                    Border myBorder = new Border() { BorderThickness = new Thickness() { Bottom = 1, Left = 0, Right = 0, Top = 0 }, BorderBrush = new SolidColorBrush(Colors.Black) };
                                    myBorder.Margin = new Thickness(0, 0, 0, -5);
                                    Grid.SetRow(myBorder, 1);
                                    Grid.SetColumn(myBorder, 0);
                                    DynamicGrid.Children.Add(myBorder);
                                    Border myBorder2 = new Border() { BorderThickness = new Thickness() { Bottom = 1, Left = 0, Right = 0, Top = 0 }, BorderBrush = new SolidColorBrush(Colors.Black) };
                                    myBorder2.Margin = new Thickness(0, 0, 0, -5);
                                    Grid.SetRow(myBorder2, 1);
                                    Grid.SetColumn(myBorder2, 1);
                                    DynamicGrid.Children.Add(myBorder2);

                                    DynamicGrid.Margin = new Thickness(0, 0, 0, 10);

                                    parent.Children.Add(DynamicGrid);
                                }
                                break;
                            }
                        }
                    }


                }
                else
                {
                    toolTip = "AIR tables loading failed";
                }
            }

            Button btn = new Button();
            btn = SetButtonAtts(btn);

            if (values > 0)
            {
                btn.Content = "Insert AIR values";
                btn.Click += InsertAirValues;
            }
            else
            {
                btn.Content = buttonLabel + "*";
                btn.IsEnabled = false;
                myPanel.ToolTip = toolTip;
            }

            myPanel.Children.Add(btn);
            parent.Children.Add(myPanel);
        }

        private void InsertAirValues(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            StackPanel myPanel = (StackPanel)button.Parent;
            StackPanel parentPanel = (StackPanel)myPanel.Parent;

            if (parentPanel != null)
            {
                string filename = parentPanel.Tag.ToString();

                if (CfgHelper.cfgFileExists(filename))
                {
                    string[] values = new string[1000];
                    int i = 0;

                    foreach (var tmpPanel in parentPanel.Children)
                    {
                        StackPanel tmp = new StackPanel();
                        if (tmpPanel.GetType() != tmp.GetType())
                            continue;

                        StackPanel panel = (StackPanel)tmpPanel;

                        if (panel.Children.Count > 0)
                        {
                            CheckBox b = new CheckBox();
                            if (panel.Children[0].GetType() == b.GetType())
                            {
                                CheckBox a = (CheckBox)panel.Children[0];
                                if (a.IsChecked == true && (string)a.Content != "Toggle all")
                                {
                                    values[i] = a.Tag.ToString();
                                    i++;
                                }
                            }
                        }
                    }

                    if (i > 0)
                    {
                        // TRY TO MAKE BACKUP
                        if (File.Exists(aircraftDirectory + "\\" + filename) && !File.Exists(aircraftDirectory + "\\." + filename))
                        {
                            CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                            File.Copy(aircraftDirectory + "\\" + filename, aircraftDirectory + "\\." + filename);

                            string airFilename = Path.GetFileName(aircraftDirectory).Trim('\\') + ".air";
                            if (File.Exists(aircraftDirectory + "\\.flight_model.cfg") && File.Exists(aircraftDirectory + "\\.engines.cfg") &&
                                File.Exists(aircraftDirectory + "\\" + airFilename) && !File.Exists(aircraftDirectory + "\\." + airFilename))
                            {
                                File.Move(aircraftDirectory + "\\" + airFilename, aircraftDirectory + "\\." + airFilename);
                                JSONHelper.scanTargetFolder(projectDirectory);
                            }


                        }

                        for (int k = 0; k < i; k++)
                        {
                            string[] value = values[k].Split('=');
                            CfgHelper.setCfgValue(aircraftDirectory, value[0].Trim(), value[1].Trim(), filename);
                        }

                        CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { filename });
                    }

                }

                SummaryUpdate();
            }
        }
        // AIR END

        // SYSTEMS START
        public void SummarySystems()
        {
            SystemsData.Children.Clear();

            if (CfgHelper.cfgFileExists("systems.cfg"))
            {
                int lightsBroken = 0;
                foreach (var light in CfgHelper.getLights(aircraftDirectory))
                    if (!String.IsNullOrEmpty(light))
                        SystemsData = AddCheckBox(SystemsData, light, Colors.DarkRed, lightsBroken++);

                StackPanel myPanel2 = new StackPanel();
                Button btn = new Button();
                btn = SetButtonAtts(btn);

                if (lightsBroken > 0)
                {
                    btn.Content = "Convert lights";
                    btn.Click += FixLightsClick;

                    tabSystems.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn.Content = "No lights issues";
                    btn.IsEnabled = false;

                    tabSystems.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }

                myPanel2.Children.Add(btn);
                SystemsData.Children.Add(myPanel2);

                //SystemContent.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void FixLightsClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("systems.cfg"))
            {
                int i = 0;

                foreach (StackPanel panel in SystemsData.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if (a.IsChecked == true && (string)a.Content != "Toggle all")
                            {
                                string val = a.Content.ToString();

                                if (val.ToLower().Trim().StartsWith("light.")) {
                                    // CONVERTS LIGHT DATA
                                    // light\.(\d+)(.*)=(.*)(\d+),(.*)(\d+),(.*)(\d+),(.*)(fx_[A-Za-z]+)(.*)
                                    // MSFS lightdef.0 = Type:1#Index:1#LocalPosition:-11.5,0,11.6#LocalRotation:0,0,0#EffectFile:LIGHT_ASOBO_BeaconTop#Node:#PotentiometerIndex:1#EmMesh:LIGHT_ASOBO_BeaconTop
                                    // FSX light.0 = 3,  -39.00, -23.6,  -0.25, fx_navredm ,
                                    string[] fsxLight = val.Split('=');
                                    if (fsxLight.Length >= 2) {
                                        string fsxNum = fsxLight[0].Trim().Replace("light.", "");
                                        string[] fsxData = fsxLight[1].Split(',');
                                        if (fsxData.Length >= 5)
                                        {
                                            string type = fsxData[0].Replace(" ", "").Trim();
                                            string x = fsxData[1].Replace(" ", "").Trim();
                                            string y = fsxData[2].Replace(" ", "").Trim();
                                            string z = fsxData[3].Replace(" ", "").Trim();
                                            Regex regex = new Regex(@"(fx_[A-Za-z]*)");
                                            Match match = regex.Match(fsxData[4]);
                                            Regex digRregex = new Regex("[0-9.-]+");
                                            if (match.Success && digRregex.IsMatch(fsxNum) && digRregex.IsMatch(type) && digRregex.IsMatch(x) && digRregex.IsMatch(y) && digRregex.IsMatch(z))
                                            {
                                                string newLight = "Type:" + getMfsfLightType(type) + "#Index:0#LocalPosition:" + x + "," + y + "," + z + "#LocalRotation:0,0,0#EffectFile:" + getMfsfLightEff(match.Value) + "#Node:#PotentiometerIndex:1#EmMesh:" + getMfsfLightEff(match.Value);
                                                CfgHelper.setCfgValue(aircraftDirectory, "lightdef." + fsxNum, newLight, "systems.cfg", "[LIGHTS]");
                                                CfgHelper.setCfgValueStatus(aircraftDirectory, "light." + fsxNum, "systems.cfg", "[LIGHTS]", false);

                                                i++;

                                                //Console.WriteLine(val);
                                                //Console.WriteLine(msfsLight);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (i > 0)
                    CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "systems.cfg" });
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
                    return "LIGHT_ASOBO_CabinBounce";
                case "fx_vclightwhi":
                    return "LIGHT_ASOBO_CabinBounceSmall";
                case "fx_vclighth":
                    return "LIGHT_ASOBO_CabinBounceLarge";
                default:
                    return "LIGHT_ASOBO_BeaconTop";
            }
        }

        public string getMfsfLightType(string fsxType)
        {
            return fsxType;
        }
        // SYSTEMS END

        // FLIGHT MODEL START
        public void SummaryFlightModel()
        {
            FlightModelData.Children.Clear();

            if (CfgHelper.cfgFileExists("flight_model.cfg"))
            {
                int contactPointsBroken = 0;
                string[] contactPoints = CfgHelper.getContactPoints(aircraftDirectory);
                string[] possiblyDamaged = new string[100];
                int possiblyDamagedCounter = 0;

                int firstCounter = 0;
                foreach (string firstContactPoint in contactPoints)
                {
                    int secondCounter = 0;

                    if (String.IsNullOrEmpty(firstContactPoint))
                        continue;

                    foreach (string secondContactPoint in contactPoints)
                    {
                        if (String.IsNullOrEmpty(secondContactPoint))
                            continue;

                        if (!String.IsNullOrEmpty(firstContactPoint) && !String.IsNullOrEmpty(secondContactPoint) && firstContactPoint != secondContactPoint &&
                            secondCounter > firstCounter)
                        {
                            double[] testOne = parseContactPoint(firstContactPoint);
                            double[] testTwo = parseContactPoint(secondContactPoint);

                            if (testOne.Length >= 5 && testTwo.Length >= 5)
                            {
                                double distance = Math.Pow(
                                        Math.Pow(testTwo[2] - testOne[2], 2) +
                                        Math.Pow(testTwo[3] - testOne[3], 2) +
                                        Math.Pow(testTwo[4] - testOne[4], 2),
                                    0.5);

                                if (distance <= 4)
                                {
                                    Color color;
                                    if (distance <= 2)
                                        color = Colors.DarkRed;
                                    else
                                        color = Colors.DarkOrange;

                                    FlightModelData = AddCheckBox(FlightModelData, firstContactPoint, color, contactPointsBroken++);
                                    FlightModelData = AddCheckBox(FlightModelData, secondContactPoint, color, contactPointsBroken);

                                    StackPanel myPanel = new StackPanel();
                                    myPanel.Height = 16;
                                    myPanel.VerticalAlignment = VerticalAlignment.Top;
                                    myPanel.Margin = new Thickness(30, 0, 0, 10);

                                    TextBlock myBlock = addTextBlock(distance.ToString("N2") + "ft between landing gear points", HorizontalAlignment.Left, VerticalAlignment.Top, color);
                                    myPanel.Children.Add(myBlock);
                                    FlightModelData.Children.Add(myPanel);
                                }
                            }
                            else if (firstCounter == 0 && (testOne.Length < 5 ? testOne.Length == 1 : testTwo.Length == 1))
                            {
                                possiblyDamaged[possiblyDamagedCounter] = testOne.Length < 5 ? firstContactPoint : secondContactPoint;
                                possiblyDamagedCounter++;
                            }
                        }

                        secondCounter++;
                    }

                    firstCounter++;
                }

                StackPanel myPanel2 = new StackPanel();
                Button btn = new Button();
                btn = SetButtonAtts(btn);

                if (contactPointsBroken > 0)
                {
                    btn.Content = "Fix contact points duplicates";
                    btn.Click += FixContactsClick;

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn.Content = "No contact points issues";
                    btn.IsEnabled = false;

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }

                myPanel2.Children.Add(btn);

                // BROKEN POINTS WARNING
                string lastPoint = "";
                if (possiblyDamagedCounter > 0)
                {
                    TextBlock headeBlock = addTextBlock("Contact points that possibly formatted incorrectly, press Edit button and fix values manually", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black);
                    myPanel2.Children.Add(headeBlock);

                    foreach (string val in possiblyDamaged)
                    {
                        if (lastPoint != val)
                        {
                            lastPoint = val;
                            TextBlock myBlock = addTextBlock(val, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkRed);
                            myPanel2.Children.Add(myBlock);
                        }
                    }

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkRed);
                }

                FlightModelData.Children.Add(myPanel2);

                // FM ISSUES
                FlightModelIssues.Children.Clear();

                int criticalIssues = 0;

                foreach (string attr in new string[] { "compute_aero_center", "fuselage_length", "fuselage_diameter" })
                {
                    string value = CfgHelper.getCfgValue(attr, "flight_model.cfg");
                    if (value == "")
                        FlightModelIssues = AddCheckBox(FlightModelIssues, attr + " = " + (value != "" ? value : "missing"), Colors.DarkRed, criticalIssues++);
                }

                foreach (string attr in new string[] { "elevator_scaling_table", "aileron_scaling_table", "rudder_scaling_table" })
                {
                    string value = CfgHelper.getCfgValue(attr, "flight_model.cfg");
                    string result = Regex.Replace(value, @"[0-9-.]+:([1][.0]+)[,]*", "");
                    if (String.IsNullOrEmpty(value) || String.IsNullOrEmpty(result.Trim()))
                    {
                        FlightModelIssues = AddCheckBox(FlightModelIssues, attr + " = " + (value != "" ? value : "missing"), Colors.DarkRed, criticalIssues++);
                    }
                }

                StackPanel myPanel3 = new StackPanel();

                Button btn3 = new Button();
                btn3 = SetButtonAtts(btn3);

                if (criticalIssues > 0)
                {
                    btn3.Content = "Fix flight model issues";
                    btn3.Click += FixFlightModelClick;

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn3.Content = "No flight model issues";
                    btn3.IsEnabled = false;
                }

                myPanel3.Children.Add(btn3);
                FlightModelIssues.Children.Add(myPanel3);



                getAirCheckboxes(FlightModelAir, "flight_model.cfg");
            }
        }

        private void FixFlightModelClick(object sender, RoutedEventArgs e)
        {
            int i = 0;

            if (CfgHelper.cfgFileExists("flight_model.cfg"))
            {
                string message = "";

                foreach (StackPanel panel in FlightModelIssues.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if (a.IsChecked == true && (string)a.Content != "Toggle all" && a.Content.ToString().Contains('='))
                            {
                                string val = a.Content.ToString().Split('=')[0].Trim();

                                if (val.Contains("_table"))
                                    CfgHelper.setCfgValue(aircraftDirectory, val, "-0.785:1,0:0.4,0.785:1", "flight_model.cfg");
                                else if (val == "compute_aero_center")
                                    CfgHelper.setCfgValue(aircraftDirectory, val, "1", "flight_model.cfg");
                                else if (val == "fuselage_length")
                                {
                                    string length = CfgHelper.getCfgValue("wing_span", "flight_model.cfg", "[AIRPLANE_GEOMETRY]");
                                    double num;
                                    CfgHelper.setCfgValue(aircraftDirectory, val, (Double.TryParse(length, out num) ? 1.1 * num : 50).ToString(), "flight_model.cfg");
                                    message += val + " value was calculated from wing_span, you'll need to adjust it manually" + Environment.NewLine;
                                }
                                else if (val == "fuselage_diameter")
                                {
                                    string length = CfgHelper.getCfgValue("wing_span", "flight_model.cfg", "[AIRPLANE_GEOMETRY]");
                                    double num;
                                    string weight = CfgHelper.getCfgValue("max_gross_weight", "flight_model.cfg", "[WEIGHT_AND_BALANCE]");
                                    double num2;
                                    CfgHelper.setCfgValue(aircraftDirectory, val, Math.Max(5, (Double.TryParse(length, out num) && Double.TryParse(length, out num2) ? num * num2 / 666 : 5)).ToString(), "flight_model.cfg");
                                    message += val + " value was calculated from wing_span and max_gross_weight, you'll need to adjust it manually" + Environment.NewLine;
                                }

                                i++;
                            }
                        }
                    }
                }
                
                if (message != "")
                    MessageBox.Show(message, "", MessageBoxButton.OK);

            }

            if (i > 0)
                CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "flight_model.cfg" });

            SummaryUpdate();
        }

        private double[] parseContactPoint(string val)
        {
            // point.0 = 1,  43.00,   -0.05,  -9.70, 1600, 0, 1.442, 55.92, 0.6, 2.5, 0.9, 4.0, 4.0, 0, 220.0, 250.0 ; 
            //Console.WriteLine("Trying to parse point: " + val);
            string[] contactPoint = val.Split('=');
            if (contactPoint.Length >= 2)
            {
                string fsxNum = contactPoint[0].Replace("point.", "").Trim();
                string[] fsxData = contactPoint[1].Split(',');
                if (fsxData.Length >= 4)
                {
                    Regex digRregex = new Regex("[0-9.-]+");
                    if (digRregex.IsMatch(fsxNum) && fsxData[0].Replace(" ", "").Trim() == "1")
                    {
                        double[] msfsData = new double[fsxData.Length + 1];
                        int.TryParse(fsxNum.Contains('.') ? fsxNum.Trim('"').Split('.')[0] : fsxNum.Trim('"'), NumberStyles.Any, CultureInfo.InvariantCulture, out int numInt);
                        msfsData[0] = numInt;

                        int i = 0;
                        for (; i < fsxData.Length; i++)
                        {
                            string word = Regex.Replace(fsxData[i], "[^-0-9.]", "").TrimEnd('.'); ;
                            if (!word.Contains('.'))
                                word += ".0";

                            if (double.TryParse(word, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                                msfsData[i + 1] = num;
                            else
                            {
                                Console.WriteLine("Bad contact point coordinate: '" + fsxData[i].Replace(" ", "").Trim() + "'");
                                return new double[] { 1 };
                            }
                        }

                        return msfsData;
                    }
                }
            }

            return new double[] { };
        }

        private void FixContactsClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("flight_model.cfg"))
            {
                int i = 0;

                string lastPoint = null;
                foreach (StackPanel panel in FlightModelData.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if ((string)a.Content != "Toggle all")
                            {
                                string val = a.Content.ToString();

                                if (lastPoint == null)
                                {
                                    lastPoint = a.IsChecked == true ? val : "";
                                }
                                else
                                {
                                    // CALCULATE AVERAGE
                                    if (!String.IsNullOrEmpty(lastPoint) && a.IsChecked == true)
                                    {
                                        double[] testOne = parseContactPoint(lastPoint.ToLower().Replace(" ", "").Trim());
                                        double[] testTwo = parseContactPoint(val.ToLower().Replace(" ", "").Trim());

                                        if (testOne.Length >= 3 && testTwo.Length >= 3)
                                        {
                                            testTwo[2] = ((testOne[2] + testTwo[2]) / 2);
                                            testTwo[3] = ((testOne[3] + testTwo[3]) / 2);
                                            testTwo[4] = ((testOne[4] + testTwo[4]) / 2);

                                            string value = String.Join(", ", testTwo);
                                            int index = value.IndexOf(",");
                                            value = index >= 0 ? value.Substring(index + 1) : value;

                                            CfgHelper.setCfgValueStatus(aircraftDirectory, "point." + testOne[0], "flight_model.cfg", "[CONTACT_POINTS]", false);
                                            CfgHelper.setCfgValue(aircraftDirectory, "point." + testTwo[0], value, "flight_model.cfg", "[CONTACT_POINTS]");

                                            i++;
                                        }
                                    }

                                    lastPoint = null;
                                }
                            }
                        }
                    }
                }

                if (i > 0)
                    CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "flight_model.cfg" });
            }

            SummaryUpdate();
        }
        // FLIGHT MODEL END

        // GLOBAL SECTIONS START
        public void SummarySections()
        {
            List<StackPanel> parentPanelsList = new List<StackPanel>();
            parentPanelsList.Add(AircraftSections);
            parentPanelsList.Add(EnginesSections);
            parentPanelsList.Add(CockpitSections);
            parentPanelsList.Add(SystemsSections);
            parentPanelsList.Add(FlightModelSections);
            parentPanelsList.Add(RunwaySections);

            foreach (StackPanel parentPanel in parentPanelsList)
            {
                string filename = parentPanel.Tag.ToString();

                parentPanel.Children.Clear();

                if (CfgHelper.cfgFileExists(filename))
                {
                    //Console.WriteLine(filename + " " + CfgHelper.getSectionsList(aircraftDirectory, filename).Length);
                    int sectionsMissing = 0;
                    int requiredMissing = 0;
                    foreach (var secton in CfgHelper.getSectionsList(aircraftDirectory, filename))
                    {
                        if (!String.IsNullOrEmpty(secton) && !secton.Contains("VERSION"))
                        {
                            if (secton[0] == '-')
                            {
                                if (secton.Contains("[FUEL_QUANTITY]") || secton.Contains("[AIRSPEED]") || secton.Contains("[RPM]") ||
                                    secton.Contains("[THROTTLE_LEVELS]") || secton.Contains("[FLAPS_LEVELS]") ||
                                    secton.Contains("[CONTROLS.") /*|| secton.Contains("[FUELSYSTEM.")*/ || secton.Contains("[SIMVARS.") ||
                                    (secton.Contains("[PROPELLER]") || secton.Contains("[PISTON_ENGINE]")) && CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]") == "0" ||
                                    (secton.Contains("[PROPELLER]") || secton.Contains("[TURBOPROP_ENGINE]") || secton.Contains("[TURBINEENGINEDATA]")) && CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]") == "5" ||
                                    (secton.Contains("[TURBINEENGINEDATA]") || secton.Contains("[JET_ENGINE]")) && CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]") == "1"
                                    /*|| secton.Contains("ENGINE PARAMETERS.")*/
                                    )
                                {
                                    AddCheckBox(parentPanel, secton.Replace("-", ""), Colors.DarkRed, sectionsMissing++);
                                    requiredMissing++;
                                }
                                else
                                    AddCheckBox(parentPanel, secton.Replace("-", ""), Colors.DarkOrange, sectionsMissing++);
                            }
                            else
                            {
                                StackPanel myPanel = new StackPanel();
                                myPanel.Height = 16;
                                myPanel.VerticalAlignment = VerticalAlignment.Top;
                                myPanel.HorizontalAlignment = HorizontalAlignment.Left;

                                TextBlock myBlock = addTextBlock(secton, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen);
                                myBlock.Margin = new Thickness(20, 0, 0, 0);
                                myPanel.Children.Add(myBlock);
                                parentPanel.Children.Add(myPanel);
                            }
                        }
                    }

                    if (sectionsMissing > 0)
                    {
                        StackPanel myPanel = new StackPanel();
                        Button btn = new Button();
                        btn = SetButtonAtts(btn);
                        btn.Content = "Insert selected CFG sections";
                        btn.Click += InsertSectionsClick;
                        myPanel.Children.Add(btn);
                        parentPanel.Children.Add(myPanel);
                    }

                    foreach (var item in fsTabControl.Items)
                    {
                        if (((TabItem)item).Name == "tab" + parentPanel.Name.Replace("Sections", ""))
                        {
                            if (requiredMissing > 0)
                                ((TabItem)item).Foreground = new SolidColorBrush(Colors.DarkRed);
                            else if (((TabItem)item).Foreground.ToString() == "#FF000000")
                                ((TabItem)item).Foreground = new SolidColorBrush(Colors.DarkGreen);

                            break;
                        }
                    }
                }
            }
        }

        private void InsertSectionsClick(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            StackPanel myPanel = (StackPanel)button.Parent;
            StackPanel parentPanel = (StackPanel)myPanel.Parent;

            if (parentPanel != null)
            {
                string filename = parentPanel.Tag.ToString();

                string[] sections = new string[100];
                int i = 0;

                foreach (StackPanel panel in parentPanel.Children)
                {
                    if (panel.Children.Count > 0)
                    {
                        CheckBox b = new CheckBox();
                        if (panel.Children[0].GetType() == b.GetType())
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if (a.IsChecked == true && (string)a.Content != "Toggle all")
                            {
                                sections[i] = a.Content.ToString();
                                i++;
                            }
                        }
                    }
                }

                if (i > 0)
                {
                    MessageBoxResult messageBoxResult = MessageBox.Show("All variables will have default values, it may affect aircraft behaviour in the game" + Environment.NewLine + "Press YES to insert and activate default values" + Environment.NewLine + "Press NO to insert and deactivate default values" + Environment.NewLine + "Press CANCEL to abort insertion", i + " section" + (i > 1 ? "s" : "") + " will be inserted into " + filename, System.Windows.MessageBoxButton.YesNoCancel);
                    if (messageBoxResult != MessageBoxResult.Cancel)
                    {
                        CfgHelper.insertSections(aircraftDirectory, filename, sections, messageBoxResult == MessageBoxResult.Yes);
                        JSONHelper.scanTargetFolder(projectDirectory);
                        SummaryUpdate();
                    }
                }
            }
        }
        // GLOBAL SECTIONS END

        // TEXTURES START
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

                            if (System.IO.Path.GetFileName(fileName)[0] != '.')
                                TexturesList = AddCheckBox(TexturesList, fileName, Colors.DarkRed, texturesToConvert++);
                        }
                    }
                }
            }

            StackPanel myPanel2 = new StackPanel();

            Button btn = new Button();
            btn = SetButtonAtts(btn);
            btn.Name = "convertTexturesButon";

            if (texturesToConvert > 0)
            {
                btn.Content = "BMP to DDS by ImageTools";
                btn.Click += ConvertTexturesClick;

                Button btn2 = new Button();
                btn2 = SetButtonAtts(btn2);
                btn2.Content = "BMP to DDS by nvdxt";
                btn2.Click += ConvertTexturesClick;

                tabTextures.Foreground = new SolidColorBrush(Colors.DarkRed);
                myPanel2.Children.Add(btn2);
            }
            else
            {
                btn.Content = "No textures issues";
                btn.IsEnabled = false;
                tabTextures.Foreground = new SolidColorBrush(Colors.DarkGreen);
            }

            myPanel2.Children.Add(btn);
            TexturesList.Children.Add(myPanel2);
        }

        private void ConvertTexturesClick(object sender, RoutedEventArgs e)
        {
            CheckBox tmp = new CheckBox();

            // COUNT
            int count = 0;
            int converted = 0;
            string[] bmp = new string[1000];
            string[] dds = new string[1000];

            foreach (StackPanel panel in TexturesList.Children)
                if (panel.Children.Count > 0 && panel.Children[0].GetType() == tmp.GetType())
                {
                    CheckBox a = (CheckBox)panel.Children[0];
                    if (a.IsChecked == true && (string)a.Content != "Toggle all")
                    {
                        bmp[count] = projectDirectory + a.Content.ToString().ToLower();
                        dds[count] = projectDirectory + a.Content.ToString().ToLower().Replace("bmp", "dds");

                        count++;
                    }
                }

            for (int i = 0; i < count; i++)
            {
                if (File.Exists(dds[i]))
                {
                    try { File.Delete(dds[i]); } catch (Exception) { }
                }

                if (!File.Exists(dds[i]))
                {
                    Process process = new Process();
                    process.StartInfo.FileName = "cmd.exe";

                    Button button = (Button)sender;

                    if (button.Content.ToString().Contains("ImageTool"))
                        Process.Start(AppDomain.CurrentDomain.BaseDirectory + "ImageTool.exe", "-nogui -dds -dxt5 -32 -nostop -o \"" + dds[i] + "\" \"" + bmp[i] + "\"");
                    else
                        Process.Start(AppDomain.CurrentDomain.BaseDirectory + "nvdxt.exe", "-dxt5 -quality_highest -flip -file \"" + bmp[i] + "\" -output \"" + dds[i] + "\"");

                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    while (true)
                    {
                        if (sw.ElapsedMilliseconds > 5000 || File.Exists(dds[i])) break;
                    }

                    if (File.Exists(dds[i]))
                    {
                        File.Move(System.IO.Path.GetDirectoryName(bmp[i]) + "\\" + System.IO.Path.GetFileName(bmp[i]),
                            System.IO.Path.GetDirectoryName(bmp[i]) + "\\." + System.IO.Path.GetFileName(bmp[i]));
                    }

                    converted++;

                    // SET PROGRESS
                    /*TexturesList.Children.Add(new Rectangle
                    {
                        Width = ((Panel)Application.Current.MainWindow.Content).ActualHeight / count,
                        Height = 10,
                        StrokeThickness = 1,
                        Stroke = new SolidColorBrush(Colors.Black),
                        Margin = new Thickness(0)
                    });*/
                }
            }

            JSONHelper.scanTargetFolder(projectDirectory);
            SummaryUpdate();
        }
        // TEXTURES END

        // MODELS START
        public void SummaryModels()
        {
            ModelsList.Children.Clear();

            int modelsToConvert = 0;
            int modelsTotal = 0;

            if (aircraftDirectory != "")
            {
                foreach (var subdir in Directory.GetDirectories(aircraftDirectory))
                {
                    string folderName = subdir.Split('\\').Last().ToLower().Trim();
                    if (folderName[0] != '.' && folderName.Contains("model"))
                    {
                        var modelFiles = Directory.EnumerateFiles(subdir, "*_interior.mdl", SearchOption.TopDirectoryOnly);

                        foreach (string currentFile in modelFiles)
                        {
                            string fileName = currentFile.Replace(aircraftDirectory, "").Trim('\\');
                            ModelBackupButton.Tag = fileName;

                            if (Path.GetFileName(fileName)[0] != '.')
                            {
                                if (!File.Exists(Path.GetDirectoryName(currentFile) + "\\." + Path.GetFileName(currentFile)))
                                    modelsToConvert++;

                                string contents = File.ReadAllText(currentFile);
                                if (contents.Contains("MREC"))
                                    ModelsList = AddCheckBox(ModelsList, fileName, modelsToConvert > 0 ? Colors.DarkRed : Colors.Black, modelsTotal++);
                            }
                        }
                    }
                }
            }

            StackPanel myPanel2 = new StackPanel();

            Button btn = new Button();
            btn = SetButtonAtts(btn);
            btn.Name = "removeModelSwitches";

            if (modelsToConvert > 0)
            {
                btn.Content = "Remove interior clickable switches";
                btn.Click += RemoveSwitchesClick;

                if (modelsToConvert > 0)
                    tabModel.Foreground = new SolidColorBrush(Colors.DarkRed);
            }
            else
            {
                btn.Content = modelsTotal > 0 ? "No models with clickable switches" : "No interior models found";
                btn.IsEnabled = false;
            }

            myPanel2.Children.Add(btn);
            ModelsList.Children.Add(myPanel2);
        } 

        private void RemoveSwitchesClick(object sender, RoutedEventArgs e)
        {
            CheckBox tmp = new CheckBox();

            // COUNT
            foreach (StackPanel panel in ModelsList.Children)
                if (panel.Children.Count > 0 && panel.Children[0].GetType() == tmp.GetType())
                {
                    CheckBox a = (CheckBox)panel.Children[0];
                    if (a.IsChecked == true && (string)a.Content != "Toggle all")
                    {
                        string mainFile = aircraftDirectory + "\\" + (string)a.Content;

                        if (File.Exists(mainFile))
                        {
                            byte[] cache = new byte[4];
                            byte[] MDLDsize = new byte[4];
                            int MDLDsizeInt = -1;
                            byte[] MRECsize = new byte[4];
                            int MRECsizeInt = -1;
                            int MDLDpos = -1;
                            int MRECpos = -1;


                            byte[] buf = File.ReadAllBytes(mainFile);
                            for (int i = 0; i < buf.Length; i++)
                            {
                                for (int k = 0; k < cache.Length - 1; k++) {
                                    cache[k] = cache[k+1];
                                }
                                cache[cache.Length - 1] = buf[i];

                                System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                                string s = enc.GetString(cache);

                                if (s == "MDLD")
                                    MDLDpos = i - 3;
                                else if (s == "MREC")
                                    MRECpos = i - 3;

                                if (MDLDpos > 0 && i == MDLDpos + 7) // CAPTURE MDLD SIZE
                                {
                                    if (BitConverter.IsLittleEndian)
                                        Array.Reverse(cache);
                                    MDLDsizeInt = cache[3] | (cache[2] << 8) | (cache[1] << 16) | (cache[0] << 24);
                                    Console.WriteLine(BitConverter.ToString(cache));

                                }
                                else if (MRECpos > 0 && i == MRECpos + 7) // CAPTURE MREC SIZE
                                {
                                    if (BitConverter.IsLittleEndian)
                                        Array.Reverse(cache);
                                    MRECsizeInt = cache[3] | (cache[2] << 8) | (cache[1] << 16) | (cache[0] << 24);
                                    Console.WriteLine(BitConverter.ToString(cache));
                                }
                                else if (MRECpos > 0 && MRECsizeInt > 0 && i < MRECpos + MRECsizeInt + 7) // FILL MREC WITH ZEROES
                                {
                                    buf[i] = 0x00;
                                }
                            }

                            if (MRECpos > 0 && MRECsizeInt > 0)
                            {
                                // MAKE MDL BACKUP
                                backUpFile(mainFile);

                                // CLEAR AIRCRAFT CACHE
                                deleteCVCfolder();

                                Console.WriteLine("MDLD pos" + MDLDpos + " lng" + MDLDsizeInt + "; MREC pos" + MRECpos + " lng" + MRECsizeInt);
                                File.WriteAllBytes(mainFile, buf);
                            } else {
                                MessageBox.Show("Clickable switches removal from " + a.Content + " failed");
                            }
                        }

                    }
                }


            SummaryUpdate();
        }
        // MODELS END

        public string Get_aircraft_directory()
        {
            if (Directory.Exists(projectDirectory + @"\SimObjects\Airplanes\"))
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
            } else
            {
                MessageBox.Show("Directory not found " + projectDirectory + @"\SimObjects\Airplanes\");
                return "";
            }
        }

        public Button SetButtonAtts(Button btn)
        {
            btn.MinHeight = 30;
            btn.FontSize = 20;
            btn.Margin = new Thickness(5, 20, 5, 20);
            btn.FontFamily = new FontFamily("Arial Black");
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            btn.VerticalAlignment = VerticalAlignment.Top;

            return btn;
        }

        private void CfgBackupClick(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            string filename = btn.Tag.ToString();
            string mainFile = aircraftDirectory + "\\" + filename;
            string backupFile = Path.GetDirectoryName(mainFile) + "\\." + Path.GetFileName(mainFile);

            if (File.Exists(backupFile))
            {
                MessageBoxResult messageBoxResult = MessageBox.Show("All changes in " + filename + " made since backup will be erased", "You are going to restore " + filename, System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;

                    try
                    {
                        File.Delete(mainFile);
                        File.Copy(backupFile, mainFile);
                    } catch(Exception)
                    {

                    }
                    CfgHelper.processCfgfiles(aircraftDirectory + "\\", true);
                    SummaryUpdate();

                    JSONHelper.scanTargetFolder(projectDirectory);
                }
            }
            else if (File.Exists(mainFile))
            {
                CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                File.Copy(mainFile, backupFile);
                btn.Content = "Restore backup";
            }
        }
        private void CfgEditClick(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            string filename = btn.Tag.ToString();

            if (File.Exists(aircraftDirectory + "\\" + filename))
            {
                Process.Start(aircraftDirectory + "\\" + filename);
            }
        }

        public TextBlock addTextBlock(string text, HorizontalAlignment ha, VerticalAlignment va, Color clr)
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
            string defaultPath = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            dialog.InitialDirectory = defaultPath;
            dialog.IsFolderPicker = true;
            dialog.RestoreDirectory = (String.IsNullOrEmpty(defaultPath) || defaultPath == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) ? true : false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                TargetFolder = dialog.FileName + "\\";
                btnTargetFolderPath.Text = "into " + TargetFolder + PackageDir.Text.ToLower().Trim() + "\\";
            }
        }

        private void TextBlockTargetFile_Input(object sender, RoutedEventArgs e)
        {
            if (TargetFolder != "")
            {
                btnTargetFolderPath.Text = "into " + TargetFolder + PackageDir.Text.ToLower().Trim() + "\\";
            }
        }

        private void BtnOpenSourceFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            string defaultPath = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\microsoft games\\Flight Simulator\\10.0", "SetupPath",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            dialog.InitialDirectory = defaultPath;
            dialog.IsFolderPicker = true;
            dialog.RestoreDirectory = (String.IsNullOrEmpty(defaultPath) || defaultPath == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) ? true : false;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (File.Exists(dialog.FileName + "\\aircraft.cfg"))
                {
                    SourceFolder = dialog.FileName + "\\";
                    btnSourceFolderPath.Text = "from " + SourceFolder;
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
                MessageBox.Show("You have to select both source (FSX) ans destination (MSFS) folders");
            else if (String.IsNullOrWhiteSpace(PackageTitle.Text) || String.IsNullOrWhiteSpace(PackageDir.Text) || String.IsNullOrWhiteSpace(PackageManufacturer.Text) || String.IsNullOrWhiteSpace(PackageAuthor.Text) ||
                String.IsNullOrWhiteSpace(PackageVer1.Text) || String.IsNullOrWhiteSpace(PackageVer2.Text) || String.IsNullOrWhiteSpace(PackageVer3.Text) ||
                String.IsNullOrWhiteSpace(PackageMinVer1.Text) || String.IsNullOrWhiteSpace(PackageMinVer2.Text) || String.IsNullOrWhiteSpace(PackageMinVer3.Text))
                MessageBox.Show("You have to fill in all fields");
            else if (Directory.Exists(TargetFolder + PackageDir.Text.ToLower().Trim() + "\\"))
            {
                MessageBox.Show("Aircraft already exists in folder " + TargetFolder + PackageDir.Text);
            } else if (SourceFolder == TargetFolder + PackageDir.Text.ToLower().Trim() + "\\")
            {
                MessageBox.Show("You can't set same forlder for source and destination");
            }
            else
            {
                string[] data = new string[] { "", "AIRCRAFT", PackageTitle.Text, PackageManufacturer.Text, PackageAuthor.Text,
            PackageVer1.Text + "." + PackageVer2.Text + "." + PackageVer3.Text, PackageMinVer1.Text + "." + PackageMinVer2.Text + "." + PackageMinVer2.Text, "" };

                JSONHelper.createManifest(this, SourceFolder, TargetFolder + PackageDir.Text.ToLower().Trim() + "\\", data);
            }
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

        public StackPanel AddCheckBox(StackPanel mainPanel, string content, Color color, int index = 1, string tag = "")
        {
            StackPanel myPanel = new StackPanel();
            myPanel.Height = 17;
            myPanel.VerticalAlignment = VerticalAlignment.Top;

            // ADD TOGGLE CHECKBOX
            if (index == 0)
            {
                StackPanel myPanel2 = new StackPanel();
                myPanel2.Height = myPanel.Height;
                myPanel2.VerticalAlignment = myPanel.VerticalAlignment;
                myPanel2.Margin = new Thickness(0, 0, 0, 5);

                CheckBox ToggleCheckBox = new CheckBox();
                ToggleCheckBox.Content = "Toggle all";
                ToggleCheckBox.MaxWidth = 600;
                ToggleCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
                ToggleCheckBox.Foreground = new SolidColorBrush(Colors.Black);
                ToggleCheckBox.Click += toggleCheckboxes;
                myPanel2.Children.Add(ToggleCheckBox);
                mainPanel.Children.Insert(0,myPanel2);
            }

            CheckBox checkBox = new CheckBox();
            checkBox.Content = content;
            checkBox.MaxWidth = 600;
            checkBox.HorizontalAlignment = HorizontalAlignment.Left;
            checkBox.Foreground = new SolidColorBrush(color);
            if (tag != "")
                checkBox.Tag = tag;
            myPanel.Children.Add(checkBox);
            mainPanel.Children.Add(myPanel);

            return mainPanel;
        }

        private void toggleCheckboxes(object sender, RoutedEventArgs e)
        {
            CheckBox ToggleCheckBox = (CheckBox)sender;
            StackPanel myPanel = (StackPanel) ToggleCheckBox.Parent;
            StackPanel parentPanel = (StackPanel)myPanel.Parent;

            if (parentPanel.Children.Count > 0)
            {
                bool state = false;
                int i = 0;

                foreach (var tmpPanel in parentPanel.Children)
                {
                    StackPanel tmp = new StackPanel();
                    if (tmpPanel.GetType() != tmp.GetType())
                        continue;

                    StackPanel panel = (StackPanel)tmpPanel;

                    if (panel.Children.Count > 0)
                    {
                        foreach (var checkBox in panel.Children)
                        {
                            CheckBox b = new CheckBox();
                            if (checkBox.GetType() == b.GetType())
                            {
                                CheckBox thisCheckBox = (CheckBox)checkBox;
                                if (i <= 0 && thisCheckBox.IsChecked == true)
                                    state = true;
                                else
                                    thisCheckBox.IsChecked = state;
                                i++;
                            }
                        }
                    }
                }
            }
        }

        public void deleteCVCfolder()
        {
            if (projectDirectory != "")
            {
                if (Directory.Exists(projectDirectory.TrimEnd('\\') + "_CVT_"))
                {
                    try
                    {
                        Directory.Delete(projectDirectory.TrimEnd('\\') + "_CVT_", true);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("CVT folder removal is failed");
                    }
                }
            }
        }

        public void backUpFile(string mainFile, bool force = false)
        {
            string backupFile = Path.GetDirectoryName(mainFile) + "\\." + Path.GetFileName(mainFile);
            CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;

            if (File.Exists(backupFile) && force)
            {
                try
                {
                    File.Delete(backupFile);
                } catch (Exception)
                {
                    MessageBox.Show("Backup creation is failed");
                }
            }

            if (!File.Exists(backupFile)) {
                File.Copy(mainFile, backupFile);
            }
        }

        // UPDATES START
        public async System.Threading.Tasks.Task CheckUpdateAsync()
        {
            string pubVer = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            {
                var client = new HttpClient();
                string data = await client.GetStringAsync(updatedirectory);

                //Console.WriteLine(data);

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
                            //Console.WriteLine(ver + " " + pubVer);
                        }
                    }
                }

                Button btn = null;
                Button btn2 = null;
                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = addTextBlock("", HorizontalAlignment.Center, VerticalAlignment.Top, Colors.DarkGreen);

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
                    myBlock.Text = "You are using latest program version (" + pubVer + ")";
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

        public void UpdateAutomaticallyClick(object sender, RoutedEventArgs e)
        {
            if (updateURL != "")
            {
                WebClient _webClient = new WebClient();
                //_webClient.DownloadProgressChanged += OnDownloadProgressChanged;
                _webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(OnDownloadCompleted);

                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = addTextBlock("Applying update ver" + updateVersion, HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black);
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
                {
                    try {
                        File.Delete(EXE_PATH + ".BAK");
                    }
                    catch(Exception)
                    {
                        MessageBox.Show("Update failed");
                    }
                }

                if (!File.Exists(EXE_PATH + ".BAK"))
                {
                    File.Move(EXE_PATH, EXE_PATH + ".BAK");
                    Extract extract = new Extract();
                    extract.Run(TEMP_FILE, EXE_PATH);
                }
            }
        }

        public void SetUpdateReady()
        {
            if (File.Exists(EXE_PATH))
            {
                Process.Start(EXE_PATH);
                Environment.Exit(0);

                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = addTextBlock("Update failed", HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black);
                myPanel.Children.Add(myBlock);
                AboutContent.Children.Add(myPanel);
            }
        }
        // UPDATES END
    }

}
