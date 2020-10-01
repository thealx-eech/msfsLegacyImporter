using System.Windows.Forms;

namespace msfsLegacyImporter
{
    class fileDialogHelper
    {
        public string getFolderPath(string defaultPath)
        {
            FolderBrowserDialog diag = new FolderBrowserDialog();
            diag.Description = "Select folder";
            diag.SelectedPath = defaultPath;

            if (System.Windows.Forms.DialogResult.OK == diag.ShowDialog())
                return diag.SelectedPath;
            else
                return "";
        }
    }
}
