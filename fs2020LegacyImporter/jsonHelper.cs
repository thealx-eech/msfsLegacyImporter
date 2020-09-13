using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace msfsLegacyImporter
{
    class jsonHelper
    {
        public void createManifest(MainWindow parent, string SourceFolder, string TargetFolder, string[] data)
        {
            if (!Directory.Exists(TargetFolder)) { Directory.CreateDirectory(TargetFolder); }
            if (!Directory.Exists(TargetFolder + "SimObjects")) { Directory.CreateDirectory(TargetFolder + "SimObjects"); }
            if (!Directory.Exists(TargetFolder + "SimObjects\\AIRPLANES\\")) { Directory.CreateDirectory(TargetFolder + "SimObjects\\AIRPLANES\\"); }

            // COPY FSX FILES TO MSFS
            string sourceChild = new DirectoryInfo(SourceFolder).Name;
            Console.WriteLine(SourceFolder  + " - " + TargetFolder + "SimObjects\\AIRPLANES\\" + sourceChild + "\\");
            CloneDirectory(SourceFolder, TargetFolder + "SimObjects\\AIRPLANES\\" + sourceChild + "\\");

            Manifest manifest = new Manifest(new string[] { }, data[1], data[2], data[3], data[4], data[5], data[6], new ReleaseNotes(new Neutral("", "")));

            string json = JsonConvert.SerializeObject(manifest, Newtonsoft.Json.Formatting.Indented);

            try { File.WriteAllText(TargetFolder + "manifest.json", json); }
            catch (Exception)
            {
                MessageBox.Show("Can't write into file " + TargetFolder + "\\manifest.json");
                return;
            }

            // GENERATE LAYOUT FILE
            scanTargetFolder(TargetFolder);

            // SET CURRENT AIRCRAFT
            parent.setAircraftDirectory(TargetFolder.Remove(TargetFolder.Length - 1));
        }

        public void scanTargetFolder(string TargetFolder)
        {
            if (TargetFolder != "")
            {
                Content[] array = new Content[10000];
                int i = 0;

                // ADD MANIFEST AT THE TOP

                string[] parentDirectory = new string[] { TargetFolder };
                string[] subDirectories = Directory.GetDirectories(TargetFolder, "*", SearchOption.AllDirectories).Where(x => !x.Contains("\\.")).ToArray();
                subDirectories = parentDirectory.Concat(subDirectories).ToArray();

                foreach (var subdir in subDirectories)
                {
                    string folderName = subdir.Split('\\').Last().ToLower().Trim();
                    if (folderName.Length > 0 && folderName[0] != '.')
                    {
                        var txtFiles = Directory.EnumerateFiles(subdir, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (string currentFile in txtFiles)
                        {
                            if (Path.GetFileName(currentFile)[0] != '.' && Path.GetFileName(currentFile).ToLower() != "layout.json")
                            {
                                FileInfo info = new System.IO.FileInfo(currentFile);
                                array[i] = new Content(currentFile.Replace(TargetFolder, "").Replace("\\", "/").Trim('/'), info.Length, info.LastWriteTimeUtc.ToFileTimeUtc());

                                i++;
                            }
                        }
                    }
                }

                // CLEAR UNUSED ARRAY ITEMS
                Content[] truncArray = new Content[i];
                Array.Copy(array, truncArray, truncArray.Length);

                // ENCODE AND SAVE JSON
                ParentContent obj = new ParentContent(truncArray);
                string json = JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);

                try { File.WriteAllText(TargetFolder + "\\layout.json", json); }
                catch (Exception)
                {
                    MessageBox.Show("Can't write into file " + TargetFolder + "\\layout.json");
                    return;
                }
            }
        }

        private static void CloneDirectory(string root, string dest)
        {
            foreach (var directory in Directory.GetDirectories(root))
            {
                string dirName = Path.GetFileName(directory);
                if (!Directory.Exists(Path.Combine(dest, dirName)))
                {
                    Directory.CreateDirectory(Path.Combine(dest, dirName));
                }
                CloneDirectory(directory, Path.Combine(dest, dirName));
            }

            foreach (var file in Directory.GetFiles(root))
            {
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
            }
        }
    }

    public class Manifest
    {
        public string[] dependencies { get; set; }
        public string content_type { get; set; }
        public string title { get; set; }
        public string manufacturer { get; set; }
        public string creator { get; set; }
        public string package_version { get; set; }
        public string minimum_game_version { get; set; }
        public ReleaseNotes release_notes { get; set; }
        public Manifest(string[] Dependencies, string Content_type, string Title, string Manufacturer, string Creator,
            string Package_version, string Minimum_game_version, ReleaseNotes Release_notes)
        {
            dependencies = Dependencies;
            content_type = Content_type;
            title = Title;
            manufacturer = Manufacturer;
            creator = Creator;
            package_version = Package_version;
            minimum_game_version = Minimum_game_version;
            release_notes = Release_notes;
        }
        // Other properties, methods, events...
    }

    public class ReleaseNotes
    {
        public Neutral neutral { get; set; }
        public ReleaseNotes(Neutral Neutral)
        {
            neutral = Neutral;
        }
    }

    public class Neutral
    {
        public string LastUpdate { get; set; }
        public string OlderHistory { get; set; }
        public Neutral(string lastUpdate, string olderHistory )
        {
            LastUpdate = lastUpdate;
            OlderHistory = olderHistory;
        }
    }

    public class ParentContent
    {
        public Content[] content { get; set; }
        public ParentContent(Content[] Content)
        {
            content = Content;
        }
    }

    public class Content
    {
        public string path { get; set; }
        public long size { get; set; }
        public long date { get; set; }
        public Content(string Path, long Size, long Date)
        {
            path = Path;
            size = Size;
            date = Date;
        }
    }

}

