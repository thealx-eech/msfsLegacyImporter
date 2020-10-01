using ICSharpCode.SharpZipLib.Zip;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace msfsLegacyImporter
{
    class Extract
    {
        public Extract()
        {
        }

        public void Run(string TEMP_FILE, string EXE_PATH)
        {
            BackgroundWorker bgw = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            bgw.DoWork += new DoWorkEventHandler(
                delegate (object o, DoWorkEventArgs args) {
                    BackgroundWorker bw = o as BackgroundWorker;
                    FastZip fastZip = new FastZip();
                    Console.WriteLine("Unzipping");
                    fastZip.ExtractZip(TEMP_FILE, AppDomain.CurrentDomain.BaseDirectory + "\\", FastZip.Overwrite.Always, null, @"^(.*\.dll)", null, false);
                });

            bgw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate (object o, RunWorkerCompletedEventArgs args) {
                //((MainWindow)System.Windows.Application.Current.MainWindow).setUpdateReady();
                if (File.Exists(EXE_PATH))
                {
                    Process.Start(EXE_PATH);
                    Environment.Exit(0);
                } else
                {
                    MessageBox.Show("Update failed, but you can extract temp.zip manually");
                    File.Move(EXE_PATH + ".BAK", EXE_PATH);
                }
            });

            bgw.RunWorkerAsync();

        }
    }
}