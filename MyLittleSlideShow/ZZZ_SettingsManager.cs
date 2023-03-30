using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MyLittleSlideShow
{
    public class ZZZ_SettingsManager
    {

        ZZZ.StartUpManager sum = new ZZZ.StartUpManager();

        bool Autostart = false;
        string LastPathOfApplication = string.Empty;

        string FolerPath = string.Empty;
        bool ScanFolderRekursive = false;
        int ImageChangeIntervallSeconds = 0;
        string LastLoadedFile = string.Empty;

        double PositionTop = 0;
        double PositionLeft = 0;

        int SharpenValue = 0;
        bool SharpImages = true;

        bool WithAmbiFrame = true;
        int OpacityOfAmbiFrame = 90;

        bool ActivateSlideShow = true;
        bool RandomImages = false;

        bool SetEveryImageAsWallpaper = false;
        ZZZ.ChangeWindowsWallpaper.WallpaperStyle WallpaperStyle;

        int MainWindowHeight = 0;
        int MainWindowWidth = 0;
       

        public ZZZ_SettingsManager()
        {
            LoadAllSettings();
            Autostart = sum.IsOnStartup();
        }

      



        #region Properties
        public bool _Autostart
        {
            get { return Autostart; }
            set { Autostart = value; }
        }

        public string _LastPathOfApplication
        {
            get { return LastPathOfApplication; }
            set { LastPathOfApplication = value; }
        }

        public string _FolderPath
        {
            get { return FolerPath; }
            set { FolerPath = value; }
        }

        public bool _ScanFolderRekursive
        {
            get { return ScanFolderRekursive; }
            set { ScanFolderRekursive = value; }
        }


        public int _ImageChangeIntervallSeconds
        {
            get { return ImageChangeIntervallSeconds; }
            set { ImageChangeIntervallSeconds = value; }
        }


        public string _LastLoadedFile
        {
            get { return LastLoadedFile; }
            set { LastLoadedFile = value; }
        }


        public double _PositionTop
        {
            get { return PositionTop; }
            set { PositionTop = value; }
        }


        public double _PositionLeft
        {
            get { return PositionLeft; }
            set { PositionLeft = value; }
        }

        public int _SharpenValue
        {
            get { return SharpenValue; }
            set { SharpenValue = value; }
        }

        public bool _SharpImages
        {
            get { return SharpImages; }
            set { SharpImages = value; }
        }

        public bool _WithAmbiFrame
        {
            get { return WithAmbiFrame; }
            set { WithAmbiFrame = value; }
        }

        public int _OpacityOfAmbiFrame
        {
            get { return OpacityOfAmbiFrame; }
            set { OpacityOfAmbiFrame = value; }
        }

        public bool _ActivateSlideShow
        {
            get { return ActivateSlideShow; }
            set { ActivateSlideShow = value; }
        }

        public bool _RandomImages
        {
            get { return RandomImages; }
            set { RandomImages = value; }
        }

        public bool _SetEveryImageAsWallpaper
        {
            get { return SetEveryImageAsWallpaper; }
            set { SetEveryImageAsWallpaper = value; }
        }

        public ZZZ.ChangeWindowsWallpaper.WallpaperStyle _WallpaperStyle
        {
            get { return WallpaperStyle; }
            set { WallpaperStyle = value; }
        }

        public int _MainWindowHeight
        {
            get { return MainWindowHeight; }
            set { MainWindowHeight = value; }
        }

        public int _MainWindowWidth
        {
            get { return MainWindowWidth; }
            set { MainWindowWidth = value; }
        }
        #endregion




        public void LoadAllSettings()
        {
            Autostart = Properties.Settings.Default.Autostart;
            LastPathOfApplication = Properties.Settings.Default.LastPathOfApplication;

            FolerPath = Properties.Settings.Default.FolderPath;
            ScanFolderRekursive = Properties.Settings.Default.ScanFolderRekursiv;
            ImageChangeIntervallSeconds = Properties.Settings.Default.ImageChangeIntervall_Seconds;
            LastLoadedFile = Properties.Settings.Default.LastLoadedFile;

            PositionTop = Properties.Settings.Default.PositionTop;
            PositionLeft = Properties.Settings.Default.PositionLeft;

            SharpenValue = Properties.Settings.Default.SharpenValue;
            SharpImages = Properties.Settings.Default.SharpImages;


            WithAmbiFrame = Properties.Settings.Default.WithAmbiFrame;
            OpacityOfAmbiFrame = Properties.Settings.Default.OpacityOfAmbiFrame;

            ActivateSlideShow = Properties.Settings.Default.ActivateSlideShow;
            RandomImages = Properties.Settings.Default.RandomImages;

            SetEveryImageAsWallpaper = Properties.Settings.Default.SetEveryImageAsWallpaper;
            WallpaperStyle = (ZZZ.ChangeWindowsWallpaper.WallpaperStyle)Properties.Settings.Default.WallpaperStyle;

            MainWindowHeight = Properties.Settings.Default.MainWindowHeight;
            MainWindowWidth = Properties.Settings.Default.MainWindowWidth;
        }
           


        public void SaveAllSettings()
        {
            Properties.Settings.Default.Autostart = Autostart;
            Properties.Settings.Default.LastPathOfApplication = LastPathOfApplication;

            Properties.Settings.Default.FolderPath = FolerPath;
            Properties.Settings.Default.ScanFolderRekursiv = ScanFolderRekursive;
            Properties.Settings.Default.ImageChangeIntervall_Seconds = ImageChangeIntervallSeconds;
            Properties.Settings.Default.LastLoadedFile = LastLoadedFile;

            Properties.Settings.Default.PositionTop = PositionTop;
            Properties.Settings.Default.PositionLeft = PositionLeft;

            Properties.Settings.Default.SharpenValue = SharpenValue;
            Properties.Settings.Default.SharpImages = SharpImages;

            Properties.Settings.Default.WithAmbiFrame = WithAmbiFrame;
            Properties.Settings.Default.OpacityOfAmbiFrame = OpacityOfAmbiFrame;

            Properties.Settings.Default.ActivateSlideShow = ActivateSlideShow;
            Properties.Settings.Default.RandomImages = RandomImages;

            Properties.Settings.Default.SetEveryImageAsWallpaper = SetEveryImageAsWallpaper;
            Properties.Settings.Default.WallpaperStyle = WallpaperStyle;

            Properties.Settings.Default.MainWindowHeight = MainWindowHeight;
            Properties.Settings.Default.MainWindowWidth = MainWindowWidth;

            Properties.Settings.Default.Save();
        }
    }
}
