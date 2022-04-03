using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Forms;
using System.Reflection;



namespace MyLittleSlideShow
{
    /// <summary>
    /// Interaktionslogik für Window1.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {

        MainWindow _mainWindow = new MainWindow();
        ZZZ_SettingsManager _zsm = new ZZZ_SettingsManager();
        ZZZ.StartUpManager sum = new ZZZ.StartUpManager();



        public SettingsWindow(MainWindow mainwindow, ZZZ_SettingsManager zsm)
        {
            InitializeComponent();

            _mainWindow = mainwindow;
            _zsm = zsm;
            readChagelog();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void SelectFolder_Button_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.RootFolder = Environment.SpecialFolder.Desktop;
            DialogResult result = fbd.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                _zsm._FolderPath = fbd.SelectedPath;
                FileFolderPath_Label.Text = _zsm._FolderPath;
                _zsm._FolderPath = _zsm._FolderPath;
            }
        }


        //alle Optionen hier beim Start Aktualisieren
        private void SettingsWindowMain_Loaded(object sender, RoutedEventArgs e)
        {
            Autostart_CheckBox.IsChecked = sum.IsOnStartup();

            BilderSchaerfenCheckbox.IsChecked = _zsm._SharpImages;
            SharpenValue_Textbox.Text = (_zsm._SharpenValue).ToString();

            ShowAmbiFrameCheckbox.IsChecked = _zsm._WithAmbiFrame;
            AmbiFrameOpacity.Text = _zsm._OpacityOfAmbiFrame.ToString();


            Interval_in_Minutes.Text = _zsm._ImageChangeIntervallMinutes.ToString();

            SetEveryImageAsWallpaper_Checkbox.IsChecked = _zsm._SetEveryImageAsWallpaper;
            WallpaperStyle_ComboBox.ItemsSource = Enum.GetValues(typeof(ZZZ.ChangeWindowsWallpaper.WallpaperStyle));
            switch (_zsm._WallpaperStyle)
            {
                case ZZZ.ChangeWindowsWallpaper.WallpaperStyle.Zentriert:
                    WallpaperStyle_ComboBox.SelectedIndex = 0;
                    break;
                case ZZZ.ChangeWindowsWallpaper.WallpaperStyle.Gestreckt:
                    WallpaperStyle_ComboBox.SelectedIndex = 1;
                    break;
                case ZZZ.ChangeWindowsWallpaper.WallpaperStyle.Angepasst:
                    WallpaperStyle_ComboBox.SelectedIndex = 2;
                    break;
            }

            FileFolderPath_Label.Text = _zsm._FolderPath;
            Rekursive.IsChecked = _zsm._ScanFolderRekursive;
        }




        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            //Autostartoptionen festlegen
            if (!sum.IsOnStartup() && (bool)Autostart_CheckBox.IsChecked)
            {
                sum.AddShortcutToAutostart();
                _zsm._LastPathOfApplication = sum.getCurrentLocationOfApplication;
                _zsm._Autostart = Convert.ToBoolean(Autostart_CheckBox.IsChecked);
            }
            else
            {
                //alte autostart Verknüpfungen Löschen
                sum.RemoveApplicationFromStartup();

                //wenn Checkbox true soll eine neue autostart verknüpfung angelegt werden
                if ((bool)Autostart_CheckBox.IsChecked)
                {
                    sum.AddShortcutToAutostart();
                    _zsm._LastPathOfApplication = sum.getCurrentLocationOfApplication;
                    _zsm._Autostart = Convert.ToBoolean(Autostart_CheckBox.IsChecked);
                }
            }

            _zsm._SharpImages = (bool)BilderSchaerfenCheckbox.IsChecked;
            if (string.IsNullOrWhiteSpace(SharpenValue_Textbox.Text))
            {
                SharpenValue_Textbox.Text = "0";
            }
            _zsm._SharpenValue = Convert.ToInt16(SharpenValue_Textbox.Text);

            _zsm._WithAmbiFrame = (bool)ShowAmbiFrameCheckbox.IsChecked;
            if (string.IsNullOrWhiteSpace(AmbiFrameOpacity.Text))
            {
                AmbiFrameOpacity.Text = "0";
            }
            _zsm._OpacityOfAmbiFrame = Convert.ToInt16(AmbiFrameOpacity.Text);

            if (string.IsNullOrWhiteSpace(Interval_in_Minutes.Text))
            {
                Interval_in_Minutes.Text = "2";
            }
            _zsm._ImageChangeIntervallMinutes = Convert.ToInt16(Interval_in_Minutes.Text);
            _zsm._SetEveryImageAsWallpaper = (bool)SetEveryImageAsWallpaper_Checkbox.IsChecked;

            _zsm._FolderPath = FileFolderPath_Label.Text;
            _zsm._ScanFolderRekursive = (bool)Rekursive.IsChecked;
            _zsm._WallpaperStyle = (ZZZ.ChangeWindowsWallpaper.WallpaperStyle)WallpaperStyle_ComboBox.SelectedValue;


            _zsm.SaveAllSettings();



            _mainWindow.setSlideShowTimerInterval(_zsm._ImageChangeIntervallMinutes);
            _mainWindow.LoadMyImage(MainWindow.whichImage.LastLoadedFile, _zsm._ScanFolderRekursive, true);
            _mainWindow.ToggleSlideShowTimer(MainWindow.TimerStartStopToggle.Start);

            this.Close();
        }

        private void resetPath_Button_Click(object sender, RoutedEventArgs e)
        {
            _zsm._LastLoadedFile = string.Empty;

            FileFolderPath_Label.Text = string.Empty; ;
            Properties.Settings.Default.FolderPath = string.Empty;
            Properties.Settings.Default.Save();
        }


        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("http://www.MTS-Solutions.de");
        }

        private void SpendenImageButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ZZZ.PayPalSpende.PayPalSpenden(ZZZ.PayPalSpende.Sprache.DE);
        }

        private void readChagelog()
        {
            UC_InfoWindow.ChangeLogText_ListBox.Items.Clear();
            UC_InfoWindow.HiddenOptions.Items.Clear();

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "MyLittleSlideShow.Infos.Changelog.txt";
            string result = string.Empty;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                while ((result = reader.ReadLine()) != null)
                {
                    UC_InfoWindow.ChangeLogText_ListBox.Items.Add(result);
                    result = string.Empty;
                }
            }
            
            resourceName = "MyLittleSlideShow.Infos.HiddenOptions.txt";
            result = string.Empty;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                while ((result = reader.ReadLine()) != null)
                {
                    UC_InfoWindow.HiddenOptions.Items.Add(result);
                    result = string.Empty;
                }
            }

        }

        private void InfoWindow_Button_Click(object sender, RoutedEventArgs e)
        {
            UC_InfoWindow.Visibility = System.Windows.Visibility.Visible;
        }
    }       
}
