using ExifLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MyLittleSlideShow
{
    /// <summary>
    /// Interaktionslogik für InfoWindow.xaml
    /// </summary>
    public partial class InfoWindow : Window
    {
        FileInfo fi;
        string FileName = string.Empty;
        public InfoWindow(string FilePathWithName)
        {
            InitializeComponent();
            FileName = FilePathWithName;
            fi = new FileInfo(FileName);
            getFileInfos();
        }

        private void getFileInfos()
        {
            InfoLB.Items.Add("Dateiname: " + fi.Name);           
            InfoLB.Items.Add("Erstellt am: " + fi.CreationTime);
            InfoLB.Items.Add("");
            InfoLB.Items.Add("Verzeichnis: ");
            InfoLB.Items.Add(fi.Directory);
            InfoLB.Items.Add("");
            InfoLB.Items.Add("");
            InfoLB.Items.Add("ExifData");

            FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            System.Drawing.Image image = System.Drawing.Image.FromStream(fs);
            fs.Close();
            fs.Dispose();

            string ExifData = string.Empty;

            try
            {
                using (var reader = new ExifReader(FileName))
                {
                    // Get the image thumbnail (if present)
                    var thumbnailBytes = reader.GetJpegThumbnailBytes();

                    if (thumbnailBytes == null)
                    {
                        //pictureBoxThumbnail.Image = null;
                    }                        
                    else
                    {
                        //using (var stream = new MemoryStream(thumbnailBytes))
                        //   pictureBoxThumbnail.Image = Image.FromStream(stream);
                    }

                    // To read a single field, use code like this:
                    /*
                    DateTime datePictureTaken;
                    if (reader.GetTagValue(ExifTags.DateTimeOriginal, out datePictureTaken))
                    {
                        MessageBox.Show(this, string.Format("The picture was taken on {0}", datePictureTaken), "Image information", MessageBoxButtons.OK);
                    }
                    */

                    // Parse through all available fields and generate key-value labels
                    var props = Enum.GetValues(typeof(ExifTags)).Cast<ushort>().Select(tagID =>
                    {
                        object val;
                        if (reader.GetTagValue(tagID, out val))
                        {
                            // Special case - some doubles are encoded as TIFF rationals. These
                            // items can be retrieved as 2 element arrays of {numerator, denominator}
                            if (val is double)
                            {
                                int[] rational;
                                if (reader.GetTagValue(tagID, out rational))
                                    val = string.Format("{0} ({1}/{2})", val, rational[0], rational[1]);
                            }

                            return string.Format("{0}: {1}", Enum.GetName(typeof(ExifTags), tagID), RenderTag(val));
                        }

                        return null;

                    }).Where(x => x != null).ToArray();

                   ExifData = string.Join("\r\n", props);
                }
            }
            catch (Exception)
            {
                InfoLB.Items.Add("Es konnten keine Exif Daten eingelesen werden.");  //MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            string[] stringSeparators = new string[] {"\r\n"};
            string[] array = ExifData.Split(stringSeparators, StringSplitOptions.None);
            foreach (string item in array)
            {
                if (!item.Contains("MakerNote"))
                {
                    InfoLB.Items.Add(item);
                }                
            }             
        }

        private static string RenderTag(object tagValue)
        {
            // Arrays don't render well without assistance.
            var array = tagValue as Array;
            if (array != null)
            {
                // Hex rendering for really big byte arrays (ugly otherwise)
                if (array.Length > 20 && array.GetType().GetElementType() == typeof(byte))
                    return "0x" + string.Join("", array.Cast<byte>().Select(x => x.ToString("X2")).ToArray());

                return string.Join(", ", array.Cast<object>().Select(x => x.ToString()).ToArray());
            }

            return tagValue.ToString();
        }

        private void OpenDirectory_Button_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.GetDirectoryName(FileName);
            if (Directory.Exists(path))
                Process.Start("explorer.exe", "/e,/select,\"" + FileName + "\"");                
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
