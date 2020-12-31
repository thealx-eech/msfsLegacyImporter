using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Net.Http;
using System.Globalization;
using Microsoft.Deployment.Compression.Cab;
using System.Windows.Controls.Primitives;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace msfsLegacyImporter
{
    public partial class MainWindow : Window
    {
        public string EXE_PATH = "msfsLegacyImporter.exe";
        public string TEMP_FILE = "temp.zip";
        public string updatedirectory = "http://msfs.touching.cloud/legacyimporter/";

        public string updateVersion = "";
        public string updateURL = "";
        public string projectDirectory = "";
        public string aircraftDirectory = "";
        public string airFilename = "";
        public cfgHelper CfgHelper;
        private jsonHelper JSONHelper;
        private csvHelper CsvHelper;
        private xmlHelper XmlHelper;
        private fsxVarHelper FsxVarHelper;
        private fileDialogHelper FileDialogHelper;
        private float gammaSliderPos = 1;

        private string communityPath = "";
        //private bool extractingCabs = false;

        string SourceFolder = "";
        string TargetFolder = "";

        string currentMode = "";

        FileSystemWatcher fileTrackWatcher = null;

        public MainWindow()
        {
            CultureInfo customCulture = (CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            // REMOVE TEMP FILES
            try
            {
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + EXE_PATH + ".BAK");
                File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\" + TEMP_FILE);
            }
            catch { /*MessageBox.Show("Can't delete temporary files");*/ }

            // GET COMMUNITY PATH
            // MSFS 2020 Windows Store version: C: \Users\*USERNAME *\AppData\Local\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\UserCfg.opt
            // MSFS 2020 Steam version: C: \Users\*USERNAME *\AppData\Roaming\Microsoft Flight Simulator\UserCfg.opt
            try
            {
                string optPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\UserCfg.opt";

                if (!File.Exists(optPath))
                    optPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Microsoft Flight Simulator\UserCfg.opt";

                if (File.Exists(optPath))
                {
                    string content = File.ReadAllText(optPath);

                    Regex regex = new Regex("InstalledPackagesPath\\s+\"(.*)\"");
                    Match match = regex.Match(content);

                    if (match.Success && match.Groups.Count > 1 && Directory.Exists(match.Groups[1].Value + "\\Community\\"))
                    {
                        communityPath = match.Groups[1].Value + "\\Community\\";
                        Console.WriteLine("Community path found: " + communityPath);
                    }
                }

                if (String.IsNullOrEmpty(communityPath))
                    Console.WriteLine("Community path NOT found");
            }
            catch { 
            }

            InitializeComponent();

            JSONHelper = new jsonHelper();
            CfgHelper = new cfgHelper();
            CsvHelper = new csvHelper();
            XmlHelper = new xmlHelper();
            FsxVarHelper = new fsxVarHelper();
            FileDialogHelper = new fileDialogHelper();

            JSONHelper.loadSettings();

            // INIT LANGUAGES
            CsvHelper.initializeLanguages(LangSelector);
            LangSelector.DropDownClosed += new EventHandler(languageUpdated);

            // SETUP MAIN SCREEN
            mainScreen.Visibility = Visibility.Visible;
            fsTabControl.Visibility = Visibility.Collapsed;

            // STYLE BUTTONS
            setupStyles();

            // HIDE TABS
            int k = 0;
            foreach (TabItem item in fsTabControl.Items)
            {
                if (k > 0 && k < fsTabControl.Items.Count - 1)
                    item.Visibility = Visibility.Collapsed;
                k++;
            }

            // TRY TO LOAD CFGTPL FILES
            if (!CfgHelper.processCfgfiles(AppDomain.CurrentDomain.BaseDirectory + "\\cfgTpl\\"))
            {
                MessageBox.Show(CsvHelper.trans("init_cfg_tpl_missing"));
                Environment.Exit(1);
            }

            _ = CheckUpdateAsync();
        }

        private void setupStyles()
        {
            btnOpenFile = SetButtonAtts(btnOpenFile);
            btnSourceFolder = SetButtonAtts(btnSourceFolder);
            btnTargetFolder = SetButtonAtts(btnTargetFolder);
            btnScan = SetButtonAtts(btnScan);
            btnImportSubmit = SetButtonAtts(btnImportSubmit);
            TextExpressionButton = SetButtonAtts(TextExpressionButton);
            MainMenuButton = SetButtonAtts(MainMenuButton, false);

            load_imported_header = SetHeaderAtts(load_imported_header);
            load_imported_notice = SetHeaderAtts(load_imported_notice);
            update_layout_header = SetHeaderAtts(update_layout_header);
            new_import_header = SetHeaderAtts(new_import_header);
            new_import_notice = SetHeaderAtts(new_import_notice);
            engines_power = SetHeaderAtts(engines_power);
            about_links_header = SetHeaderAtts(about_links_header);
            about_colored_header = SetHeaderAtts(about_colored_header);
            about_colored_green = SetHeaderAtts(about_colored_green);
            about_colored_orange = SetHeaderAtts(about_colored_orange);
            about_colored_red = SetHeaderAtts(about_colored_red);
            about_translation_header = SetHeaderAtts(about_translation_header);
            imageLeftTooltip = SetHeaderAtts(imageLeftTooltip);
            imageRightTooltip = SetHeaderAtts(imageRightTooltip);
        }
        private void languageUpdated(object sender, System.EventArgs e)
        {
            CsvHelper.languageUpdate(((ComboBoxItem)(sender as ComboBox).SelectedItem).Content.ToString());
            setupStyles();
            SummaryUpdate(true);
        }

        // FSX IMPORT
        private void mainImportClick(object sender, RoutedEventArgs e)
        {
            showInitPage("import");
        }

        // MSFS LOAD
        private void mainLoadClick(object sender, RoutedEventArgs e)
        {
            showInitPage("load");
        }

        private void ShowMainMenu(object sender, RoutedEventArgs e)
        {
            showInitPage("main");
        }

        private void showInitPage(string state)
        {
            mainScreen.Visibility = Visibility.Collapsed;
            fsTabControl.Visibility = Visibility.Collapsed;
            LoadAircraft.Visibility = Visibility.Collapsed;
            RescanFiles.Visibility = Visibility.Collapsed;
            ImportForm.Visibility = Visibility.Collapsed;
            AircraftProcess.Visibility = Visibility.Collapsed;

            switch (state)
            {
                case "load":
                    mainScreen.Visibility = Visibility.Collapsed;
                    fsTabControl.Visibility = Visibility.Visible;
                    LoadAircraft.Visibility = Visibility.Visible;
                    break;
                case "import":
                    mainScreen.Visibility = Visibility.Collapsed;
                    fsTabControl.Visibility = Visibility.Visible;
                    ImportForm.Visibility = Visibility.Visible;
                    break;
                case "loaded":
                case "imported":
                    fsTabControl.Visibility = Visibility.Visible;
                    RescanFiles.Visibility = Visibility.Visible;
                    AircraftProcess.Visibility = Visibility.Visible;
                    break;
                case "main":
                    mainScreen.Visibility = Visibility.Visible;

                    currentMode = "";
                    aircraftDirectory = "";
                    CfgHelper.resetCfgfiles();
                    SummaryUpdate(true);
                    break;
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                string defaultPath = !String.IsNullOrEmpty(communityPath) ? communityPath : HKLMRegistryHelper.GetRegistryValue("SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath");
                dialog.InitialDirectory = defaultPath;
                dialog.IsFolderPicker = true;
                dialog.RestoreDirectory = (String.IsNullOrEmpty(defaultPath) || defaultPath == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) ? true : false;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    setAircraftDirectory(dialog.FileName);
                    
                    if (aircraftDirectory != "")
                        showInitPage("loaded");
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        public void setAircraftDirectory(string directory)
        {
            if (File.Exists(directory + "\\layout.json") &&
                File.Exists(directory + "\\manifest.json") &&
                Directory.Exists(directory + "\\SimObjects"))
            {
                if (String.IsNullOrEmpty(communityPath))
                {
                    try { Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath", System.IO.Path.GetDirectoryName(directory)); }
                    catch (Exception ex) {  Console.WriteLine(ex.Message); }
                }

                // CLEAN FIELDS
                btnSourceFolderPath.Text = "";
                PackageTitle.Text = "";
                PackageDir.Text = "";
                PackageManufacturer.Text = "";
                PackageAuthor.Text = "";
                //extractingCabs = false;

                projectDirectory = directory;
                aircraftDirectory = Get_aircraft_directory();

                if (aircraftDirectory != "")
                {
                    LoadedHeader.Text = CsvHelper.trans("init_curr_aircraft") + ": " + new DirectoryInfo(projectDirectory).Name;
                    //btnOpenFile.Content = "Select another aircraft";

                    btnOpenFilePath.Text = projectDirectory;

                    backUpFile(aircraftDirectory + "\\aircraft.cfg");
                    CfgHelper.processCfgfiles(aircraftDirectory + "\\", true);

                    // TRACK CFG FILES
                    if (fileTrackWatcher != null)
                        fileTrackWatcher.Dispose();
                    fileTrackWatcher = new FileSystemWatcher();

                    fileTrackWatcher.Filter = "*.cfg";
                    fileTrackWatcher.Changed += new FileSystemEventHandler(trackCfgEdit);
                    fileTrackWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    fileTrackWatcher.IncludeSubdirectories = true;
                    fileTrackWatcher.Path = aircraftDirectory + "\\";
                    fileTrackWatcher.EnableRaisingEvents = true;

                    LoadLabel.Text = CsvHelper.trans("init_tabs_click");
                    LoadLabel.Foreground = new SolidColorBrush(Colors.Black);

                    SummaryUpdate();

                    // START BACKGROUND CABS EXTRACTION
                    /*if (!string.IsNullOrEmpty(SourceFolder) && (Directory.Exists(SourceFolder + "..\\..\\..\\Gauges") || Directory.Exists(SourceFolder + "..\\..\\..\\SimObjects")))
                    {
                        Console.WriteLine("Trying to extract FSX cabs from " + SourceFolder + "..\\..\\..\\");
                        fsTabControl.IsEnabled = false;
                        extractingCabs = true;
                        extractDefaultCabsAsync(btnScan, SourceFolder + "..\\..\\..\\");
                    }*/
                }

                // CLEAN FIELDS
                SourceFolder = "";
            }
            else
            {
                MessageBox.Show(string.Format(CsvHelper.trans("init_simobject_missing"), directory));
            }
        }

        public void trackCfgEdit(object sender, FileSystemEventArgs e)
        {
            if (DateTime.UtcNow.Ticks - CfgHelper.lastChangeTimestamp > 10000000)
            {
                Dispatcher.Invoke(() => fsTabControl.IsEnabled = false);
                MessageBoxResult messageBoxResult = MessageBox.Show(string.Format(CsvHelper.trans("file_edited_outside"), System.IO.Path.GetFileName(e.FullPath)), CsvHelper.trans("file_edited"), MessageBoxButton.YesNo);
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

        private void SummaryUpdate(bool reset = false)
        {
            // RESET TABS COLOR
            foreach (var item in fsTabControl.Items)
                ((TabItem)item).Foreground = new SolidColorBrush(Colors.Black);

            if (!reset)
            {
                SummaryAircraft();
                SummaryEngines();
                SummarySections();
                SummarySystems();
                SummaryFlightModel();
                SummaryTextures();
                SummaryModels();
                SummarySound();
                SummaryPanel();
            }

            foreach (TabItem item in fsTabControl.Items)
            {
                if (!reset)
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
                            if (CfgHelper.cfgFileExists("systems.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
                                item.Visibility = Visibility.Visible;
                            else
                                item.Visibility = Visibility.Collapsed;
                            break;
                        case "tabFlightModel":
                            if (CfgHelper.cfgFileExists("flight_model.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
                                item.Visibility = Visibility.Visible;
                            else
                                item.Visibility = Visibility.Collapsed;
                            break;
                        case "tabEngines":
                            if (CfgHelper.cfgFileExists("engines.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
                                item.Visibility = Visibility.Visible;
                            else
                                item.Visibility = Visibility.Collapsed;
                            break;
                        case "tabRunway":
                            if (CfgHelper.cfgFileExists("engines.cfg") || CfgHelper.cfgFileExists("aircraft.cfg") /*&& CfgHelper.cfgFileExists("flight_model.cfg")*/)
                                item.Visibility = Visibility.Visible;
                            else
                                item.Visibility = Visibility.Collapsed;
                            break;
                        case "tabPanel":
                            if (aircraftDirectory != "" && Directory.Exists(aircraftDirectory) && Directory.EnumerateFiles(aircraftDirectory, "panel.cfg", SearchOption.AllDirectories).Count() > 0)
                                item.Visibility = Visibility.Visible;
                            else
                                item.Visibility = Visibility.Collapsed;
                            break;

                        default:
                            item.Visibility = Visibility.Visible;
                            break;
                    }
                } else if (item.Name != "tabInit" && item.Name != "tabAbout")
                    item.Visibility = Visibility.Collapsed;
            }

            // SET BACKUP BUTTONS LABEL
            foreach (Button btn in new Button[] { AircraftBackupButton, EnginesBackupButton, CockpitBackupButton, SystemsBackupButton, FlightModelBackupButton, RunwayBackupButton, RunwayBackupButton, ModelBackupButton, PanelBackupButton })
            {
                if (!reset)
                {
                    btn.Visibility = Visibility.Visible;

                    SetButtonAtts(btn, false);

                    if (!String.IsNullOrEmpty(btn.Tag.ToString()))
                    {
                        string filenames = btn.Tag.ToString();
                        if (!btn.Tag.ToString().Contains(','))
                            filenames += ',';

                        bool backup_not_found = false;
                        foreach (var filename in filenames.Split(','))
                        {
                            if (!String.IsNullOrEmpty(filename))
                            {
                                if (CfgHelper.cfgFileExists(filename) ||
                                    !(new string[] { "aircraft.cfg", "cameras.cfg", "cockpit.cfg", "engines.cfg", "flight_model.cfg", "gameplay.cfg", "systems.cfg" }).Contains(filename))
                                {
                                    string mainFile = aircraftDirectory + "\\" + filename;
                                    string backupFile = System.IO.Path.GetDirectoryName(mainFile) + "\\." + System.IO.Path.GetFileName(mainFile);

                                    if (!File.Exists(backupFile))
                                    {
                                        backup_not_found = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    btn.Visibility = Visibility.Hidden;
                                }
                            }
                        }

                        if (FindName(btn.Name) != null) {
                            if (!backup_not_found)
                            {
                                ((Button)FindName(btn.Name)).Content = CsvHelper.trans("restore_backup");
                                Console.WriteLine("Backup of " + btn.Tag.ToString() + " FOUND");
                            }
                            else
                            {
                                ((Button)FindName(btn.Name)).Content = CsvHelper.trans("make_backup");
                                Console.WriteLine("Backup of " + btn.Tag.ToString() + " NOT FOUND");
                            }
                        }
                    }
                } else
                    btn.Visibility = Visibility.Hidden;
            }

            foreach (Button btn in new Button[] { AircraftEditButton, EnginesEditButton, CockpitEditButton, CockpitEditButton, SystemsEditButton, FlightModelEditButton, RunwayEditButton })
            {
                if (!reset)
                {
                    btn.Visibility = Visibility.Visible;

                    if (!String.IsNullOrEmpty(btn.Tag.ToString()))
                    {
                        string filenames = btn.Tag.ToString();
                        if (!btn.Tag.ToString().Contains(','))
                            filenames += ',';

                        foreach (var filename in filenames.Split(','))
                            if (!String.IsNullOrEmpty(filename) && !CfgHelper.cfgFileExists(filename))
                                btn.Visibility = Visibility.Hidden;
                    }

                    SetButtonAtts(btn, false);
                } else
                    btn.Visibility = Visibility.Hidden;
            }

            // UPDATE TITLE
            if (!reset)
                this.Title = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title + " " + 
                Assembly.GetExecutingAssembly().GetName().Version.ToString() + (currentMode != "" ? " (" + currentMode + ")" : "");
            else
                this.Title = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false)).Title + " " +
                Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        // AIRCRAFT START
        public void SummaryAircraft()
        {
            int status = 2;
            TabPanel myPanel = new TabPanel();

            // AIRCRAFT.CFG SPLIT
            AircraftProcess.Children.Clear();
            tabAircraft.Foreground = new SolidColorBrush(Colors.DarkGreen);

            if (aircraftDirectory != "" && File.Exists(aircraftDirectory + @"\aircraft.cfg"))
            {
                List<string> presentedFiles = new List<string>();
                List<TextBlock> presentedFilesLabels = new List<TextBlock>();

                foreach (var file in new[] { "aircraft.cfg", "cameras.cfg", "cockpit.cfg", "engines.cfg", "flight_model.cfg", "gameplay.cfg", "systems.cfg", ".unknown.cfg" })
                {
                    TextBlock myBlock = null;
                    if (file == ".unknown.cfg" && File.Exists(aircraftDirectory + "\\" + file))
                    {
                        myBlock = addTextBlock(file + ": "+ CsvHelper.trans("init_cfg_ignored"), HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkOrange, new Thickness(0));
                    }
                    else if (file == ".unknown.cfg")
                    {
                        myBlock = addTextBlock("", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkOrange, new Thickness(0));
                    }
                    else if (!File.Exists(aircraftDirectory + "\\" + file))
                    {
                        myBlock = addTextBlock(file + ": "+ CsvHelper.trans("init_cfg_missing"), HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkRed, new Thickness(0));
                        status = 0;
                    }
                    else
                    {
                        myBlock = addTextBlock(file + ": " + CsvHelper.trans("init_cfg_loaded"), HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen, new Thickness(0));
                        presentedFiles.Add(file);
                    }

                    presentedFilesLabels.Add(myBlock);
                }

                TextBlock modeHeader = addTextBlock("", HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 0), TextWrapping.Wrap);
                modeHeader.FontSize = 21;
                TextBlock modeDescr = addTextBlock("", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 0), TextWrapping.Wrap);
                modeDescr.FontSize = 14;
                TextBlock modeDescr2 = addTextBlock("", HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 0), TextWrapping.Wrap);
                modeDescr2.FontSize = 14;

                if (presentedFiles.Count == 1 && presentedFiles.Contains("aircraft.cfg"))
                {
                    currentMode = CsvHelper.trans("init_basic_mode");
                    modeHeader.Text = currentMode + " " + CsvHelper.trans("init_mode_active");
                    AircraftProcess.Children.Add(modeHeader);

                    modeDescr.Text =
                    CsvHelper.trans("init_basic_notice1") + Environment.NewLine +
                    CsvHelper.trans("init_basic_notice2") + Environment.NewLine +
                    CsvHelper.trans("init_basic_notice3") + Environment.NewLine + Environment.NewLine +
                    CsvHelper.trans("init_basic_notice4") + Environment.NewLine +
                    CsvHelper.trans("init_basic_notice5") + Environment.NewLine;
                    myPanel.Children.Add(modeDescr);
                }
                else
                {
                    currentMode = CsvHelper.trans("init_full_mode");
                    modeHeader.Text = currentMode + " " + CsvHelper.trans("init_mode_active");
                    AircraftProcess.Children.Add(modeHeader);

                    modeDescr.Text =
                    CsvHelper.trans("init_full_notice1") + Environment.NewLine +
                    CsvHelper.trans("init_full_notice2") + Environment.NewLine;
                    modeDescr.Width = 460;
                    myPanel.Children.Add(modeDescr);

                    StackPanel cfgsList = new StackPanel();
                    cfgsList.Width = 150;
                    cfgsList.Margin = new Thickness(0, 20, 0, 0);
                    cfgsList.HorizontalAlignment = HorizontalAlignment.Right;
                    foreach (var presentedFilesLabel in presentedFilesLabels)
                        cfgsList.Children.Add(presentedFilesLabel);
                    myPanel.Children.Add(cfgsList);

                    modeDescr2.Text = CsvHelper.trans("init_full_notice3") + Environment.NewLine + CsvHelper.trans("init_full_notice4");
                    myPanel.Children.Add(modeDescr2);

                }


                // SHOW PROCESS INSTRUMENTS BUTTON
                Button btn = new Button();
                btn = SetButtonAtts(btn);
                btn.Click += BtnAircraftProcess_Click;

                if (presentedFiles.Count == 1 && presentedFiles.Contains("aircraft.cfg")) {
                    btn.Content = CsvHelper.trans("init_full_enable");
                    myPanel.Children.Add(btn);
                }
                else if (status == 0)
                {
                    btn.Content = CsvHelper.trans("init_parse_cfg");
                    myPanel.Children.Add(btn);
                }


                AircraftProcess.Children.Add(myPanel);
                AircraftProcess.Children.Add(sectiondivider());

                //AircraftContent.Text = @"List of CFG files:" + Environment.NewLine + Environment.NewLine + cfgsList;
                //tabAircraft.Foreground = new SolidColorBrush(status > 1 ? Colors.DarkGreen : Colors.DarkRed);
            }
            else
            {
                //AircraftContent.Text = "aircraft.cfg file not exists!";
                tabAircraft.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            // DESCRIPTION FIXES
            StackPanel myPanel2 = new StackPanel();
            AircraftPerformance.Children.Clear();

            if (CfgHelper.cfgFileExists("flight_model.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int i = 0;

                for (int k = 0; k <= 99; k++)
                {
                    string[] requiredValues = new string[] { "ui_certified_ceiling", "ui_max_range", "ui_autonomy", "cruise_speed", "ui_typerole" };

                    foreach (var requiredValue in requiredValues)
                    {
                        if (CfgHelper.cfgSectionExists("aircraft.cfg", "[FLTSIM." + k + "]"))
                        {
                            string value = CfgHelper.getCfgValue(requiredValue, "aircraft.cfg", "[FLTSIM." + k + "]").ToLower().Trim('"').Trim();
                            if (String.IsNullOrEmpty(value) || value == "0" ||
                                //!int.TryParse(value.Contains('.') ? value.Trim('"').Trim().Split('.')[0] : value.Trim('"').Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int num) ||
                                requiredValue == "ui_typerole" && (value == "glider" || value == "rotorcraft" ))
                            {
                                if (requiredValue == "ui_typerole")
                                {
                                    AircraftPerformance = AddGroupCheckBox(AircraftPerformance, "[FLTSIM." + k + "] " + requiredValue + " " + CsvHelper.trans("aircraft_parameter_invalid"), Colors.DarkRed, i++);
                                    tabAircraft.Foreground = new SolidColorBrush(Colors.DarkRed);
                                }
                                else
                                    AircraftPerformance = AddGroupCheckBox(AircraftPerformance, "[FLTSIM." + k + "] " + requiredValue + " " + CsvHelper.trans("aircraft_parameter_missing"), Colors.DarkOrange, i++);
                            }
                        }
                    }
                }

                Button btn = new Button();
                btn = SetButtonAtts(btn);
                btn.Content = CsvHelper.trans("aircraft_add_missing_parameters");
                btn.Click += AddDescriptionClick;
                myPanel2.Children.Add(btn);

                TextBlock notice = addTextBlock(CsvHelper.trans("aircraft_missing_parameter_notice"),
                   HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                AircraftPerformance.Children.Insert(0, notice);
            }

            AircraftPerformance.Children.Add(myPanel2);
            AircraftPerformance.Children.Add(sectiondivider());
        }

        private void BtnAircraftProcess_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(aircraftDirectory + "\\aircraft.cfg"))
            {
                MessageBox.Show(CsvHelper.trans("aircraft_cfg_not_found"), "", MessageBoxButton.OK);
            }
            else
            {
                // PROCESS AIRCRAFT FILE
                if (File.Exists(aircraftDirectory + "\\.aircraft.cfg"))
                {
                    try
                    {
                        File.Delete(aircraftDirectory + "\\.aircraft.cfg");
                    }
                    catch (Exception ex) {
                        Console.WriteLine(ex.Message);
                        MessageBox.Show(CsvHelper.trans("aircraft_cfg_split_failed"));
                        return;
                    }
                }

                if (!File.Exists(aircraftDirectory + "\\.aircraft.cfg"))
                {
                    MessageBoxResult messageBoxResult = MessageBox.Show(CsvHelper.trans("aircraft_split_notice"), CsvHelper.trans("aircraft_split_header"), System.Windows.MessageBoxButton.YesNo);
                    if (messageBoxResult == MessageBoxResult.Yes)
                    {
                        CfgHelper.splitCfg(aircraftDirectory);
                    } else
                    {
                        return;
                    }
                }

                JSONHelper.scanTargetFolder(projectDirectory);
                SummaryUpdate();
            }
        }

        public void AddDescriptionClick(object sender, RoutedEventArgs e)
        {
            int i = 0;

            foreach (var checkboxLabel in getCheckedOptions(AircraftPerformance)) {
                string section = checkboxLabel.Split(']')[0] + "]";
                string val = checkboxLabel.Replace(section, "").Trim().Split(' ')[0].ToLower().Trim();
                string name = "";
                string newValue = "";

                if (val == "ui_certified_ceiling")
                    name = "ceiling";
                else if (val == "ui_max_range")
                    name = "range";
                else if (val == "ui_autonomy")
                    name = "endurance";
                else if (val == "cruise_speed")
                    name = "cruise speed";
                else if (val == "ui_typerole")
                    newValue = "\"Aircraft\"";

                //Console.WriteLine("### " + val);

                if (name != "")
                {
                    string value = "";

                    if (val != "cruise_speed")
                    {
                        Regex regex = new Regex(@"(?i)(.*)" + name + @"(\\t\\n|\\n)([\d,]+)(.+)(?-i)");
                        Match match = regex.Match(CfgHelper.getCfgValue("performance", "aircraft.cfg", "[GENERAL]"));

                        if (match.Success && match.Groups.Count >= 3)
                            value = match.Groups[3].Value.Replace(",", "");
                    }
                    else
                    {
                        value = CfgHelper.getCfgValue(val, "flight_model.cfg", "[REFERENCE SPEEDS]");
                    }

                    if (value != "")
                    {
                        CfgHelper.setCfgValue(aircraftDirectory, val, value, "aircraft.cfg", section);
                        i++;
                    }
                }
                else if (newValue != "")
                {
                    CfgHelper.setCfgValue(aircraftDirectory, val, newValue, "aircraft.cfg", section);
                    i++;
                }
            }

            if (i > 0)
                CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "aircraft.cfg" });

            SummaryUpdate();
        }
        // AIRCRAFT END

        // ENGINES START
        public void SummaryEngines()
        {
            EnginesData.Children.Clear();
            tabEngines.Foreground = new SolidColorBrush(Colors.DarkGreen);
            AfterburnerData.Children.Clear();
            EnginesAir.Children.Clear();

            if (CfgHelper.cfgFileExists("engines.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int criticalIssues = 0;

                string engine_type = CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]");
                if (engine_type == "2" || engine_type == "3" || engine_type == "4" || engine_type == "")
                    EnginesData = AddGroupCheckBox(EnginesData, "engine_type = " + (engine_type != "" ? engine_type : "missing"), Colors.DarkRed, criticalIssues++);

                /*string engine0 = CfgHelper.getCfgValue("engine.0", "engines.cfg", "[GENERALENGINEDATA]");
                if (engine0 == "")
                    EnginesData = AddGroupCheckBox(EnginesData, "engine.0 = missing", Colors.DarkRed, criticalIssues++);*/

                string afterburner_available = CfgHelper.getCfgValue("afterburner_available", "engines.cfg", "[TURBINEENGINEDATA]");
                if (afterburner_available != "" && afterburner_available != "0")
                    EnginesData = AddGroupCheckBox(EnginesData, "afterburner_available = " + afterburner_available, Colors.DarkRed, criticalIssues++);

                if (CfgHelper.cfgSectionExists("engines.cfg", "[TURBINEENGINEDATA]") && (engine_type == "1" || engine_type == "5"))
                {
                    if (CfgHelper.getCfgValue("low_idle_n1", "engines.cfg", "[TURBINEENGINEDATA]") == "")
                        EnginesData = AddGroupCheckBox(EnginesData, "low_idle_n1 = missing", Colors.DarkRed, criticalIssues++);

                    if (CfgHelper.getCfgValue("low_idle_n2", "engines.cfg", "[TURBINEENGINEDATA]") == "")
                        EnginesData = AddGroupCheckBox(EnginesData, "low_idle_n2 = missing", Colors.DarkRed, criticalIssues++);

                    if (CfgHelper.getCfgValue("high_n1", "engines.cfg", "[TURBINEENGINEDATA]") == "")
                        EnginesData = AddGroupCheckBox(EnginesData, "high_n1 = missing", Colors.DarkRed, criticalIssues++);

                    if (CfgHelper.getCfgValue("high_n2", "engines.cfg", "[TURBINEENGINEDATA]") == "")
                        EnginesData = AddGroupCheckBox(EnginesData, "high_n2 = missing", Colors.DarkRed, criticalIssues++);
                }

                StackPanel myPanel2 = new StackPanel();

                Button btn = new Button();
                btn = SetButtonAtts(btn);

                if (criticalIssues > 0)
                {
                    btn.Content = CsvHelper.trans("engines_fix_issues");
                    btn.Click += FixengineClick;

                    TextBlock notice = addTextBlock(CsvHelper.trans("engines_issues_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    EnginesData.Children.Insert(0, notice);

                    tabEngines.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn.Content = CsvHelper.trans("engines_no_issues");
                    btn.IsEnabled = false;
                }

                myPanel2.Children.Add(btn);
                EnginesData.Children.Add(myPanel2);

                if (CfgHelper.cfgFileExists("engines.cfg"))
                    getAirCheckboxes(EnginesAir, "engines.cfg");

                /*****/ EnginesData.Children.Add(sectiondivider());

                // AFTERBURNER
                if (engine_type == "1" && CfgHelper.cfgSectionExists("engines.cfg", "[TURBINEENGINEDATA]"))
                {
                    double[] ABdata = getABdata();

                    string default_n1_and_mach_on_thrust_table = "0.000000:0.000000:0.900000,0.000000:0.000000:0.000000,20.000000:0.025000:0.025000,25.000000:0.051000:0.051000,30.000000:0.080000:0.080000,35.000000:0.112000:0.112000,40.000000:0.200000:0.200000,45.000000:0.281000:0.281000,50.000000:0.368000:0.370000,55.000000:0.431000:0.430000,60.000000:0.521000:0.520000,65.000000:0.629000:0.650000,70.000000:0.726000:0.800000,75.000000:0.818000:1.050000,80.000000:0.900000:1.230000,85.000000:0.992000:1.350000,90.000000:1.043000:1.420000,95.000000:1.077000:1.450000,100.000000:1.125000:1.470000,105.000000:1.145000:1.480000,110.000000:1.170000:1.478000";
                    string current_n1_and_mach_on_thrust_table = CfgHelper.getCfgValue("n1_and_mach_on_thrust_table", "engines.cfg", "[TURBINEENGINEDATA]");

                    StackPanel myPanel3 = new StackPanel();

                    Button btn3 = SetButtonAtts(new Button());

                    if (ABdata[4] > 0)
                    {
                        btn3.Content = CsvHelper.trans("engines_fix_afterburner");
                        btn3.Click += FixAfterburnerClick;
                    } else
                    {
                        btn3.Content = CsvHelper.trans("engines_fix_afterburner_not_available");
                        btn3.IsEnabled = false;
                    }

                    List<double[]>[] graphs = CfgHelper.parseCfgDoubleTable(!string.IsNullOrEmpty(current_n1_and_mach_on_thrust_table) ?
                        current_n1_and_mach_on_thrust_table : default_n1_and_mach_on_thrust_table);


                    Canvas painting = new Canvas();
                    painting.Width = 600;
                    painting.Height = 200;
                    painting.Children.Add(getGraphLine(Colors.Black, 0, 0, 0, 1, painting.Width, painting.Height, 2));
                    painting.Children.Add(getGraphLine(Colors.Black, 0, 0, 1/1.1, 0, painting.Width, painting.Height, 2));
                    for (float k = 1; k <= 5; k++)
                        painting.Children.Add(getGraphLine(k % 5 == 0 ? Colors.Black : Colors.Gray, k / 5.0 / 1.1, 0, k / 5.0 / 1.1, 1, painting.Width, painting.Height, 1));

                    for (float l = 1; l <= 6; l++)
                        painting.Children.Add(getGraphLine(l % 2 == 0 ? Colors.Black : Colors.Gray, 0, l/6.0, 1/1.1, l/6.0, painting.Width, painting.Height, 1));

                    // RENDER GRAPH
                    for (int i = 0; i <= 1; i++)
                    {
                        double x1 = -1;
                        double y1 = -1;
                        foreach (var val in graphs[i])
                        {
                            if (x1 != -1 && y1 != -1)
                                painting.Children.Add(getGraphLine(i==0?Colors.Black:Colors.DarkRed, x1 / 110, y1, val[0] / 110, val[1] / 3, painting.Width, painting.Height, 1));

                            x1 = val[0];
                            y1 = val[1] / 3;
                        }
                    }

                    List<TextBlock> notices = new List<TextBlock>();

                    notices.Add(addTextBlock(CsvHelper.trans("engines_afterburner_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 0, 0, 10), TextWrapping.Wrap, 600));

                    notices.Add(addTextBlock(ABdata[4] >= 1 ?
                        string.Format(CsvHelper.trans("engines_afterburners_stages"), ABdata[4]) :
                        CsvHelper.trans("engines_afterburners_disabled"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, ABdata[4] >= 0 ? Colors.Black : Colors.DarkRed, new Thickness(0, 0, 0, 0), TextWrapping.Wrap, 600));

                    if (string.IsNullOrEmpty(current_n1_and_mach_on_thrust_table))
                        notices.Add(addTextBlock(CsvHelper.trans("engines_afterburners_table_missing"),
                           HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.DarkRed, new Thickness(0, 0, 0, 0), TextWrapping.Wrap, 600));

                    notices.Add(addTextBlock(ABdata[3] != -1 ?
                        string.Format(CsvHelper.trans("engines_afterburners_consumption"), ABdata[3], (100 + 100*ABdata[3]/3).ToString(".")) :
                        string.Format(CsvHelper.trans("engines_afterburners_consumption_missing"), ABdata[2], (100 + 100*ABdata[2]/3).ToString(".")),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, ABdata[3] != -1 ? Colors.Black : Colors.DarkRed, new Thickness(0, 0, 0, 0), TextWrapping.Wrap, 600));

                    notices.Add(addTextBlock(ABdata[1] != -1 ?
                        string.Format(CsvHelper.trans("engines_afterburners_threshold"), ABdata[1]) :
                        string.Format(CsvHelper.trans("engines_afterburners_threshold_missing"), ABdata[0]),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, ABdata[1] != -1 ? Colors.Black : Colors.DarkRed, new Thickness(0, 0, 0, 10), TextWrapping.Wrap, 600));

                    foreach (var notice in notices)
                        AfterburnerData.Children.Add(notice);

                    myPanel3.Children.Add(btn3);
                    AfterburnerData.Children.Add(painting);
                    AfterburnerData.Children.Add(myPanel3);

                    /*****/
                    AfterburnerData.Children.Add(sectiondivider());
                }

            }

        }

        private Line getGraphLine(Color color, double x1, double y1, double x2, double y2, double width, double height, int thickness = 1)
        {
            Line line = new Line();
            line.Stroke = new SolidColorBrush(color);
            line.StrokeThickness = thickness;
            line.X1 = x1 * width;
            line.Y1 = height * (1 - y1);
            line.X2 = x2 * width;
            line.Y2 = height * (1 - y2);

            return line;
        }

        private void FixengineClick(object sender, RoutedEventArgs e) {
            int i = 0;

            if (CfgHelper.cfgFileExists("engines.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                foreach (var checkboxLabel in getCheckedOptions(EnginesData, "="))
                {
                    string[] val = checkboxLabel.Split('=');

                    /*if (val[0].Trim() == "engine.0")
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "0,0,0", "engines.cfg", "[GENERALENGINEDATA]");*/
                    if (val[0].Trim() == "low_idle_n1")
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "20", "engines.cfg", "[TURBINEENGINEDATA]");
                    else if (val[0].Trim() == "low_idle_n2")
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "60", "engines.cfg", "[TURBINEENGINEDATA]");
                    else if (val[0].Trim() == "high_n1")
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "100", "engines.cfg", "[TURBINEENGINEDATA]");
                    else if (val[0].Trim() == "high_n2")
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "100", "engines.cfg", "[TURBINEENGINEDATA]");
                    else if (val[0].Trim() == "afterburner_available")
                    {
                        CfgHelper.setCfgValue(aircraftDirectory, "afterburner_stages", val[1].Trim(), "engines.cfg", "[TURBINEENGINEDATA]", false);
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "0", "engines.cfg");
                    }
                    else
                        CfgHelper.setCfgValue(aircraftDirectory, val[0].Trim(), "0", "engines.cfg");
                    
                    i++;
                }
            }

            if (i > 0)
                CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "engines.cfg" });

            SummaryUpdate();
        }

        private void FixAfterburnerClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("engines.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                string resultTable = "";

                double[] ABdata = getABdata();
                double threshold = ABdata[1] != -1 ? ABdata[1] : ABdata[0];
                double consumption = ABdata[3] != -1 ? ABdata[3] : ABdata[2];

                string default_n1_and_mach_on_thrust_table = "0.000000:0.000000:0.900000,0.000000:0.000000:0.000000,20.000000:0.025000:0.025000,25.000000:0.051000:0.051000,30.000000:0.080000:0.080000,35.000000:0.112000:0.112000,40.000000:0.200000:0.200000,45.000000:0.281000:0.281000,50.000000:0.368000:0.370000,55.000000:0.431000:0.430000,60.000000:0.521000:0.520000,65.000000:0.629000:0.650000,70.000000:0.726000:0.800000,75.000000:0.818000:1.050000,80.000000:0.900000:1.230000,85.000000:0.992000:1.350000,90.000000:1.043000:1.420000,95.000000:1.077000:1.450000,100.000000:1.125000:1.470000,105.000000:1.145000:1.480000,110.000000:1.170000:1.478000";
                string current_n1_and_mach_on_thrust_table = CfgHelper.getCfgValue("n1_and_mach_on_thrust_table", "engines.cfg", "[TURBINEENGINEDATA]");


                List<double[]>[] graphs = CfgHelper.parseCfgDoubleTable(!string.IsNullOrEmpty(current_n1_and_mach_on_thrust_table) ? current_n1_and_mach_on_thrust_table : default_n1_and_mach_on_thrust_table);

                for(int i = 0; i < graphs[0].Count && i < graphs[1].Count; i++)
                {
                    double val1 = graphs[0].ElementAt(i)[0];
                    double val2 = graphs[0].ElementAt(i)[1];
                    double val3 = graphs[1].ElementAt(i)[1];

                    if (threshold != 0 && consumption != 0 && val1 > 100 * threshold)
                    {
                        val2 *= 1 + consumption / 3;
                        val3 *= 1 + consumption / 3;
                    }

                    resultTable += val1 + ":" + val2 + ":" + val3 + ",";
                }

                resultTable = resultTable.TrimEnd(',');
                Console.WriteLine("Afterburner final table: " + resultTable);

                MessageBoxResult messageBoxResult = MessageBox.Show(string.Format(CsvHelper.trans("engines_afterburner_warning_notice"), threshold, consumption), CsvHelper.trans("engines_afterburner_warning_header"), System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    CfgHelper.setCfgValue(aircraftDirectory, "n1_and_mach_on_thrust_table", resultTable, "engines.cfg", "[TURBINEENGINEDATA]", true);
                    CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "engines.cfg" });
                }
            }


            SummaryUpdate();
        }
        public double[] getABdata()
        {
            string current_afterburner_throttle_threshold = CfgHelper.getCfgValue("afterburner_throttle_threshold", "engines.cfg", "[TURBINEENGINEDATA]");
            string current_afterburnthrustspecificfuelconsumption = CfgHelper.getCfgValue("afterburnthrustspecificfuelconsumption", "engines.cfg", "[TURBINEENGINEDATA]");
            string current_afterburner_available = CfgHelper.getCfgValue("afterburner_stages", "engines.cfg", "[TURBINEENGINEDATA]", true);

            double val1 = 0.85;
            double.TryParse(!string.IsNullOrEmpty(current_afterburner_throttle_threshold) ? current_afterburner_throttle_threshold : "-1", out double val2);
            double val3 = 1.5;
            double.TryParse(!string.IsNullOrEmpty(current_afterburnthrustspecificfuelconsumption) ? current_afterburnthrustspecificfuelconsumption : "-1", out double val4);

            double.TryParse(!string.IsNullOrEmpty(current_afterburner_available) ? current_afterburner_available : "-1", out double val5);

            return new double[] { val1, val2, val3, val4, val5 };
        }


        private void BtnEnginePowerClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("engines.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                Button btn = (Button)sender;

                CfgHelper.adjustEnginesPower(aircraftDirectory, double.Parse(btn.Tag.ToString()));
            }
        }
        // ENGINES END

        // AIR START
        private void getAirCheckboxes(StackPanel parent, string filename)
        {
            airFilename = "";
            var airFiles = Directory.EnumerateFiles(aircraftDirectory, "*.air", SearchOption.TopDirectoryOnly);

            foreach (string currentFile in airFiles)
            {
                string tempName = System.IO.Path.GetFileName(currentFile);
                if (!String.IsNullOrEmpty(tempName) /*&& tempName[0] != '.'*/)
                    airFilename = tempName;
            }
            
            if (airFilename == "")
                airFilename = System.IO.Path.GetFileName(aircraftDirectory).Trim('\\') + ".air";

            int values = 0;
            StackPanel myPanel = new StackPanel();

            string airExported = aircraftDirectory + "\\" + airFilename.Replace(".air", ".txt");
            string conversionTable = AppDomain.CurrentDomain.BaseDirectory + "\\airTbls\\AIR to CFG Master Sheet - " + filename + ".csv";
            string buttonLabel = "";
            bool download = false;
            bool launch = false;

            // NO TABLES
            if (!File.Exists(conversionTable))
            {
                buttonLabel = CsvHelper.trans("air_tbl_not_found");

                TextBlock notice = addTextBlock(string.Format(CsvHelper.trans("air_tbl_not_found_notice"), conversionTable), HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black,
                    new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                parent.Children.Insert(0, notice);
            }
            // NO AIRUPDATE
            else if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "AirDat\\AirUpdate.exe"))
            {
                buttonLabel = CsvHelper.trans("airupdate_not_found");

                TextBlock notice = addTextBlock(CsvHelper.trans("airupdate_not_found_notice"), HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black,
                    new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                parent.Children.Insert(0, notice);

                download = true;
            }
            // NO AIR DUMP
            else if (!File.Exists(airExported))
            {
                buttonLabel = CsvHelper.trans("air_dump_not_found");

                TextBlock notice = addTextBlock(string.Format(CsvHelper.trans("air_dump_notice"), airExported) + Environment.NewLine +
                    CsvHelper.trans("air_dump_notice1") + Environment.NewLine +
                    string.Format(CsvHelper.trans("air_dump_notice2"), aircraftDirectory) + Environment.NewLine + "" +
                    CsvHelper.trans("air_dump_notice3") + Environment.NewLine +
                    CsvHelper.trans("air_dump_notice4") + Environment.NewLine +
                    CsvHelper.trans("air_dump_notice5") + Environment.NewLine +
                    CsvHelper.trans("air_dump_notice6"), HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black,
                    new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                parent.Children.Insert(0, notice);

                launch = true;
            }
            //CHECK AIR VALUES
            else
            {
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
                                    parent = AddGroupCheckBox(parent, attr, Colors.Black, values++, attr + "=" + airLine[2]);

                                    Grid DynamicGrid = new Grid();
                                    ColumnDefinition gridCol1 = new ColumnDefinition();
                                    ColumnDefinition gridCol2 = new ColumnDefinition();
                                    RowDefinition gridRow1 = new RowDefinition();
                                    RowDefinition gridRow2 = new RowDefinition();
                                    DynamicGrid.ColumnDefinitions.Add(gridCol1);
                                    DynamicGrid.ColumnDefinitions.Add(gridCol2);

                                    TextBlock myBlock2 = addTextBlock(CsvHelper.trans("air_new_val") + ": " + airLine[2], HorizontalAlignment.Left, VerticalAlignment.Top,
                                        airLine[2] == "0" || airLine[2] == "0.0" || Regex.IsMatch(airLine[2], @"(0,){8,}") || String.IsNullOrWhiteSpace(Regex.Replace(airLine[2], @"[0-9-.]+:([1][.0]+)[,]*", ""))
                                            ? Colors.DarkRed : Colors.Black, new Thickness(0));
                                    myBlock2.ToolTip = airLine[2].Replace(",", Environment.NewLine);
                                    Grid.SetColumn(myBlock2, 0);
                                    Grid.SetRow(myBlock2, 0);
                                    DynamicGrid.Children.Add(myBlock2);

                                    TextBlock myBlock3;
                                    if (oldVal != "")
                                    {
                                        myBlock3 = addTextBlock(CsvHelper.trans("air_old_val") + ": " + oldVal, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black, new Thickness(0));
                                        myBlock3.ToolTip = oldVal.Replace(",", Environment.NewLine);
                                    } else
                                    {
                                        myBlock3 = addTextBlock(CsvHelper.trans("air_default_val") + ": " + defVal, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.Black, new Thickness(0));
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
                    TextBlock notice = addTextBlock(CsvHelper.trans("air_loading_failed"), HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.DarkRed, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    parent.Children.Insert(0, notice);
                }

                if (values > 0)
                {
                    TextBlock notice = addTextBlock(CsvHelper.trans("air_import_notice"), HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    parent.Children.Insert(0, notice);

                    buttonLabel = CsvHelper.trans("air_insert_values");
                }
                else
                {
                    TextBlock notice = addTextBlock(CsvHelper.trans("air_no_values_to_import_notice"), HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    parent.Children.Insert(0, notice);

                    buttonLabel = CsvHelper.trans("air_no_values_to_import");
                }
            }

            Button btn = new Button();
            btn = SetButtonAtts(btn);
            btn.Content = buttonLabel;

            if (values > 0)
                btn.Click += InsertAirValues;
            else
            {
                if (download)
                    btn.Click += GetAirUpdate;
                else if (launch)
                    btn.Click += LaunchAirUpdate;
                else
                    btn.IsEnabled = false;
            }

            myPanel.Children.Add(btn);

			// REMOVE BUTTON
            if (airFilename.Length > 1 && airFilename[0] != '.' && File.Exists(aircraftDirectory + "\\" + airFilename) && !File.Exists(aircraftDirectory + "\\." + airFilename) &&
                !download && !launch)
            {
                Button btn2 = new Button();
                btn2 = SetButtonAtts(btn2);
                btn2.Content = CsvHelper.trans("air_remove");
                btn2.Click += backupAirFile;
                myPanel.Children.Add(btn2);
            }

            parent.Children.Add(myPanel);

            parent.Children.Add(sectiondivider());
        }

        private void LaunchAirUpdate(object sender, RoutedEventArgs e)
        {
            Process prcs = new Process();
            
            try
            {
                prcs.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "AirDat\\AirUpdate.exe";
                prcs.StartInfo.CreateNoWindow = true;
                prcs.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory + "AirDat\\";
                prcs.EnableRaisingEvents = true;
                prcs.Exited += new EventHandler(AirUpdateClosed);
                prcs.Start();
            }
            catch
            {
                Dispatcher.Invoke(() => SummaryUpdate());
                return;
            }
        }
        private void AirUpdateClosed(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() => SummaryUpdate());
        }

        private void GetAirUpdate(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\"))
                Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\");

            Dispatcher.Invoke(() => fsTabControl.IsEnabled = false);

            WebClient _webClient = new WebClient();
            _webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(OnAirDownloadCompleted);
            _webClient.DownloadFileAsync(new Uri("http://www.mudpond.org/AirDat.ZIP"), AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\" + TEMP_FILE);
        }

        private void OnAirDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if ((e == null || !e.Cancelled && e.Error == null) && File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\" + TEMP_FILE) && new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\" + TEMP_FILE).Length > 10)
            {
                Extract extract = new Extract();
                extract.Run(AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\" + TEMP_FILE, "", AppDomain.CurrentDomain.BaseDirectory + "\\AirDat\\");
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (true) { if (sw.ElapsedMilliseconds > 1000 || File.Exists(AppDomain.CurrentDomain.BaseDirectory + "AirDat\\AirUpdate.exe")) break; }

            Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
            SummaryUpdate();
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

                    foreach (var checkboxLabel in getCheckedOptions(parentPanel, "", true))
                    {
                        values[i] = checkboxLabel;
                        i++;
                    }

                    if (i > 0)
                    {
                        // CFG BACKUP
                        if (File.Exists(aircraftDirectory + "\\" + filename) && !File.Exists(aircraftDirectory + "\\." + filename))
                        {
                            CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                            File.Copy(aircraftDirectory + "\\" + filename, aircraftDirectory + "\\." + filename);
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

        private void backupAirFile(object sender, RoutedEventArgs e)
        {
            // AIR BACKUP
            if (airFilename.Length > 1 && airFilename[0] != '.' &&
                File.Exists(aircraftDirectory + "\\" + airFilename) && !File.Exists(aircraftDirectory + "\\." + airFilename))
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(string.Format(CsvHelper.trans("air_delete_warning_notice"), "." + airFilename, airFilename), CsvHelper.trans("air_delete_warning_header"), System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(aircraftDirectory + "\\." + airFilename))
                            File.Delete(aircraftDirectory + "\\." + airFilename);
                        File.Move(aircraftDirectory + "\\" + airFilename, aircraftDirectory + "\\." + airFilename);
                        if (File.Exists(aircraftDirectory + "\\." + airFilename.Replace(".air", ".txt")))
                            File.Delete(aircraftDirectory + "\\." + airFilename.Replace(".air", ".txt"));
                        File.Move(aircraftDirectory + "\\" + airFilename.Replace(".air", ".txt"), aircraftDirectory + "\\." + airFilename.Replace(".air", ".txt"));
                    }
                    catch { }

                    JSONHelper.scanTargetFolder(projectDirectory);
                    SummaryUpdate();
                }
            }
        }

    // AIR END

    // SYSTEMS START
    public void SummarySystems()
        {
            SystemsData.Children.Clear();

            if (CfgHelper.cfgFileExists("systems.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int lightsBroken = 0;
                int lightsToInsert = 0;
                foreach (var light in CfgHelper.getLights(aircraftDirectory))
                    if (!String.IsNullOrEmpty(light))
                        SystemsData = AddGroupCheckBox(SystemsData, light, Colors.DarkRed, lightsBroken++);

                StackPanel myPanel2 = new StackPanel();
                Button btn = new Button();
                btn = SetButtonAtts(btn);

                if (lightsBroken > 0)
                {
                    btn.Content = CsvHelper.trans("systems_convert_lights");
                    btn.Click += FixLightsClick;

                    TextBlock notice = addTextBlock(CsvHelper.trans("systems_convert_lights_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    SystemsData.Children.Insert(0, notice);

                    tabSystems.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    List<string> contactPoints = CfgHelper.getContactPoints(aircraftDirectory, "1");
                    if (CfgHelper.getTaxiLights(aircraftDirectory) == 0 && contactPoints.Count > 0)
                    {
                        foreach (var point in contactPoints)
                            if (!String.IsNullOrEmpty(point))
                                SystemsData = AddGroupCheckBox(SystemsData, point, Colors.DarkOrange, lightsToInsert++);

                        btn.Content = CsvHelper.trans("systems_insert_lights");
                        btn.Click += InsertLightsClick;

                        TextBlock notice = addTextBlock(CsvHelper.trans("systems_insert_lights_notice"),
                            HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                        SystemsData.Children.Insert(0, notice);

                        tabSystems.Foreground = new SolidColorBrush(Colors.DarkOrange);
                    }
                    else
                    {
                        btn.Content = CsvHelper.trans("systems_no_light_issues");
                        btn.IsEnabled = false;

                        tabSystems.Foreground = new SolidColorBrush(Colors.DarkGreen);
                    }
                }

                myPanel2.Children.Add(btn);
                SystemsData.Children.Add(myPanel2);

                //SystemContent.Foreground = new SolidColorBrush(Colors.Black);

                SystemsData.Children.Add(sectiondivider());
            }

        }

        private void FixLightsClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("systems.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int i = 0;

                foreach (var checkboxLabel in getCheckedOptions(SystemsData))
                {
                    if (checkboxLabel.ToLower().Trim().StartsWith("light."))
                    {
                        // CONVERTS LIGHT DATA
                        // light\.(\d+)(.*)=(.*)(\d+),(.*)(\d+),(.*)(\d+),(.*)(fx_[A-Za-z]+)(.*)
                        // MSFS lightdef.0 = Type:1#Index:1#LocalPosition:-11.5,0,11.6#LocalRotation:0,0,0#EffectFile:LIGHT_ASOBO_BeaconTop#Node:#PotentiometerIndex:1#EmMesh:LIGHT_ASOBO_BeaconTop
                        // FSX light.0 = 3,  -39.00, -23.6,  -0.25, fx_navredm ,
                        string[] fsxLight = checkboxLabel.Split('=');
                        if (fsxLight.Length >= 2)
                        {
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

        private void InsertLightsClick(object sender, RoutedEventArgs e)
        {
            if (CfgHelper.cfgFileExists("systems.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int i = 100;

                foreach (var checkboxLabel in getCheckedOptions(SystemsData))
                {
                    if (checkboxLabel.ToLower().Trim().StartsWith("point."))
                    {
                        string[] fsxLight = checkboxLabel.Split('=');
                        if (fsxLight.Length >= 2)
                        {
                            if (fsxLight[1].Contains(',') && fsxLight[1].Split(',').Length >= 4)
                            {
                                string[] fsxData = fsxLight[1].Split(',');
                                if (fsxData.Length >= 5)
                                {
                                    string x = fsxData[1].Replace(" ", "").Trim() + (!fsxData[1].Contains('.') ? ".0" : "");
                                    string z = fsxData[2].Replace(" ", "").Trim() + (!fsxData[2].Contains('.') ? ".0" : "");
                                    string y = fsxData[3].Replace(" ", "").Trim() + (!fsxData[3].Contains('.') ? ".0" : "");
                                    if (double.TryParse(x, out double xVal) && double.TryParse(y, out double yVal) && double.TryParse(z, out double zVal))
                                    {
                                        string newLight = "Type:6#Index:0#LocalPosition:" + (xVal + 1.0).ToString() + "," + zVal.ToString() + "," + (yVal + 2.0).ToString() + "#LocalRotation:0,0,0#EffectFile:LIGHT_ASOBO_Taxi#PotentiometerIndex:1#EmMesh:LIGHT_ASOBO_Taxi";
                                        CfgHelper.setCfgValue(aircraftDirectory, "lightdef." + i, newLight, "systems.cfg", "[LIGHTS]");
                                        i++;
                                    }
                                }
                            }
                        }
                    }
                }

                if (i > 100)
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
            FlightModelAir.Children.Clear();

            if (CfgHelper.cfgFileExists("flight_model.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int contactPointsBroken = 0;
                List<string> contactPoints = CfgHelper.getContactPoints(aircraftDirectory, "1");
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

                                //Console.WriteLine(firstContactPoint + " " + secondContactPoint + " distance: " + distance.ToString());
                                double minDistance = 3.0;
                                if (distance > 0 && distance < minDistance)
                                {
                                    Color color = Colors.DarkOrange;

                                    FlightModelData = AddGroupCheckBox(FlightModelData, firstContactPoint, color, contactPointsBroken++);
                                    FlightModelData = AddGroupCheckBox(FlightModelData, secondContactPoint, color, contactPointsBroken);

                                    StackPanel myPanel = new StackPanel();
                                    myPanel.Height = 16;
                                    myPanel.VerticalAlignment = VerticalAlignment.Top;
                                    myPanel.Margin = new Thickness(30, 0, 0, 10);

                                    TextBlock myBlock = addTextBlock(string.Format(CsvHelper.trans("fm_contact_point_data"), distance.ToString("N2")), HorizontalAlignment.Left, VerticalAlignment.Top, color, new Thickness(0));
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
                    btn.Content = CsvHelper.trans("fm_fix_contact_points");
                    btn.Click += FixContactsClick;

                    TextBlock notice = addTextBlock(CsvHelper.trans("fm_fix_contact_points_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600 );
                    FlightModelData.Children.Insert(0, notice);

                    //tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn.Content = CsvHelper.trans("fm_contact_points_issues");
                    btn.IsEnabled = false;

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkGreen);
                }

                myPanel2.Children.Add(btn);

                // BROKEN POINTS WARNING
                string lastPoint = "";
                if (possiblyDamagedCounter > 0)
                {
                    TextBlock notice = addTextBlock(CsvHelper.trans("fm_contact_points_issues_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    myPanel2.Children.Add(notice);

                    foreach (string val in possiblyDamaged)
                    {
                        if (lastPoint != val)
                        {
                            lastPoint = val;
                            TextBlock myBlock = addTextBlock(val, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkRed, new Thickness(0));
                            myPanel2.Children.Add(myBlock);
                        }
                    }

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkRed);
                }

                FlightModelData.Children.Add(myPanel2);
                FlightModelData.Children.Add(sectiondivider());

                // FM ISSUES
                FlightModelIssues.Children.Clear();

                int criticalIssues = 0;

                foreach (string attr in new string[] { "compute_aero_center", "fuselage_length", "fuselage_diameter" })
                {
                    string value = CfgHelper.getCfgValue(attr, "flight_model.cfg");
                    if (value == "" && !String.IsNullOrEmpty(CfgHelper.getCfgValue(attr, "flight_model.cfg", "", true)))
                        FlightModelIssues = AddGroupCheckBox(FlightModelIssues, attr + " = " + (value != "" ? value : "missing"), Colors.DarkRed, criticalIssues++);
                }

                foreach (string attr in new string[] { "elevator_scaling_table", "aileron_scaling_table", "rudder_scaling_table" })
                {
                    string value = CfgHelper.getCfgValue(attr, "flight_model.cfg");
                    string result = Regex.Replace(value, @"[0-9-.]+:([1][.0]+)[,]*", "");
                    if ((String.IsNullOrEmpty(value) || String.IsNullOrEmpty(result.Trim())) && !String.IsNullOrEmpty(CfgHelper.getCfgValue(attr, "flight_model.cfg", "", true)))
                    {
                        FlightModelIssues = AddGroupCheckBox(FlightModelIssues, attr + " = " + (value != "" ? value : "missing"), Colors.DarkRed, criticalIssues++);
                    }
                }

                StackPanel myPanel3 = new StackPanel();

                Button btn3 = new Button();
                btn3 = SetButtonAtts(btn3);

                if (criticalIssues > 0)
                {
                    btn3.Content = CsvHelper.trans("fm_fix_flight_model");
                    btn3.Click += FixFlightModelClick;

                    TextBlock notice = addTextBlock(CsvHelper.trans("fm_fix_flight_model_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    FlightModelIssues.Children.Insert(0, notice);

                    tabFlightModel.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    btn3.Content = CsvHelper.trans("fm_no_flight_model_issues");
                    btn3.IsEnabled = false;
                }

                myPanel3.Children.Add(btn3);
                FlightModelIssues.Children.Add(myPanel3);
                FlightModelIssues.Children.Add(sectiondivider());

                if (CfgHelper.cfgFileExists("flight_model.cfg"))
                    getAirCheckboxes(FlightModelAir, "flight_model.cfg");

            }
        }

        private void FixFlightModelClick(object sender, RoutedEventArgs e)
        {
            int i = 0;

            if (CfgHelper.cfgFileExists("flight_model.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                string message = "";

                foreach (var checkboxLabel in getCheckedOptions(FlightModelIssues, "="))
                {
                    string val = checkboxLabel.Split('=')[0].Trim();

                    if (val.Contains("_table"))
                        CfgHelper.setCfgValue(aircraftDirectory, val, "-0.785:1,0:0.4,0.785:1", "flight_model.cfg");
                    else if (val == "compute_aero_center")
                        CfgHelper.setCfgValue(aircraftDirectory, val, "1", "flight_model.cfg");
                    else if (val == "fuselage_length")
                    {
                        string length = CfgHelper.getCfgValue("wing_span", "flight_model.cfg", "[AIRPLANE_GEOMETRY]");
                        double num;
                        string stringNewVal = (Double.TryParse(length, out num) ? 1.1 * num : 50).ToString();
                        CfgHelper.setCfgValue(aircraftDirectory, val, stringNewVal, "flight_model.cfg");
                        message += string.Format(CsvHelper.trans("fm_value_generated_notice"), val, stringNewVal, val) + Environment.NewLine + Environment.NewLine;
                    }
                    else if (val == "fuselage_diameter")
                    {
                        string length = CfgHelper.getCfgValue("wing_span", "flight_model.cfg", "[AIRPLANE_GEOMETRY]");
                        double num;
                        string weight = CfgHelper.getCfgValue("max_gross_weight", "flight_model.cfg", "[WEIGHT_AND_BALANCE]");
                        double num2;
                        string stringNewVal = Math.Max(5, (Double.TryParse(length, out num) && Double.TryParse(length, out num2) ? num * num2 / 666 : 5)).ToString();
                        CfgHelper.setCfgValue(aircraftDirectory, val, stringNewVal, "flight_model.cfg");
                        message += string.Format(CsvHelper.trans("fm_value_generated_notice2"), val, stringNewVal) + Environment.NewLine + Environment.NewLine;
                    }

                    i++;
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
                    if (digRregex.IsMatch(fsxNum))
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
                                Console.WriteLine(string.Format(CsvHelper.trans("fm_bad_contact_point"), fsxData[i].Replace(" ", "").Trim()));
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
            if (CfgHelper.cfgFileExists("flight_model.cfg") || CfgHelper.cfgFileExists("aircraft.cfg"))
            {
                int i = 0;
                string lastPoint = null;

                foreach (var pnl in FlightModelData.Children)
                {
                    if (pnl.GetType() != typeof(StackPanel))
                        continue;

                    StackPanel panel = (StackPanel)pnl;

                    if (panel.Children.Count > 0)
                    {
                        if (panel.Children[0].GetType() == typeof(CheckBox))
                        {
                            CheckBox a = (CheckBox)panel.Children[0];
                            if ((string)a.Content != CsvHelper.trans("toggle_all"))
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
                                            /*double minDistance = 3.0;
                                            double distance = Math.Pow(
                                                    Math.Pow(testTwo[2] - testOne[2], 2) +
                                                    Math.Pow(testTwo[3] - testOne[3], 2) +
                                                    Math.Pow(testTwo[4] - testOne[4], 2),
                                                0.5);
                                            
                                            double[] magnitude1 = new double[] { testTwo[2] - testOne[2], testTwo[3] - testOne[3], testTwo[4] - testOne[4] };
                                            double max1 = 0;
                                            foreach (double mag in magnitude1)
                                                if (Math.Abs(mag) > max1)
                                                    max1 = Math.Abs(mag);

                                            double[] magnitude2 = new double[] { testOne[2] - testTwo[2], testOne[3] - testTwo[3], testOne[4] - testTwo[4] };
                                            double max2 = 0;
                                            foreach (double mag in magnitude2)
                                                if (Math.Abs(mag) > max2)
                                                    max2 = Math.Abs(mag);

                                            if (max1 > 0 && max2 > 0)
                                            {
                                                testOne[2] -= magnitude1[0] / max1 * (minDistance - distance) / 2.0;
                                                testOne[3] -= magnitude1[1] / max1 * (minDistance - distance) / 2.0;
                                                testOne[4] -= magnitude1[2] / max1 * (minDistance - distance) / 2.0;

                                                testTwo[2] -= magnitude2[0] / max2 * (minDistance - distance) / 2.0;
                                                testTwo[3] -= magnitude2[1] / max2 * (minDistance - distance) / 2.0;
                                                testTwo[4] -= magnitude2[2] / max2 * (minDistance - distance) / 2.0;*/


                                            testOne[2] = testTwo[2] = ((testOne[2] + testTwo[2]) / 2);
                                            testOne[3] = testTwo[3] = ((testOne[3] + testTwo[3]) / 2);
                                            testOne[4] = testTwo[4] = ((testOne[4] + testTwo[4]) / 2);
                                            
                                            string value1 = String.Join(", ", testOne);
                                            int index = value1.IndexOf(",");
                                            value1 = index >= 0 ? value1.Substring(index + 1) : value1;

                                            string value2 = String.Join(", ", testTwo);
                                            index = value2.IndexOf(",");
                                            value2 = index >= 0 ? value2.Substring(index + 1) : value2;

                                            CfgHelper.setCfgValue(aircraftDirectory, "point." + testOne[0], value1, "flight_model.cfg", "[CONTACT_POINTS]");
                                            CfgHelper.setCfgValue(aircraftDirectory, "point." + testTwo[0], value2, "flight_model.cfg", "[CONTACT_POINTS]");

                                            i++;

                                            //}
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

            foreach (var pnl in parentPanelsList)
            {
                if (pnl.GetType() != typeof(StackPanel))
                    continue;

                StackPanel parentPanel = (StackPanel)pnl;

                string filename = parentPanel.Tag.ToString();

                parentPanel.Children.Clear();

                if (CfgHelper.cfgFileExists(filename) || CfgHelper.cfgFileExists("aircraft.cfg"))
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
                                string engine_type = CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]");
                                if (engine_type.Contains('.'))
                                    engine_type = engine_type.Split('.')[0];

                                if (secton.Contains("[FUEL_QUANTITY]") || secton.Contains("[AIRSPEED]") || 
                                    secton.Contains("[RPM]") && engine_type == "0" ||
                                    secton.Contains("[TORQUE]") && (engine_type == "1" || engine_type == "5") ||
                                    secton.Contains("[THROTTLE_LEVELS]") || secton.Contains("[FLAPS_LEVELS]") ||
                                    secton.Contains("[CONTROLS.") || secton.Contains("[FUELSYSTEM.") || secton.Contains("[SIMVARS.") ||
                                    (secton.Contains("[PROPELLER]") || secton.Contains("[PISTON_ENGINE]")) && engine_type == "0" ||
                                    (secton.Contains("[PROPELLER]") || secton.Contains("[TURBOPROP_ENGINE]") || secton.Contains("[TURBINEENGINEDATA]")) && engine_type == "5" ||
                                    (secton.Contains("[TURBINEENGINEDATA]") || secton.Contains("[JET_ENGINE]")) && engine_type == "1" ||
                                    !String.IsNullOrEmpty(airFilename) && File.Exists(aircraftDirectory + "\\" + airFilename.Replace(".air", ".txt")) && secton.Contains("[AERODYNAMICS]")
                                    /*|| secton.Contains("ENGINE PARAMETERS.")*/
                                    )
                                {
                                    AddGroupCheckBox(parentPanel, secton.Substring(1), Colors.DarkRed, sectionsMissing++);
                                    requiredMissing++;
                                }
                                else
                                    AddGroupCheckBox(parentPanel, secton.Substring(1), Colors.Black, sectionsMissing++);
                            }
                            else
                            {
                                StackPanel myPanel = new StackPanel();
                                myPanel.Height = 16;
                                myPanel.VerticalAlignment = VerticalAlignment.Top;
                                myPanel.HorizontalAlignment = HorizontalAlignment.Left;

                                TextBlock myBlock = addTextBlock(secton, HorizontalAlignment.Left, VerticalAlignment.Top, Colors.DarkGreen, new Thickness(20, 0, 0, 0));
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
                        btn.Content = CsvHelper.trans("cfg_insert_sections");
                        btn.Click += InsertSectionsClick;
                        myPanel.Children.Add(btn);
                        parentPanel.Children.Add(myPanel);

                        TextBlock notice = addTextBlock("", HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);

                        if (filename == "cockpit.cfg")
                        {
                            notice.Text = CsvHelper.trans("cfg_insert_cockpit_sections_notice");
                            parentPanel.Children.Insert(0, notice);
                        }
                        else if (filename == "runway.flt")
                        {
                            notice.Text = CsvHelper.trans("cfg_insert_flt_sections_notice");
                            parentPanel.Children.Insert(0, notice);
                        }
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

                parentPanel.Children.Add(sectiondivider());
            }
        }

        private void InsertSectionsClick(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            StackPanel myPanel = (StackPanel)button.Parent;
            StackPanel parentPanel = (StackPanel)myPanel.Parent;

            if (parentPanel != null)
            {
                string sourceFilename = parentPanel.Tag.ToString();
                string targetFilename = sourceFilename;
                if (!CfgHelper.cfgFileExists(sourceFilename))
                    targetFilename = "aircraft.cfg";

                string[] sections = new string[100];
                int i = 0;

                foreach (var checkboxLabel in getCheckedOptions(parentPanel))
                {
                    sections[i] = checkboxLabel;
                    i++;
                }

                if (i > 0)
                {
                    MessageBoxResult messageBoxResult;

                    if (sourceFilename == "cockpit.cfg")
                        messageBoxResult = MessageBoxResult.Yes;
                    else
                        messageBoxResult = MessageBox.Show(CsvHelper.trans("cfg_insert_sections_notice"),
                            string.Format(i > 1 ? CsvHelper.trans("cfg_insert_sections_header") : CsvHelper.trans("cfg_insert_section_header"), i, targetFilename), System.Windows.MessageBoxButton.YesNoCancel);
                    
                    if (messageBoxResult != MessageBoxResult.Cancel)
                    {
                        CfgHelper.insertSections(aircraftDirectory, sourceFilename, targetFilename, sections, messageBoxResult == MessageBoxResult.Yes);
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
                                TexturesList = AddGroupCheckBox(TexturesList, fileName, Colors.DarkRed, texturesToConvert++);
                        }
                    }
                }
            }

            StackPanel myPanel2 = new StackPanel();

            Button btn = new Button();
            btn = SetButtonAtts(btn);
            btn.Name = "ImageTools";

            Button btn2 = new Button();
            btn2 = SetButtonAtts(btn2);
            btn2.Name = "nvdxt";

            if (texturesToConvert > 0)
            {
                btn.Content = CsvHelper.trans("textures_imagetools");
                btn.Click += ConvertTexturesClick;

                btn2.Content = CsvHelper.trans("textures_nvdxt");
                btn2.Click += ConvertTexturesClick;

                tabTextures.Foreground = new SolidColorBrush(Colors.DarkRed);
                myPanel2.Children.Add(btn);

                TextBlock notice = addTextBlock(CsvHelper.trans("textures_convert_notice"),
                   HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                TexturesList.Children.Insert(0, notice);
            }
            else
            {
                btn2.Content = CsvHelper.trans("textures_no_issues");
                btn2.IsEnabled = false;
                tabTextures.Foreground = new SolidColorBrush(Colors.DarkGreen);
            }

            myPanel2.Children.Add(btn2);
            TexturesList.Children.Add(myPanel2);
            TexturesList.Children.Add(sectiondivider());
        }

        private void ConvertTexturesClick(object sender, EventArgs e)
        {
            fsTabControl.IsEnabled = false;
            ConvertTexturesAsync(sender, getCheckedOptions(TexturesList), ((Button)sender).Name.ToString());
        }

        private async void ConvertTexturesAsync(object sender, List<string> values, string buttonLabel)
        {
            await Task.Run(() => ConvertTexturesTask(sender, values, buttonLabel));
        }

        private async Task ConvertTexturesTask(object sender, List<string> values, string buttonLabel)
        {
            // COUNT
            int count = 0;
            int converted = 0;
            string[] bmp = new string[10000];
            string[] dds = new string[10000];

            foreach (var value in values)
            {
                bmp[count] = projectDirectory + value.ToString().ToLower();
                dds[count] = projectDirectory + value.ToString().ToLower().Replace("bmp", "dds");

                count++;
            }

            // CONVERT
            for (int i = 0; i < count; i++)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    if (sender.GetType() == typeof(Button))
                        ((Button)sender).Content = string.Format(CsvHelper.trans("textures_conversion_process"), i, count);
                });


                if (File.Exists(dds[i]))
                {
                    try { File.Delete(dds[i]); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                if (!File.Exists(dds[i]))
                {
                    Process process = new Process();
                    process.StartInfo.FileName = "cmd.exe";

                    if (buttonLabel.Contains("ImageTool"))
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
                }
            }

            JSONHelper.scanTargetFolder(projectDirectory);
            Application.Current.Dispatcher.Invoke(() => SummaryUpdate());
            Application.Current.Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
        }
        // TEXTURES END

        // MODELS START
        public void SummaryModels()
        {
            ModelAfterburnerList.Children.Clear();
            ModelsList.Children.Clear();

            int modelsWithoutBackup = 0;
            int modelsToConvert = 0;
            int modelsFound = 0;
            int modelsAfterburnerToConvert = 0;
            int modelsAfterburnerFound = 0;

            List<string> warnings = new List<String>();
            List<string> warningsAfterburner = new List<String>();
            List<string> modelFiles;

            ModelBackupButton.Tag = "";

            if (aircraftDirectory != "")
            {
                // CHECK EXTERNAL MODEL FOR AFTERBURNER VALUES
                if (CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]") == "1" && CfgHelper.cfgSectionExists("engines.cfg", "[TURBINEENGINEDATA]"))
                {
                    modelFiles = CfgHelper.getExteriorModels(aircraftDirectory);
                    foreach (string modelFile in modelFiles)
                    {
                        if (modelFile != "" && File.Exists(modelFile))
                        {
                            string fileName = modelFile.Replace(aircraftDirectory, "").Trim('\\');
                            ModelBackupButton.Tag += fileName + ",";

                            if (System.IO.Path.GetFileName(fileName)[0] != '.')
                            {
                                bool hasBackup = File.Exists(System.IO.Path.GetDirectoryName(modelFile) + "\\." + System.IO.Path.GetFileName(modelFile));
                                if (!hasBackup)
                                    modelsWithoutBackup++;

                                modelsAfterburnerFound++;

                                string contents = File.ReadAllText(modelFile);

                                // IF AB VARIABLE NOT SET
                                if (CfgHelper.getCfgValue("afterburner_stages", "engines.cfg", "[TURBINEENGINEDATA]") == "0" ||
                                    CfgHelper.getCfgValue("afterburner_stages", "engines.cfg", "[TURBINEENGINEDATA]", true) == "")
                                {
                                    int topABstage = -1;
                                    bool varsToReplace = false;

                                    var regex = new Regex(@"([A-Za-z])?\s*:\s*TURB\s*ENG(\d)?\s*AFTERBURNER\s*STAGE\s*ACTIVE\s*(:\s*\d)?,\s*number\s*\)\s*(\d)");
                                    foreach (Match match in regex.Matches(contents))
                                    {
                                        foreach (var group in match.Groups)
                                        {
                                            if (group.ToString().Length == 1 && group.ToString().ToUpper()[0] == 'A')
                                                varsToReplace = true;

                                            if (group.ToString().Length == 1 && int.TryParse(group.ToString(), out int num))
                                            {
                                                if (num > 0 && num > topABstage)
                                                    topABstage = num;
                                            }
                                        }
                                    }

                                    // SET AB VARIABLE
                                    Console.WriteLine("Top afterburner stage in model is " + topABstage.ToString());
                                    if (topABstage != -1)
                                    {
                                        CfgHelper.setCfgValue(aircraftDirectory, "afterburner_stages", topABstage.ToString(), "engines.cfg", "[TURBINEENGINEDATA]", false);
                                        CfgHelper.saveCfgFiles(aircraftDirectory, new string[] { "engines.cfg" });
                                        SummaryEngines();
                                    }
                                        

                                    if (varsToReplace)
                                        ModelAfterburnerList = AddGroupCheckBox(ModelAfterburnerList, fileName, Colors.Black, modelsAfterburnerToConvert++);
                                }
                                else
                                {
                                    if (contents.Contains("A:TURB ENG AFTERBURNER") || contents.Contains("A:TURB ENG1 AFTERBURNER"))
                                        ModelAfterburnerList = AddGroupCheckBox(ModelAfterburnerList, fileName, Colors.Black, modelsAfterburnerToConvert++);
                                }

                            }
                        }
                    }
                }

                // CHECK INTERIOR MODELS FOR CLICKABLES
                modelFiles = CfgHelper.getInteriorModels(aircraftDirectory);
                foreach (string modelFile in modelFiles)
                {
                    if (modelFile != "" && File.Exists(modelFile))
                    {
                        string fileName = modelFile.Replace(aircraftDirectory, "").Trim('\\');
                        ModelBackupButton.Tag += fileName + ",";

                        if (System.IO.Path.GetFileName(fileName)[0] != '.')
                        {
                            bool hasBackup = File.Exists(System.IO.Path.GetDirectoryName(modelFile) + "\\." + System.IO.Path.GetFileName(modelFile));
                            if (!hasBackup)
                                modelsWithoutBackup++;

                            modelsFound++;

                            string contents = File.ReadAllText(modelFile);
                            if (contents.Contains("MREC"))
                                ModelsList = AddGroupCheckBox(ModelsList, fileName, Colors.Black, modelsToConvert++);

                            if (!contents.Contains("MDLXMDLH"))
                                warnings.Add(string.Format(CsvHelper.trans("model_format_is_not_compatible"), fileName) + Environment.NewLine);
                        }
                    }
                }

                ModelBackupButton.Tag = ModelBackupButton.Tag.ToString().TrimEnd(',');
            }

            if (CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]") == "1" && CfgHelper.cfgSectionExists("engines.cfg", "[TURBINEENGINEDATA]"))
            {
                StackPanel myPanel1 = new StackPanel();

                Button btn1 = new Button();
                btn1 = SetButtonAtts(btn1);

                if (modelsAfterburnerToConvert > 0)
                {
                    btn1.Content = CsvHelper.trans("model_replace_afterburner");
                    btn1.Click += ReplaceAfterburnerClick;

                    TextBlock notice = addTextBlock(CsvHelper.trans("model_replace_afterburner_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.DarkRed, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    ModelAfterburnerList.Children.Insert(0, notice);
                }
                else
                {
                    btn1.Content = modelsAfterburnerFound > 0 ? CsvHelper.trans("model_no_afterburner") : CsvHelper.trans("model_no_exterior_model");
                    btn1.IsEnabled = false;
                }

                if (modelsAfterburnerFound == 0 || warningsAfterburner.Count > 0 || modelsAfterburnerToConvert > 0)
                    tabModel.Foreground = new SolidColorBrush(Colors.DarkRed);

                myPanel1.Children.Add(btn1);
                ModelAfterburnerList.Children.Add(myPanel1);

                if (warningsAfterburner.Count > 0)
                {
                    foreach (var warning in warningsAfterburner)
                        ModelAfterburnerList.Children.Add(addTextBlock(warning, HorizontalAlignment.Center, VerticalAlignment.Center, Colors.DarkRed, new Thickness(0)));
                }

                ModelAfterburnerList.Children.Add(sectiondivider());
            }

            StackPanel myPanel2 = new StackPanel();

            Button btn2 = new Button();
            btn2 = SetButtonAtts(btn2);

            if (modelsToConvert > 0)
            {
                btn2.Content = CsvHelper.trans("model_remove_clickable");
                btn2.Click += RemoveSwitchesClick;

                TextBlock notice = addTextBlock(CsvHelper.trans("model_remove_clickable_notice"),
                   HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                ModelsList.Children.Insert(0, notice);
            }
            else
            {
                if (modelsFound <= 0)
                    MessageBox.Show(CsvHelper.trans("model_no_interior_model_warning"), CsvHelper.trans("model_no_interior_model"));


                btn2.Content = modelsFound > 0 ? CsvHelper.trans("model_no_clickable_switches") : CsvHelper.trans("model_no_interior_model");
                btn2.IsEnabled = false;
            }

            if (modelsFound == 0 || warnings.Count > 0 /*|| modelsToConvert > 0 && modelsWithoutBackup > 0*/)
                tabModel.Foreground = new SolidColorBrush(Colors.DarkRed);

            myPanel2.Children.Add(btn2);
            ModelsList.Children.Add(myPanel2);

            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                    ModelsList.Children.Add(addTextBlock(warning, HorizontalAlignment.Center, VerticalAlignment.Center, Colors.DarkRed, new Thickness(0)));
            }

            ModelsList.Children.Add(sectiondivider());
        }

        private void ReplaceAfterburnerClick(object sender, EventArgs e)
        {
            fsTabControl.IsEnabled = false;
            ReplaceAfterburnerClickAsync(sender, getCheckedOptions(ModelAfterburnerList));
        }

        private async void ReplaceAfterburnerClickAsync(object sender, List<string> values)
        {
            await Task.Run(() => ReplaceAfterburnerClickTask(sender, values));
        }

        private async Task ReplaceAfterburnerClickTask(object sender, List<string> values)
        {
            // COUNT
            foreach (var checkboxLabel in values)
            {
                string mainFile = aircraftDirectory + "\\" + checkboxLabel;

                if (File.Exists(mainFile))
                {
                    //(A:TURB ENG AFTERBURNER STAGE ACTIVE:1,number)
                    //(A:TURB ENG1 AFTERBURNER,bool)
                    //(A:TURB ENG AFTERBURNER:1,bool)
                    //(A:TURB ENG AFTERBURNER PCT ACTIVE,percent)

                    Application.Current.Dispatcher.Invoke(() => {
                        if (sender.GetType() == typeof(Button))
                            ((Button)sender).Content = string.Format(CsvHelper.trans("models_processing_file"), System.IO.Path.GetFileName(mainFile));
                    });

                    byte[] cache = new byte[22];


                    byte[] buf = File.ReadAllBytes(mainFile);
                    for (int i = 0; i < buf.Length; i++)
                    {
                        for (int k = 0; k < cache.Length - 1; k++)
                        {
                            cache[k] = cache[k + 1];
                        }
                        cache[cache.Length - 1] = buf[i];

                        System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                        string s = enc.GetString(cache).ToUpper();

                        if (s == "A:TURB ENG AFTERBURNER" || s == "A:TURB ENG1 AFTERBURNE" || s == "A:TURB ENG2 AFTERBURNE" || s == "A:TURB ENG3 AFTERBURNE" || s == "A:TURB ENG4 AFTERBURNE" ||
                            s == "A :TURB ENG AFTERBURNE" || s == "A: TURB ENG AFTERBURNE" || s == "A : TURB ENG AFTERBURN")
                            buf[i - 21] = (byte)'L';
                    }

                    // MAKE MDL BACKUP
                    backUpFile(mainFile);

                    // CLEAR AIRCRAFT CACHE
                    deleteCVCfolder();

                    try { File.WriteAllBytes(mainFile, buf); }
                    catch (Exception ex) { MessageBox.Show("Can't save MDL file" + Environment.NewLine + "Error: " + ex.Message); }

                }
            }

            Application.Current.Dispatcher.Invoke(() => SummaryUpdate());
            Application.Current.Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
        }

        private void RemoveSwitchesClick(object sender, RoutedEventArgs e)
        {
            // COUNT
            MessageBoxResult messageBoxResult = MessageBox.Show(CsvHelper.trans("model_remove_clickable_warning"), CsvHelper.trans("model_remove_clickable_warning_header"), System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                foreach (var checkboxLabel in getCheckedOptions(ModelsList))
                {
                    string mainFile = aircraftDirectory + "\\" + checkboxLabel;

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
                            for (int k = 0; k < cache.Length - 1; k++)
                            {
                                cache[k] = cache[k + 1];
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
                                //Console.WriteLine(BitConverter.ToString(cache));

                            }
                            else if (MRECpos > 0 && i == MRECpos + 7) // CAPTURE MREC SIZE
                            {
                                if (BitConverter.IsLittleEndian)
                                    Array.Reverse(cache);
                                MRECsizeInt = cache[3] | (cache[2] << 8) | (cache[1] << 16) | (cache[0] << 24);
                                //Console.WriteLine(BitConverter.ToString(cache));
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
                            try { File.WriteAllBytes(mainFile, buf); }
                            catch (Exception ex) { MessageBox.Show("Can't save MDL file" + Environment.NewLine + "Error: " + ex.Message); }
                        }
                        else
                        {
                            MessageBox.Show(string.Format(CsvHelper.trans("model_clickable_removal_failed"), checkboxLabel));
                        }
                    }
                }
            }

            SummaryUpdate();
        }
        // MODELS END

        // SOUND START
        public void SummarySound()
        {
            SoundList.Children.Clear();

            //int soundsToDisable = 0;
            int soundsFound = 0;

            if (aircraftDirectory != "")
            {
                List<string> soundFiles = CfgHelper.getSounds(aircraftDirectory);

                if (soundFiles.Count() > 0)
                {
                    foreach (string soundFile in soundFiles)
                    {
                        if (soundFile != "")
                        {
                            /*if (soundFile[0] != '-' && (soundFile.Contains("[GEAR_DOWN]") || soundFile.Contains("[GEAR_UP]")))
                            {
                                SoundList = AddGroupCheckBox(SoundList, soundFile, Colors.DarkRed, soundsFound++, "", true);
                                soundsToDisable++;
                            }
                            else*/
                            {
                                if (soundFile[0] == '-')
                                    SoundList = AddGroupCheckBox(SoundList, soundFile.Substring(1), Colors.Black, soundsFound++, "", false);
                                else
                                    SoundList = AddGroupCheckBox(SoundList, soundFile, Colors.Black, soundsFound++, "", true);
                            }
                        }
                    }

                    TextBlock notice = addTextBlock(CsvHelper.trans("sounds_toggle_notice"),
                       HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                    SoundList.Children.Insert(0, notice);
                }
            }

            StackPanel myPanel2 = new StackPanel();

            Button btn = new Button();
            btn = SetButtonAtts(btn);

            if (soundsFound > 0)
            {
                btn.Content = CsvHelper.trans("sounds_update");
                btn.Click += UpdateSoundsClick;
            }
            else
            {
                btn.Content = CsvHelper.trans("sounds_not_found");
                btn.IsEnabled = false;
            }

            /*if (soundsToDisable > 0)
                tabSound.Foreground = new SolidColorBrush(Colors.DarkRed);*/

            myPanel2.Children.Add(btn);
            SoundList.Children.Add(myPanel2);

            SoundList.Children.Add(sectiondivider());

            if (Directory.Exists(aircraftDirectory + "\\sound\\") && !File.Exists(aircraftDirectory + "\\sound\\sound.xml") &&
                File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\sound.xml"))
            {
                StackPanel myPanel3 = new StackPanel();

                TextBlock volumeCorr = addTextBlock(string.Format(CsvHelper.trans("sounds_tone_volume"), "0", "30", "100"), HorizontalAlignment.Left, VerticalAlignment.Center, Colors.Black,
                    new Thickness(0, 10, 0, 0));
                myPanel3.Children.Add(volumeCorr);

                if (this.FindName("VarioVolimeSlider") != null)
                    UnregisterName("VarioVolimeSlider");
                Slider varioVolimeSlider = new Slider();
                RegisterName("VarioVolimeSlider", varioVolimeSlider);
                varioVolimeSlider.Value = 30;
                varioVolimeSlider.Minimum = 0;
                varioVolimeSlider.Maximum = 100;
                varioVolimeSlider.AutoToolTipPlacement = AutoToolTipPlacement.TopLeft;
                varioVolimeSlider.AutoToolTipPrecision = 1;
                myPanel3.Children.Add(varioVolimeSlider);

                Button btn1 = new Button();
                btn1 = SetButtonAtts(btn1);
                btn1.Content = CsvHelper.trans("sounds_insert_tone");
                btn1.Click += InsertVarioClick;
                myPanel3.Children.Add(btn1);
                SoundList.Children.Add(myPanel3);
            } else if (Directory.Exists(aircraftDirectory + "\\sound\\") && File.Exists(aircraftDirectory + "\\sound\\sound.xml"))
            {
                StackPanel myPanel3 = new StackPanel();
                Button btn1 = new Button();
                btn1 = SetButtonAtts(btn1);
                btn1.Content = CsvHelper.trans("sounds_remove_tone");
                btn1.Click += DeleteSoundXmlClick;
                myPanel3.Children.Add(btn1);
                SoundList.Children.Add(myPanel3);
            }
        }

        public void UpdateSoundsClick(object sender, RoutedEventArgs e)
        {
            CfgHelper.updateSounds(aircraftDirectory, getCheckedOptions(SoundList));
            SummaryUpdate();
        }

        public void DeleteSoundXmlClick(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(aircraftDirectory + "\\sound\\") && File.Exists(aircraftDirectory + "\\sound\\sound.xml"))
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(CsvHelper.trans("sounds_remove_notice"), CsvHelper.trans("sounds_warning"), System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(aircraftDirectory + "\\sound\\sound.xml");
                    }
                    catch { }

                    JSONHelper.scanTargetFolder(projectDirectory);
                    SummaryUpdate();
                }
            }
        }

        public void InsertVarioClick(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(aircraftDirectory + "\\sound\\") && !File.Exists(aircraftDirectory + "\\sound\\sound.xml") && File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\sound.xml"))
            {
                try
                {
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\sound.xml", aircraftDirectory + "\\sound\\sound.xml");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_solid.wav", aircraftDirectory + "\\sound\\variotone_solid.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle100.wav", aircraftDirectory + "\\sound\\variotone_cycle100.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle200.wav", aircraftDirectory + "\\sound\\variotone_cycle200.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle250.wav", aircraftDirectory + "\\sound\\variotone_cycle250.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle300.wav", aircraftDirectory + "\\sound\\variotone_cycle300.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle400.wav", aircraftDirectory + "\\sound\\variotone_cycle400.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle500.wav", aircraftDirectory + "\\sound\\variotone_cycle500.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle550.wav", aircraftDirectory + "\\sound\\variotone_cycle550.wav");
                    File.Copy(AppDomain.CurrentDomain.BaseDirectory + "\\varioSound\\variotone_cycle600.wav", aircraftDirectory + "\\sound\\variotone_cycle600.wav");

                    float varioVolime = 30;
                    var varioVolimeSlider = FindName("VarioVolimeSlider");

                    if (varioVolimeSlider != null)
                        varioVolime = (float)((Slider)varioVolimeSlider).Value;

                    try { File.WriteAllText(aircraftDirectory + "\\sound\\sound.xml", File.ReadAllText(aircraftDirectory + "\\sound\\sound.xml").Replace("[VOLUME]", varioVolime.ToString())); }
                    catch (Exception ex) { MessageBox.Show("Can't save sound.xml file" + Environment.NewLine + "Error: " + ex.Message); }
                }
                catch { }

                JSONHelper.scanTargetFolder(projectDirectory);
                SummaryUpdate();
            }
        }
        // SOUND END

        // PANEL START
        public void SummaryPanel()
        {
            PanelsList.Children.Clear();
            CabsList.Children.Clear();

            int panelsWithoutBackup = 0;
            int panelsToConvert = 0;

            int cabsWithoutBackup = 0;
            int cabsToConvert = 0;

            PanelBackupButton.Tag = "";

            if (aircraftDirectory != "")
            {
                var panelFiles = Directory.EnumerateFiles(aircraftDirectory, "panel.cfg", SearchOption.AllDirectories);
                foreach (string panelFile in panelFiles)
                {
                    string fileName = panelFile.Replace(aircraftDirectory, "").Trim('\\');
                    if (panelFile != "" && fileName[0] != '.' && System.IO.Path.GetFileName(panelFile)[0] != '.')
                    {
                        PanelBackupButton.Tag += fileName + ",";

                        bool PanelHasBackup = File.Exists(System.IO.Path.GetDirectoryName(panelFile) + "\\." + System.IO.Path.GetFileName(panelFile));
                        if (!PanelHasBackup)
                            panelsWithoutBackup++;

                        PanelsList = AddGroupCheckBox(PanelsList, fileName, PanelHasBackup ? Colors.Black : Colors.DarkRed, panelsToConvert++);

                        var cabFiles = Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(panelFile), "*.cab", SearchOption.TopDirectoryOnly);
                        foreach (string cabFile in cabFiles)
                        {
                            string cabFileName = cabFile.Replace(aircraftDirectory, "").Trim('\\');
                            if (cabFile != "" && System.IO.Path.GetFileName(cabFileName)[0] != '.')
                            {
                                PanelBackupButton.Tag += cabFileName + ",";

                                bool cabHasBackup = File.Exists(System.IO.Path.GetDirectoryName(cabFile) + "\\." + System.IO.Path.GetFileName(cabFile));
                                if (!cabHasBackup)
                                    cabsWithoutBackup++;

                                CabsList = AddGroupCheckBox(CabsList, cabFileName, cabHasBackup ? Colors.Black : Colors.DarkRed, cabsToConvert++);
                            }
                        }

                        continue;
                    }
                }

                PanelBackupButton.Tag = PanelBackupButton.Tag.ToString().TrimEnd(',');
            }

            StackPanel myPanel1 = new StackPanel();

            // CHECK FOR DLL FILES
            if (Directory.GetFiles(aircraftDirectory, "*.dll", SearchOption.AllDirectories).Length > 0 || Directory.GetFiles(aircraftDirectory, "*.gau", SearchOption.AllDirectories).Length > 0)
            {
                TextBlock dllWarning = new TextBlock();
                dllWarning.Text = CsvHelper.trans("panel_dll_detected");
                dllWarning.Foreground = new SolidColorBrush(Colors.DarkRed);
                myPanel1.Children.Add(dllWarning);
                tabPanel.Foreground = new SolidColorBrush(Colors.DarkRed);
            }

            Button btn1 = new Button();
            btn1 = SetButtonAtts(btn1);
            if (cabsToConvert > 0)
            {
                TextBlock extractGaugesNotice = addTextBlock(CsvHelper.trans("panel_extract_ac_cabs_notice"),
                    HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                CabsList.Children.Insert(0, extractGaugesNotice);

                btn1.Content = CsvHelper.trans("panel_extract_gauges");
                btn1.Click += extractCabClick;
            } else
            {
                btn1.Content = CsvHelper.trans("panel_no_panel");
                btn1.IsEnabled = false;
            }
            myPanel1.Children.Add(btn1);
            /*----*/myPanel1.Children.Add(sectiondivider());

            Button btn3 = new Button();
            btn3 = SetButtonAtts(btn3);

            TextBlock extractGaugesNotice2 = addTextBlock(string.Format(CsvHelper.trans("panel_extract_fsx_cabs_notice"),
                System.IO.Path.GetDirectoryName(projectDirectory.TrimEnd('\\')) + "\\legacy-vcockpits-instruments\\.FSX\\"),
                HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
            myPanel1.Children.Add(extractGaugesNotice2);

            btn3.Content = CsvHelper.trans("panel_extract_fsx_resources");
            btn3.Click += extractDefaultCabsClick;

            myPanel1.Children.Add(btn3);

            /*----*/myPanel1.Children.Add(sectiondivider());

            if (cabsToConvert > 0 || cabsWithoutBackup > 0)
                tabPanel.Foreground = new SolidColorBrush(Colors.DarkRed);

            CabsList.Children.Add(myPanel1);


            StackPanel myPanel4 = new StackPanel();
            myPanel4.Margin = new Thickness(0, 10, 0, 5);

            Button btn4 = new Button();
            btn4 = SetButtonAtts(btn4);
            TextBlock brokenSwitches = addTextBlock(CsvHelper.trans("panel_broken_switches_notice"),
                HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 0), TextWrapping.Wrap, 600);
            myPanel4.Children.Add(brokenSwitches);

            btn4.Content = CsvHelper.trans("panel_broken_switches");
            btn4.Tag = "https://forums.flightsimulator.com/t/make-legacy-cockpit-buttons-work-again/325942";
            btn4.Click += Button_RequestNavigate;

            myPanel4.Children.Add(btn4);
            /*----*/myPanel4.Children.Add(sectiondivider());
            CabsList.Children.Add(myPanel4);


            StackPanel myPanel2 = new StackPanel();
            myPanel2.Margin = new Thickness(0, 10, 0, 5);

            Button btn2 = new Button();
            btn2 = SetButtonAtts(btn2);

            if (panelsToConvert > 0)
            {
                btn2.Content = CsvHelper.trans("panel_import_gauges");
                btn2.Click += importPanelGaugeClick;

                myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_force_gauge_background"), 600, HorizontalAlignment.Left, "ForceBackground");
                myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_transparent_mask"), 600, HorizontalAlignment.Left, "TransparentMask");
                myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_preserve_size"), 600, HorizontalAlignment.Left, "PreservePanelSize");
                myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_scale_up_gauge"), 600, HorizontalAlignment.Left, "ScaleUpGauge");
                myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_taxi_lights_switch"), 600, HorizontalAlignment.Left, "TaxiLightsSwitch");
                string engine_type = CfgHelper.getCfgValue("engine_type", "engines.cfg", "[GENERALENGINEDATA]");
                string afterburner_available = CfgHelper.getCfgValue("afterburner_stages", "engines.cfg", "[TURBINEENGINEDATA]", true);
                if (engine_type == "1" && afterburner_available != "" && afterburner_available != "0")
                    myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_afterburner_vars"), 600, HorizontalAlignment.Left, "AfterburnerVars");
                myPanel2 = AddSingleCheckBox(myPanel2, CsvHelper.trans("panel_ignore_errors"), 600, HorizontalAlignment.Left, "IgnorePanelErrors");

                TextBlock gammaCorr = addTextBlock(string.Format(CsvHelper.trans("panel_gamma_correction"), "0", "1", "2"), HorizontalAlignment.Left, VerticalAlignment.Center, Colors.Black,
                    new Thickness(0, 10, 0, 0));
                myPanel2.Children.Add(gammaCorr);

                if (this.FindName("GammaSlider") != null)
                    UnregisterName("GammaSlider");
                Slider gammaSlider = new Slider();
                RegisterName("GammaSlider", gammaSlider);
                gammaSlider.Value = gammaSliderPos;
                gammaSlider.Minimum = 0.1;
                gammaSlider.Maximum = 2.0;
                gammaSlider.AutoToolTipPlacement = AutoToolTipPlacement.TopLeft;
                gammaSlider.AutoToolTipPrecision = 1;
                myPanel2.Children.Add(gammaSlider);

                TextBlock extractGaugesNotice3 = addTextBlock(CsvHelper.trans("panel_convert_gauges_notice"),
                    HorizontalAlignment.Stretch, VerticalAlignment.Top, Colors.Black, new Thickness(0, 10, 0, 10), TextWrapping.Wrap, 600);
                PanelsList.Children.Insert(0, extractGaugesNotice3);
            }
            else
            {
                btn2.Content = CsvHelper.trans("panel_not_found");
                btn2.IsEnabled = false;
            }

            if (panelsToConvert > 0 && panelsWithoutBackup > 0)
                tabPanel.Foreground = new SolidColorBrush(Colors.DarkRed);

            myPanel2.Children.Add(btn2);
            PanelsList.Children.Add(myPanel2);

            /*----*/PanelsList.Children.Add(sectiondivider());
        }

        // AC CAB EXTRACT
        private void extractCabClick(object sender, EventArgs e)
        {
            fsTabControl.IsEnabled = false;
            extractAcCabAsync(sender, getCheckedOptions(CabsList));
        }

        private async void extractAcCabAsync(object sender, List<string> values)
        {
            await Task.Run(() => extractAcCabTask(sender, values));
        }

        private async Task extractAcCabTask(object sender, List<string> values)
        {
            foreach (var value in values)
            {
                string mainFile = aircraftDirectory + "\\" + value;
                string backupFile = System.IO.Path.GetDirectoryName(mainFile) + "\\." + System.IO.Path.GetFileName(mainFile);

                if (File.Exists(mainFile))
                {
                    Application.Current.Dispatcher.Invoke(() => {
                        if (sender.GetType() == typeof(Button))
                            ((Button)sender).Content = string.Format(CsvHelper.trans("panel_extracting_cab"), System.IO.Path.GetFileName(mainFile));
                    });

                    try
                    {
                        string extractDirectory = System.IO.Path.GetDirectoryName(mainFile) + "\\." + System.IO.Path.GetFileNameWithoutExtension(mainFile);
                        if (!Directory.Exists(extractDirectory))
                            Directory.CreateDirectory(extractDirectory);

                        if (File.Exists(backupFile))
                            File.Delete(backupFile);

                        CabInfo cab = new CabInfo(mainFile);
                        cab.Unpack(extractDirectory);

                        File.Move(mainFile, backupFile);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception " + e.Message);
                    }
                }
            }

            JSONHelper.scanTargetFolder(projectDirectory);
            Application.Current.Dispatcher.Invoke(() => SummaryUpdate());
            Application.Current.Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
        }

        // DEFAULT CAB EXTRACT
        private void extractDefaultCabsClick(object sender, RoutedEventArgs e)
        {
            string selectedPath = HKLMRegistryHelper.GetRegistryValue("SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\10.0\\", "SetupPath", RegistryView.Registry32);

            if (selectedPath != Environment.SpecialFolder.MyDocuments.ToString())
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(string.Format(CsvHelper.trans("panel_current_fsx_path"), selectedPath) + Environment.NewLine + Environment.NewLine +
                    CsvHelper.trans("panel_fsx_extract_notice1") + Environment.NewLine +
                    CsvHelper.trans("panel_fsx_extract_notice2") + Environment.NewLine +
                    CsvHelper.trans("panel_fsx_extract_notice3"), CsvHelper.trans("panel_fsx_header"), MessageBoxButton.YesNoCancel);
                if (messageBoxResult == MessageBoxResult.Cancel)
                {
                    selectedPath = "";
                }
                else if (messageBoxResult == MessageBoxResult.No)
                {
                    selectedPath = FileDialogHelper.getFolderPath(HKLMRegistryHelper.GetRegistryValue("SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\10.0\\", "SetupPath", RegistryView.Registry32));
                }
            }

            if (!String.IsNullOrEmpty(selectedPath))
            {
                if (Directory.Exists(selectedPath + "\\Gauges\\") && Directory.Exists(selectedPath + "\\SimObjects\\Airplanes\\"))
                {
                    fsTabControl.IsEnabled = false;
                    extractDefaultCabsAsync(sender, selectedPath);
                }
                else
                {
                    MessageBox.Show(string.Format(CsvHelper.trans("panel_gauges_not_found"), selectedPath));
                }
            }
        }
        private async void extractDefaultCabsAsync(object sender, string selectedPath)
        {
            Console.WriteLine("Extracting FSX cabs");
            await Task.Run(() => extractDefaultCabsTask(sender, selectedPath));
        }

        private async Task extractDefaultCabsTask(object sender, string selectedPath)
        {
            var cabFiles = Directory.EnumerateFiles(selectedPath + "\\Gauges\\", "*.cab", SearchOption.AllDirectories);
            var cabFiles2 = Directory.EnumerateFiles(selectedPath + "\\SimObjects\\Airplanes\\", "*.cab", SearchOption.AllDirectories);

            IEnumerable<string> combined = cabFiles.Concat(cabFiles2);

            foreach (string cabFile in combined)
            {
                if (File.Exists(cabFile))
                {
                    Console.WriteLine("Extracting " + cabFile);

                    string extractDirectory = System.IO.Path.GetDirectoryName(projectDirectory.TrimEnd('\\')) + "\\legacy-vcockpits-instruments\\.FSX\\" + System.IO.Path.GetFileNameWithoutExtension(cabFile);

                    if (!Directory.Exists(extractDirectory))
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            if (sender.GetType() == typeof(Button))
                                ((Button)sender).Content = string.Format(CsvHelper.trans("panel_extracting_cab"), System.IO.Path.GetFileName(cabFile));
                        });

                        Directory.CreateDirectory(extractDirectory);

                        try
                        {
                            CabInfo cab = new CabInfo(cabFile);
                            cab.Unpack(extractDirectory);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Exception " + e.Message);
                        }

                        await Task.Delay(100);
                    }
                    else
                    {
                        Console.WriteLine("Already extracted, skipping " + cabFile);
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(() => SummaryUpdate());
            Application.Current.Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
            //Application.Current.Dispatcher.Invoke(() => extractingCabs = false);
            Application.Current.Dispatcher.Invoke(() => btnScan.Content = CsvHelper.trans("btnScan"));
        }


        // PANEL IMPORT
        private void importPanelGaugeClick(object sender, EventArgs e)
        {
            gammaSliderPos = FindName("GammaSlider") != null ? (float)((Slider)FindName("GammaSlider")).Value : 1;

            fsTabControl.IsEnabled = false;
            importPanelGaugeAsync(sender, getCheckedOptions(PanelsList),
                new float[] {
                    gammaSliderPos,
                    FindName("ForceBackground") != null && ((CheckBox)FindName("ForceBackground")).IsChecked == true ? 1 : 0,
                    FindName("IgnorePanelErrors") != null && ((CheckBox)FindName("IgnorePanelErrors")).IsChecked == true ? 1 : 0,
                    FindName("PreservePanelSize") != null && ((CheckBox)FindName("PreservePanelSize")).IsChecked == true ? 1 : 0,
                    FindName("TaxiLightsSwitch") != null && ((CheckBox)FindName("TaxiLightsSwitch")).IsChecked == true ? 1 : 0,
                    FindName("TransparentMask") != null && ((CheckBox)FindName("TransparentMask")).IsChecked == true ? 1 : 0,
                    FindName("ScaleUpGauge") != null && ((CheckBox)FindName("ScaleUpGauge")).IsChecked == true ? 1 : 0,
                    FindName("AfterburnerVars") != null && ((CheckBox)FindName("AfterburnerVars")).IsChecked == true ? 1 : 0,
                }
            );
        }

        private async void importPanelGaugeAsync(object sender, List<string> values, float[] atts)
        {
            await Task.Run(() => importPanelGaugeTask(sender, values, atts));

            JSONHelper.scanTargetFolder(projectDirectory);
            Application.Current.Dispatcher.Invoke(() => SummaryUpdate());
            Application.Current.Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
        }

        private async Task importPanelGaugeTask(object sender, List<string> values, float[] atts)
        {
            foreach (var value in values)
            {
                XmlHelper.insertFsxGauge(this, sender, aircraftDirectory, projectDirectory, value, atts, CfgHelper, FsxVarHelper, JSONHelper);
            }
        }

        private void TextExpressionClick(object sender, RoutedEventArgs e)
        {
            TextExpressionField.Visibility = Visibility.Visible;
            TextExpressionResult.Visibility = Visibility.Visible;
            TextExpressionResult.Text = FsxVarHelper.fsx2msfsSimVar(TextExpressionField.Text, new xmlHelper());
        }
        // PANEL END

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
                MessageBox.Show(string.Format(CsvHelper.trans("directory_not_found"), projectDirectory + @"\SimObjects\Airplanes\"));
                return "";
            }
        }

        public Button SetButtonAtts(Button btn, bool large = true)
        {
            if (large)
            {
                btn.MinHeight = 30;
                btn.FontSize = 20;
                btn.Margin = new Thickness(5, 20, 5, 20);
                btn.Padding = new Thickness(5, 5, 5, 5);
                btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                btn.Width = double.NaN;
            }

            btn.FontFamily = new FontFamily("Arial Black");
            btn.VerticalAlignment = VerticalAlignment.Top;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(90, 90, 255));
            btn.Background = new SolidColorBrush(Color.FromRgb(190, 221, 255));
            if (btn.Name != null && !String.IsNullOrEmpty(btn.Name.ToString()))
                btn.Content = CsvHelper.trans(btn.Name.ToString());

            return btn;
        }

        public TextBlock SetHeaderAtts (TextBlock header, bool large = true)
        {
            if (header.Name != null && !String.IsNullOrEmpty(header.Name.ToString()))
            {
                if (header.ToolTip != null)
                    header.ToolTip = CsvHelper.trans(header.Name.ToString());
                else
                    header.Text = CsvHelper.trans(header.Name.ToString());
            }

            return header;
        }

            private void CfgBackupClick(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;

            string filenames = btn.Tag.ToString();
            if (!btn.Tag.ToString().Contains(','))
                filenames += ',';

            foreach (var filename in filenames.Split(','))
            {
                if (!String.IsNullOrEmpty(filename))
                {
                    string mainFile = aircraftDirectory + "\\" + filename;
                    string backupFile = System.IO.Path.GetDirectoryName(mainFile) + "\\." + System.IO.Path.GetFileName(mainFile);

                    if (File.Exists(backupFile))
                    {
                        MessageBoxResult messageBoxResult = MessageBox.Show(string.Format(CsvHelper.trans("restore_backup_notice"), filename), string.Format(CsvHelper.trans("restore_backup_header"), filename), MessageBoxButton.YesNo);
                        if (messageBoxResult == MessageBoxResult.Yes)
                        {
                            CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;

                            try
                            {
                                File.Delete(mainFile);
                                File.Copy(backupFile, mainFile);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                    else if (File.Exists(mainFile))
                    {
                        CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;
                        File.Copy(mainFile, backupFile);
                        btn.Content = CsvHelper.trans("restore_backup");
                    }
                }
            }

            CfgHelper.processCfgfiles(aircraftDirectory + "\\", true);
            SummaryUpdate();

            JSONHelper.scanTargetFolder(projectDirectory);
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

        public TextBlock addTextBlock(string text, HorizontalAlignment ha, VerticalAlignment va, Color clr, Thickness margin, TextWrapping wrapping = TextWrapping.NoWrap, int width = 0 )
        {
            TextBlock myBlock = new TextBlock();
            myBlock.HorizontalAlignment = ha;
            myBlock.VerticalAlignment = va;
            myBlock.Text = text;
            myBlock.Foreground = new SolidColorBrush(clr);
            myBlock.Margin = margin;
            myBlock.TextWrapping = wrapping;

            if (width > 0)
                myBlock.Width = width;

            return myBlock;
        }

        private void BtnOpenTargetFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                string defaultPath = !String.IsNullOrEmpty(communityPath) ? communityPath : HKLMRegistryHelper.GetRegistryValue("SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\11.0\\", "CommunityPath");
                dialog.InitialDirectory = defaultPath;
                dialog.IsFolderPicker = true;
                dialog.RestoreDirectory = (String.IsNullOrEmpty(defaultPath) || defaultPath == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) ? true : false;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    TargetFolder = dialog.FileName + "\\";
                    btnTargetFolderPath.Text = string.Format(CsvHelper.trans("destination_path"), TargetFolder + XmlHelper.sanitizeString(PackageDir.Text.ToLower().Trim()) + "\\");
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void TextBlockTargetFile_Input(object sender, RoutedEventArgs e)
        {
            if (TargetFolder != "")
            {
                btnTargetFolderPath.Text = string.Format(CsvHelper.trans("destination_path"), TargetFolder + XmlHelper.sanitizeString(PackageDir.Text.ToLower().Trim()) + "\\");
            }
        }

        private void BtnOpenSourceFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                string defaultPath = HKLMRegistryHelper.GetRegistryValue("SOFTWARE\\Microsoft\\microsoft games\\Flight Simulator\\10.0\\", "SetupPath", RegistryView.Registry32);
                dialog.InitialDirectory = defaultPath;
                dialog.IsFolderPicker = true;
                dialog.RestoreDirectory = (String.IsNullOrEmpty(defaultPath) || defaultPath == Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)) ? true : false;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string selectedPath = dialog.FileName;

                    if (File.Exists(selectedPath + "\\aircraft.cfg"))
                    {
                        SourceFolder = selectedPath + "\\";
                        btnSourceFolderPath.Text = string.Format(CsvHelper.trans("source_path"), SourceFolder);

                        // POPULATE INPUT FIELDS
                        string content = File.ReadAllText(selectedPath + "\\aircraft.cfg");
                        List<msfsLegacyImporter.cfgHelper.CfgLine> panelLines = CfgHelper.readCSV(content + "\r\n[]");
                        var title = panelLines.Find(x => x.Name == "title");
                        if (title != null) { PackageTitle.Text = title.Value.Trim('"').Trim(); }

                        var ui_manufacturer = panelLines.Find(x => x.Name == "ui_manufacturer");
                        if (ui_manufacturer != null) { PackageManufacturer.Text = ui_manufacturer.Value.Trim('"').Trim(); }

                        var ui_createdby = panelLines.Find(x => x.Name == "ui_createdby");
                        if (ui_createdby != null) { PackageAuthor.Text = ui_createdby.Value.Trim('"').Trim(); }

                        var sim = panelLines.Find(x => x.Name == "sim");
                        if (sim != null) { PackageDir.Text = sim.Value.Trim('"').Trim(); }
                    }
                    else
                    {
                        SourceFolder = "";
                        MessageBox.Show(string.Format(CsvHelper.trans("aircraft_not_found_in"), selectedPath));
                    }
                }
            } catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void BtnImportSubmit_Click(object sender, RoutedEventArgs e)
        {
            // VALIDATE FIELDS
            if (TargetFolder == "" || SourceFolder == "")
                MessageBox.Show(CsvHelper.trans("import_folders_not_selected"));
            else if (String.IsNullOrWhiteSpace(PackageTitle.Text) || String.IsNullOrWhiteSpace(PackageDir.Text) || String.IsNullOrWhiteSpace(PackageManufacturer.Text) || String.IsNullOrWhiteSpace(PackageAuthor.Text) ||
                String.IsNullOrWhiteSpace(PackageVer1.Text) || String.IsNullOrWhiteSpace(PackageVer2.Text) || String.IsNullOrWhiteSpace(PackageVer3.Text) ||
                String.IsNullOrWhiteSpace(PackageMinVer1.Text) || String.IsNullOrWhiteSpace(PackageMinVer2.Text) || String.IsNullOrWhiteSpace(PackageMinVer3.Text))
                MessageBox.Show(CsvHelper.trans("import_fields_are_empty"));
            else if (Directory.Exists(TargetFolder + XmlHelper.sanitizeString(PackageDir.Text.ToLower().Trim()) + "\\"))
            {
                MessageBox.Show(string.Format(CsvHelper.trans("import_already_exists"), TargetFolder + XmlHelper.sanitizeString(PackageDir.Text.ToLower().Trim())));
            } else if (SourceFolder == TargetFolder + XmlHelper.sanitizeString(PackageDir.Text.ToLower().Trim()) + "\\")
            {
                MessageBox.Show(CsvHelper.trans("import_same_directory"));
            }
            else
            {
                string[] data = new string[] { "", "AIRCRAFT", PackageTitle.Text, PackageManufacturer.Text, PackageAuthor.Text,
            PackageVer1.Text + "." + PackageVer2.Text + "." + PackageVer3.Text, PackageMinVer1.Text + "." + PackageMinVer2.Text + "." + PackageMinVer3.Text, "" };

                JSONHelper.createManifest(this, SourceFolder, TargetFolder + XmlHelper.sanitizeString(PackageDir.Text.ToLower().Trim()) + "\\", data);
                showInitPage("imported");
            }
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            int filesCount = JSONHelper.scanTargetFolder(projectDirectory);

            if (filesCount > 0)
            {
                btnScan.Content = string.Format(CsvHelper.trans("json_files_added"), filesCount);
            } else
            {
                btnScan.Content = CsvHelper.trans("json_no_files_added");
            }

            var t = Task.Run(async delegate
            {
                await Task.Delay(2000);
                Dispatcher.Invoke(() => btnScan.Content = CsvHelper.trans("btnScan"));
                return;
            });
        }

        private void Hyperlink_RequestNavigate(object sender,
                                               System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString().Contains("//") ? e.Uri.AbsoluteUri : e.Uri.ToString());
        }
        private void Button_RequestNavigate(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            System.Diagnostics.Process.Start(btn.Tag.ToString());
        }

        public StackPanel AddGroupCheckBox(StackPanel mainPanel, string content, Color color, int index = 1, string tag = "", bool isChecked = false)
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
                ToggleCheckBox.Content = CsvHelper.trans("toggle_all");
                ToggleCheckBox.Tag = CsvHelper.trans("toggle_all");
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
            checkBox.IsChecked = isChecked;
            if (tag != "")
                checkBox.Tag = tag;
            myPanel.Children.Add(checkBox);
            mainPanel.Children.Add(myPanel);

            return mainPanel;
        }

        public StackPanel AddSingleCheckBox(StackPanel mainPanel, string content, int width = 0, HorizontalAlignment alignment = HorizontalAlignment.Left, string name = "")
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Content = content;
            if (width > 0)
                checkBox.MaxWidth = width;
            checkBox.HorizontalAlignment = alignment;

            if (!string.IsNullOrEmpty(name))
            {
                if (FindName(name) != null)
                    UnregisterName(name);
                RegisterName(name, checkBox);
            }

            mainPanel.Children.Add(checkBox);

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
                            if (checkBox.GetType() == typeof(CheckBox))
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
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        MessageBox.Show(CsvHelper.trans("cvt_remove_failed"));
                    }
                }
            }
        }

        public void backUpFile(string mainFile, bool force = false)
        {
            string backupFile = System.IO.Path.GetDirectoryName(mainFile) + "\\." + System.IO.Path.GetFileName(mainFile);
            CfgHelper.lastChangeTimestamp = DateTime.UtcNow.Ticks;

            if (File.Exists(backupFile) && force)
            {
                try
                {
                    File.Delete(backupFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    MessageBox.Show(CsvHelper.trans("backup_creation_failed"));
                }
            }

            if (!File.Exists(backupFile)) {
                File.Copy(mainFile, backupFile);
            }
        }

        // UPDATES START
        private void setNewsLabel(string counter)
        {
            if (counter != "0")
                Application.Current.Dispatcher.Invoke(() => newsLink.Text = counter + " missed update" + (counter != "1" ? "s" : ""));
            else
                Application.Current.Dispatcher.Invoke(() => newsLink.Text = "Updates and announces");
        }
        private void resetMissedUpdates(object sender, RoutedEventArgs e)
        {
            JSONHelper.importerSettings["last_read"] = DateTime.Now.ToUniversalTime().ToString();
            setNewsLabel("0");
            JSONHelper.saveSettings();
        }
        public async Task CheckUpdateAsync()
        {
            string pubVer = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            {
                var client = new HttpClient();
                if (!JSONHelper.importerSettings.TryGetValue("last_read", out string date))
                    date = "2000-01-01";
                string data = await client.GetStringAsync(updatedirectory + "?last_read=" + date);

                // GET NES COUNT
                Regex regexNews = new Regex(@"news=(\d+)");
                Match matchNews = regexNews.Match(data);
                if (matchNews.Groups.Count >= 2)
                {
                    setNewsLabel(matchNews.Groups[1].ToString());
                }

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
                            if (ver <= Version.Parse(pubVer))
                            {
                                break;
                            }
                            else
                            {
                                Console.WriteLine("online" + ver + " curr" + pubVer);

                                if (updateVersion == "")
                                {
                                    updateVersion = ver.ToString();
                                    updateURL = updatedirectory + url;
                                }
                                
                                if (url.Contains("_full"))
                                {
                                    updateVersion = ver.ToString();
                                    updateURL = updatedirectory + url;
                                    break;
                                }
                                    
                            }
                        }
                    }
                }

                Button btn = null;
                Button btn2 = null;
                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = addTextBlock("", HorizontalAlignment.Center, VerticalAlignment.Top, Colors.DarkGreen, new Thickness(0));

                if (updateVersion != "")
                {
                    btn = new Button();
                    btn2 = new Button();
                    btn = SetButtonAtts(btn);
                    btn.Content = CsvHelper.trans("update_auto");
                    btn.Click += UpdateAutomaticallyClick;

                    btn2 = SetButtonAtts(btn2);
                    btn2.Content = CsvHelper.trans("update_manually");
                    btn2.Click += UpdateManuallyClick;

                    myBlock.Text = string.Format(CsvHelper.trans("update_available"), updateVersion);
                    myBlock.Foreground = new SolidColorBrush(Colors.DarkRed);

                    tabAbout.Foreground = new SolidColorBrush(Colors.DarkRed);
                }
                else
                {
                    myBlock.Text = string.Format(CsvHelper.trans("update_current_version"), pubVer);
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
                Dispatcher.Invoke(() => fsTabControl.IsEnabled = false);

                WebClient _webClient = new WebClient();
                _webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(OnDownloadCompleted);

                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = addTextBlock(string.Format(CsvHelper.trans("update_applying_version"), updateVersion), HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black, new Thickness(0));
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
            if ((e == null || !e.Cancelled && e.Error == null) && File.Exists(TEMP_FILE) && new FileInfo(TEMP_FILE).Length > 10)
            {
                // CHECK EXE BACKUP
                if (File.Exists(EXE_PATH + ".BAK"))
                {
                    try {
                        File.Delete(EXE_PATH + ".BAK");
                    } catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        MessageBox.Show(CsvHelper.trans("update_cant_delete_backup"));
                    }
                }

                if (!File.Exists(EXE_PATH + ".BAK"))
                {
                    File.Move(EXE_PATH, EXE_PATH + ".BAK");

                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    while (true) { if (sw.ElapsedMilliseconds > 1000 || !File.Exists(EXE_PATH)) break; }

                    if (File.Exists(EXE_PATH))
                        MessageBox.Show(CsvHelper.trans("update_cant_delete_exe"));
                    
                    Extract extract = new Extract();
                    extract.Run(TEMP_FILE, EXE_PATH, AppDomain.CurrentDomain.BaseDirectory + "\\");
                }
            }

            Dispatcher.Invoke(() => fsTabControl.IsEnabled = true);
        }

        public void SetUpdateReady()
        {
            if (File.Exists(EXE_PATH))
            {
                Process.Start(EXE_PATH);
                Environment.Exit(0);

                StackPanel myPanel = new StackPanel();
                TextBlock myBlock = addTextBlock(CsvHelper.trans("update_failed"), HorizontalAlignment.Center, VerticalAlignment.Top, Colors.Black, new Thickness(0));
                myPanel.Children.Add(myBlock);
                AboutContent.Children.Add(myPanel);
            }
        }
        // UPDATES END
        public class HKLMRegistryHelper
        {
            public static RegistryKey GetRegistryKey(string keyPath, RegistryView view)
            {
                RegistryKey localMachineRegistry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);

                return string.IsNullOrEmpty(keyPath) ? localMachineRegistry : localMachineRegistry.OpenSubKey(keyPath);
            }

            public static string GetRegistryValue(string keyPath, string keyName, RegistryView view = RegistryView.Registry64)
            {
                try
                {
                    RegistryKey registry = GetRegistryKey(keyPath, view);
                    if (registry != null)
                    {
                        object defaultPathObj = registry.GetValue(keyName);
                        if (defaultPathObj != null)
                        {
                            string defaultPath = defaultPathObj.ToString();
                            if (!String.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
                                return defaultPath;
                        }
                    }
                }
                catch {
                    Console.WriteLine("failed to get registry value");
                }

                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        private Separator sectiondivider()
        {
            Separator sectn = new Separator();
            sectn.Margin = new Thickness(0, 0, 0, 10);

            return sectn;
        }

        private List<string> getCheckedOptions(StackPanel panel, string additionalCondition = "", bool tagValue = false)
        {
            List<string> values = new List<string>();

            foreach (var pnl in panel.Children)
            {
                if (pnl.GetType() != typeof(StackPanel))
                    continue;

                if (((StackPanel)pnl).Children.Count > 0 && ((StackPanel)pnl).Children[0].GetType() == typeof(CheckBox))
                {
                    CheckBox a = (CheckBox)((StackPanel)pnl).Children[0];
                    string content = tagValue ? a.Tag.ToString() : a.Content.ToString();
                    if (((CheckBox)((StackPanel)pnl).Children[0]).IsChecked == true && content != CsvHelper.trans("toggle_all") &&
                        ( additionalCondition == "" || content.Contains(additionalCondition) ) )
                    {
                        values.Add(content);
                    }
                }
            }

            return values;
        }
    }

}
