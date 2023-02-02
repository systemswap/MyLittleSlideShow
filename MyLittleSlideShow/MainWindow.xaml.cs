using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Forms;

namespace MyLittleSlideShow
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (Properties.Settings.Default.UpdateRequired)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateRequired = false;
            }

            var versionAttribute = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true).FirstOrDefault() as AssemblyFileVersionAttribute;
            if (versionAttribute != null)
            {
                version = "version: " + versionAttribute.Version.ToString();
            }
        }


        #region DLLImport
        //ausblenden aus der Taskleiste
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        #endregion

        string version = string.Empty;
        ZZZ_SettingsManager zsm;
        ZZZ.ImageProcessing iP = new ZZZ.ImageProcessing();

        string[] MyFiles;
        public bool isFirstFileAtStart = true;
        string CurrentFile = string.Empty;    
    
        System.Windows.Threading.DispatcherTimer SlideShowTimer;
        int SlideShowTimerTicks = 0; 

        private System.Windows.Forms.NotifyIcon notify;

        #region NotifyIcon ContextMenuStrip Events
       
        private void Show_Click(object sender, EventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Normal;
            this.Activate();
        }

        private void Restart_Click(object sender, EventArgs e)
        {
            //System.Windows.Forms.Application.Restart();
            System.Diagnostics.Process.Start(System.Windows.Forms.Application.ExecutablePath); // to start new instance of application
            this.Close(); //to turn off current app
        }

        private void Minimize_Click(object sender, EventArgs e)
        {
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        private void Close_Click(object sender, EventArgs e)
        {
            CloseMyLittleSlideShow();
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            Main_Window.Height = Properties.Settings.Default.MainWindowHeight;
            Main_Window.Width = Properties.Settings.Default.MainWindowWidth;
            MainGrid.Height = Properties.Settings.Default.MainWindowHeight;
            MainGrid.Width = Properties.Settings.Default.MainWindowWidth;


            VersionLabel.Content = version;

            ShrinkFormToHideControls();
            zsm = new ZZZ_SettingsManager();            

            //ausblenden aus der Taskleiste
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
            this.ShowInTaskbar = false;

            //Load all Settings from PropertiesSettings  
            zsm.LoadAllSettings();

            int wholeScreenWidth = 0;
            foreach (Screen screen in Screen.AllScreens)
            {
                wholeScreenWidth += screen.Bounds.Width;
            }

            Main_Window.Top = zsm._PositionTop;
            if (wholeScreenWidth > zsm._PositionLeft)
            {
                Main_Window.Left = zsm._PositionLeft;
            }
            else
            {
                Main_Window.Left = Screen.AllScreens[0].Bounds.Width - Main_Window.Width - 20;
            }
            zsm.SaveAllSettings();
                        


            //NotifyIcon and create the ContextMenu
            this.notify = new System.Windows.Forms.NotifyIcon();
            this.notify.Text = "MyLittleSlideShow";
            this.notify.Icon = Properties.Resources.My_Icon;
            this.notify.Visible = true;

            System.Windows.Forms.ContextMenuStrip menustripe = new ContextMenuStrip();
            menustripe.Items.Add("MyLittleSlideShow" + version.Replace("version: ", "  v"), iP.LoadBitmapFromEmbeddedFolder(), null);
            menustripe.Items.Add("Restart", iP.LoadBitmapFromEmbeddedFolder("Images/refresh.png"), this.Restart_Click);
            menustripe.Items.Add("Anzeigen", iP.LoadBitmapFromEmbeddedFolder("Images/Eye.png"), this.Show_Click);            
            menustripe.Items.Add("Minimieren", iP.LoadBitmapFromEmbeddedFolder("Images/Minimize.png"), this.Minimize_Click);
            menustripe.Items.Add("Beenden", iP.LoadBitmapFromEmbeddedFolder("Images/Shutdown16.png"), this.Close_Click);
  
            this.notify.ContextMenuStrip = menustripe;

            LoadDefaultBackground();

            DeleteImageButton.Source = iP.LoadBitmapImageFromResource("/Images/Delete.png", null);
            PlayStopImage.Source = iP.LoadBitmapImageFromResource("/Images/Pause.png", null);
            Next_Button.Source = iP.LoadBitmapImageFromResource("/Images/Next.png", null);
            Prev_Button.Source = iP.LoadBitmapImageFromResource("/Images/Prev.png", null);
            CloseImageButton.Source = iP.LoadBitmapImageFromResource("/Images/Shutdown.png", null);
            SettingsImageButton.Source = iP.LoadBitmapImageFromResource("/Images/Settings512.png", null);
            RotateRightImage.Source = iP.LoadBitmapImageFromResource("/Images/Rotate.png", null);
            RotateLeftImage.Source = iP.LoadBitmapImageFromResource("/Images/Rotate.png", null);
            InfoImage.Source = iP.LoadBitmapImageFromResource("/Images/Info.png", null);

            LoadMyImage(whichImage.LastLoadedFile, zsm._ScanFolderRekursive, true);

            initializeSlideShowTimer();
            SlideShowTimer.Start();                       
        }

        public void CloseMyLittleSlideShow()
        {
            zsm._PositionLeft = Main_Window.Left;
            zsm._PositionTop = Main_Window.Top;
            zsm.SaveAllSettings();
            if (this.notify != null)
            {
                this.notify.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }          

       


        #region Functions
        int FileIndex_in_Array = 0;
        public void LoadMyImage(whichImage whichImage, bool ScanFolderRekursive, bool RescanFolders)
        {
            zsm._PositionLeft = Main_Window.Left;
            zsm._PositionTop = Main_Window.Top;
            zsm.SaveAllSettings();

            bool DirectoryExists = false;
            bool LastLoadedFileExists = false;
            bool FileExists = false;
            
           
            if (Directory.Exists(zsm._FolderPath))
            {
                DirectoryExists = true;

                var filters = new String[] { "jpg", "jpeg", "png", "gif","tif", "bmp" };

                // festlegen wann ordner neu gescannt werden soll
                if (RescanFolders)
                {
                    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                    watch.Start();
                    MyFiles = GetFilesFrom(zsm._FolderPath, filters, ScanFolderRekursive);                    
                    watch.Stop();
                    LoadTimeValue_Label.Content = Math.Round(watch.Elapsed.Duration().TotalMilliseconds, 2);
                }            
            }
            else
            {
                MyFiles = null;
            }

            //wenn keine Dateien eingelesen werden konnten Methode sofort verlassen
            if (MyFiles == null)
            {
                LoadedFilesLabel.Content = "Datei: 0 / 0";
                ToggleSlideShowTimer(TimerStartStopToggle.Stop);
                CurrentFile = string.Empty;
                LoadDefaultBackground();
                return;
            }

            //Sortiert werden kann erst hier da vorher MyFiles auch null sein kann
            Array.Sort(MyFiles);


            if (File.Exists(zsm._LastLoadedFile))
            {
                LastLoadedFileExists = true;
		 	}

            if (File.Exists(CurrentFile) && MyFiles.Count() > 0 || LastLoadedFileExists)
            {
                FileExists = true;

                if (isFirstFileAtStart && MyFiles.Count() > 0)
                {
                    FileIndex_in_Array = Array.FindIndex(MyFiles, x => x.Contains(zsm._LastLoadedFile));
                    whichImage = MainWindow.whichImage.LastLoadedFile;
                }

                if (LastLoadedFileExists && MyFiles.Contains(zsm._LastLoadedFile) && !isFirstFileAtStart)
                {
                    FileIndex_in_Array = Array.FindIndex(MyFiles, x => x.Contains(CurrentFile));
                }                

                switch (whichImage)
                {
                    case whichImage.next:
                        FileIndex_in_Array = FileIndex_in_Array + 1;
                        break;

                    case whichImage.previous:
                        FileIndex_in_Array = FileIndex_in_Array - 1;
                        break;

                    case whichImage.start:
                        FileIndex_in_Array = 0;
                        break;

                    case whichImage.LastLoadedFile:
                        break;
                }
            }
            else
            {
                //wenn ein Bild gelöscht wurde wird der selbe index noh mal verwendet
                //FileIndex_in_Array = 0;                
            }
            
            
            if (MyFiles.Count() > 0)
            {
                if (FileIndex_in_Array >= MyFiles.Count())
                {
                    FileIndex_in_Array = 0;
                }

                if (FileIndex_in_Array < 0)
                {
                    FileIndex_in_Array = MyFiles.Count() - 1;
                }
            }

            LoadedFilesLabel.Content = "Datei: " + (FileIndex_in_Array + 1) + " / " + MyFiles.Count();       

            try
            {
                if (DirectoryExists && MyFiles.Count() > 0 && File.Exists(MyFiles[FileIndex_in_Array]))
                {                   
                       FileStream fs = new FileStream(MyFiles[FileIndex_in_Array], FileMode.Open, FileAccess.Read, FileShare.Read);
                       
                        CurrentFile = MyFiles[FileIndex_in_Array];

                        zsm._LastLoadedFile = CurrentFile;
                        zsm.SaveAllSettings();


                        System.Drawing.Image image = System.Drawing.Image.FromStream(fs);

                        foreach (var prop in image.PropertyItems)
                        {
                            if ((prop.Id == 0x0112 || prop.Id == 5029 || prop.Id == 274 ))
                            {
                                var value = (int)prop.Value[0];
                                switch (value)
                                {
                                    case 1:
                                        // No rotation required.
                                        break;
                                    case 2:
                                        //image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                                        break;
                                    case 3:
                                        //image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                        break;
                                    case 4:
                                        //image.RotateFlip(RotateFlipType.Rotate180FlipX);
                                        break;
                                    case 5:
                                        //image.RotateFlip(RotateFlipType.Rotate90FlipX);
                                        break;
                                    case 6:
                                        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                        break;
                                    case 7:
                                        //image.RotateFlip(RotateFlipType.Rotate270FlipX);
                                        break;
                                    case 8:
                                        image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                        break;
                                }                               
                            }
                        }
                        
                        double cropFactor = 0;
                        double factorHeight = (image.Height / Main_Window.Height); //255
                        double factorWidth = (image.Width / Main_Window.Width); //386

                        if (factorHeight > factorWidth)
                        {
                            cropFactor = factorHeight; 
                        }
                        else
                        {
                            cropFactor = factorWidth;
                        }

                        int newWidth = Convert.ToInt32(image.Width / cropFactor);
                        int newHeight = Convert.ToInt32(image.Height / cropFactor);

                        
                        BitmapImage CurrentLoadedBitmapImage = new BitmapImage();
                        
                        image = iP.ResizeImage(image, newWidth, newHeight, ZZZ.ImageProcessing.ShrinkOrGrow.Shrink);

                        if (zsm._SharpImages)
                        {
                            image = iP.Sharpen(image, ZZZ.ImageProcessing.whichMatrix.Gaussian3x3, zsm._SharpenValue);
                        }
                        
                        CurrentLoadedBitmapImage = iP.ConvertWinFormsImageToWpfBitmapImage(image);                        
                        image.Dispose();
                        fs.Close();
                        if (fs != null)
                        {
                            fs.Dispose(); 
                        }

                        ShowImage.Source = CurrentLoadedBitmapImage;

                        if (zsm._WithAmbiFrame)
                        {
                            BackgroundImageBox.Visibility = System.Windows.Visibility.Visible;
                            BackgroundImageBox.Source = CurrentLoadedBitmapImage;
                            double dTemp = Convert.ToDouble(zsm._OpacityOfAmbiFrame);
                            BackgroundImageBox.Opacity = dTemp / 100;                       
                        }
                        else
                        {
                            double dTemp = Convert.ToDouble(zsm._OpacityOfAmbiFrame);
                            BackgroundImageBox.Opacity = dTemp / 100; 
                            BackgroundImageBox.Source = CurrentLoadedBitmapImage;
                            BackgroundImageBox.Visibility = System.Windows.Visibility.Hidden;                            
                        }
                        
                                            
                        isFirstFileAtStart = false;
                        setSlideShowTimerInterval(zsm._ImageChangeIntervallMinutes);

                        if (zsm._SetEveryImageAsWallpaper)
                        {                            
                            SetFileAsWallpaper(CurrentFile, false);                            
                        }
                    } 
                else
                {
                    //wenn eine Datei händisch aus dem ordner gelöscht wurde ohne diesen neu einzuscannen landet man hier                    
                   
                    if (string.IsNullOrWhiteSpace(CurrentFile))
                    {
                        LoadDefaultBackground(); 
                    }
                    else
                    {
                        LoadMyImage(MainWindow.whichImage.next, zsm._ScanFolderRekursive, true);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Laden der Datei aus dem Folgenden Ordner.\r\nOrdnerpfad: " + zsm._FolderPath, ex);
            }
        }
        

        private String[] GetFilesFrom(String searchFolder, String[] filters, bool isRecursive)
        {            
            if (string.IsNullOrEmpty(searchFolder))
            {
                return null;
            }
            List<String> filesFound = new List<String>();
            try
            {                
                var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                // Make a reference to a directory.
                DirectoryInfo di = new DirectoryInfo(searchFolder);

                //wenn es sich nur um ein Laufwerk handelt müßen hier noch alle ordner der ersten ebene gescannt werden
                if (di.Parent == null)
                { 
                    // Get a reference to each directory in that directory.
                    DirectoryInfo[] diArr = di.GetDirectories();

                    // Durchläuft alle ordner auf laufwerks ebene aber nicht die dateien in der RootEbene (AllDirectories muss hier gewählt sein)
                    foreach (DirectoryInfo dri in diArr)
                    {
                        if (!dri.Name.Contains("$RECYCLE.BIN") && !dri.Name.Contains("Volume Information"))
                        {
                            foreach (var filter in filters)
                            { 
                                filesFound.AddRange(Directory.GetFiles(dri.FullName, String.Format("*.{0}", filter), searchOption));
                            }
                        }                       
                    }

                    //Durchläuft alle Dateinen in der Root ebene ohne RekursiveScan
                    System.IO.DriveInfo driveInfo = new System.IO.DriveInfo(di.FullName);
                    System.IO.DirectoryInfo rootDir = driveInfo.RootDirectory;                    
                    foreach (var filter in filters)
                    {                        
                        filesFound.AddRange(rootDir.GetFiles(String.Format("*.{0}", filter)).Select(f => f.FullName).ToArray());
                    }


                    if (filesFound.Count <= 0)
                    {
                        return null;
                    }
                }
                else
                {
                    foreach (var filter in filters)
                    {
                        filesFound.AddRange(Directory.GetFiles(searchFolder, String.Format("*.{0}", filter), searchOption));                       
                    }
                }                
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Laden der Datei aus dem Folgenden Ordner.\r\nOrdnerpfad: " + zsm._FolderPath, ex);
            }            
            return filesFound.ToArray();
        }

        public void SetFileAsWallpaper(string FileName, bool askUserToChangeWallpaper)
        {
            string fileForWallpaper = string.Empty;

            //endung prüfen
            //ggf Endung umbenennen
            FileInfo oldFileInfo = new FileInfo(FileName);
            string oldFileMimeType = iP.GetMimeType(FileName).Replace("image/", ".");
            string OldFilePath = System.IO.Path.GetDirectoryName(FileName) + @"\";
            string oldFileName = System.IO.Path.GetFileNameWithoutExtension(FileName);
            string tempPath = System.IO.Path.GetTempPath() + "MyLittleSlideShow Temp Wallpaper\\";

            string newFile = string.Empty;



            switch (oldFileMimeType)
            {
                case ".jpeg":
                    oldFileMimeType = ".jpg";
                    break;
            }


            if (oldFileInfo.Extension != oldFileMimeType)
            {
                newFile = OldFilePath + oldFileName + oldFileMimeType;
                File.Move(FileName, newFile);
                LoadMyImage(whichImage.LastLoadedFile, zsm._ScanFolderRekursive, true);
            }
            else
            {
                //oldFileMimeType = oldFileInfo.Extension;
                newFile = FileName;
            }

            if (Directory.Exists(tempPath))
            {
                Array.ForEach(Directory.GetFiles(tempPath), File.Delete);
            }
            else
            {
                Directory.CreateDirectory(tempPath);
            }

            



            //Wenn datei kein JPG oder BMP ist dann Convertieren und Kopieren  
            FileInfo newFileInfo = new FileInfo(newFile);
            if (newFileInfo.Extension != ".bmp" && newFileInfo.Extension != ".jpg")
            {
                string newTempFile = tempPath + System.IO.Path.GetFileNameWithoutExtension(newFile) + ".jpg";
                iP.ConvertToJpg(newFile, newTempFile, false);

                fileForWallpaper = newTempFile;
            }
            else
            {
                fileForWallpaper = newFile;
            }



            //Als Wallpaper setzen

            ToggleSlideShowTimer(TimerStartStopToggle.Stop);

            MessageBoxResult result = MessageBoxResult.Yes;

            if (askUserToChangeWallpaper)
            {
                result = MyMessageBox.ShowDialog("Desktophintergrund ändern", "Soll dieses Bild als Desktophintergrund genutzt werden?", this);
            }          

            if (result == MessageBoxResult.Yes)
            {
                ZZZ.ChangeWindowsWallpaper cww = new ZZZ.ChangeWindowsWallpaper();

                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    //cww.SetWallpaper(CurrentFile, zsm._WallpaperStyle);
                    cww.SetWallpaper(fileForWallpaper, zsm._WallpaperStyle);
                }

            }

            ToggleSlideShowTimer(TimerStartStopToggle.Start);
        }

        public bool isReadable(string folder)
        {
            try
            {
                // Attempt to get a list of security permissions from the folder. 
                // This will raise an exception if the path is read only or do not have access to view the permissions. 
                //System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(folder, System.Security.AccessControl.AccessControlSections.Access);

                System.Security.AccessControl.DirectorySecurity ds = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(folder), System.Security.AccessControl.AccessControlSections.Access);
                return true;
            }
            catch (UnauthorizedAccessException)
            {                
                return false;                
            }          
        }

        private void LoadDefaultBackground()
        {
            BitmapImage default_Background = new BitmapImage();
            default_Background.DecodePixelHeight = (int)Main_Window.Height;

            //default_BackgroundFront.BeginInit();
            //default_BackgroundFront.UriSource = new Uri("pack://application:,,,/Images/Buch.png");
            //default_BackgroundFront.EndInit();

            Bitmap TempBitmap = iP.LoadBitmapFromEmbeddedFolder("/Images/Buch.png");
            TempBitmap = iP.Sharpen(TempBitmap, ZZZ.ImageProcessing.whichMatrix.Gaussian5x5Type1, 40);
            default_Background = iP.ConvertWinFormsImageToWpfBitmapImage(TempBitmap);
            TempBitmap.Dispose();

            ShowImage.Source = default_Background;

            if (zsm._WithAmbiFrame)
            {
                BackgroundImageBox.Visibility = System.Windows.Visibility.Visible;
                BackgroundImageBox.Source = default_Background;
                double dTemp = Convert.ToDouble(zsm._OpacityOfAmbiFrame);
                BackgroundImageBox.Opacity = dTemp /100;          
            }
            else
            {
                BackgroundImageBox.Source = default_Background;
                double dTemp = Convert.ToDouble(zsm._OpacityOfAmbiFrame);
                BackgroundImageBox.Opacity = dTemp / 100;  
                BackgroundImageBox.Visibility = System.Windows.Visibility.Hidden;               
            }
            
        }
        #endregion



        #region Enums
        public enum TimerStartStopToggle
        {
            Start,
            Stop,
            Auto
        }

        public enum whichImage
        {
            start,
            next,
            previous,
            LastLoadedFile
        }

        #endregion

        #region SlideShowTimer
        private void initializeSlideShowTimer()
        {
            SlideShowTimer = new System.Windows.Threading.DispatcherTimer();
            SlideShowTimer.Tick += new EventHandler(SlideShowTimer_Tick);
            SlideShowTimer.Interval = new TimeSpan(0, 0, 1);            
        }
        public void ToggleSlideShowTimer(TimerStartStopToggle Select)
        {
            if (SlideShowTimer == null)
            {
                initializeSlideShowTimer();
            }

            switch (Select)
            {
                case TimerStartStopToggle.Start:
                    if (!string.IsNullOrEmpty(CurrentFile))
                    {
                        PlayStopImage.Source = iP.LoadBitmapImageFromResource("/Images/pause.png", null);
                        RestTimeValue_Label.Content = TimeSpan.FromSeconds((double)SlideShowTimerTicks);
                        SlideShowTimer.IsEnabled = true;
                    }
                    break;

                case TimerStartStopToggle.Stop:
                     PlayStopImage.Source = iP.LoadBitmapImageFromResource("/Images/play.png", null);
                        RestTimeValue_Label.Content = "angehalten";
                        SlideShowTimer.IsEnabled = false;
                    break;


                case TimerStartStopToggle.Auto:                    
                        if (SlideShowTimer.IsEnabled)
                        {
                            PlayStopImage.Source = iP.LoadBitmapImageFromResource("/Images/play.png", null);
                            RestTimeValue_Label.Content = "angehalten";
                            SlideShowTimer.IsEnabled = false;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(CurrentFile))
                            {
                                PlayStopImage.Source = iP.LoadBitmapImageFromResource("/Images/pause.png", null);
                                RestTimeValue_Label.Content = TimeSpan.FromSeconds((double)SlideShowTimerTicks);
                                SlideShowTimer.IsEnabled = true;
                            }
                        }                   
                    break;            
            }
        }

        private void SlideShowTimer_Tick(object sender, EventArgs e)
        {
            if (SlideShowTimerTicks <= 0)
            {
                SlideShowTimerTicks = zsm._ImageChangeIntervallMinutes * 60;
                LoadMyImage(whichImage.next, zsm._ScanFolderRekursive, false);
            }

            SlideShowTimerTicks--;

            if (!string.IsNullOrEmpty(CurrentFile))
            {
                RestTimeValue_Label.Content = TimeSpan.FromSeconds((double)SlideShowTimerTicks);
            }
            else
            {
                RestTimeValue_Label.Content = "angehalten";
            }
        }

        public void setSlideShowTimerInterval(int Minutes)
        {
            SlideShowTimerTicks = Minutes * 60;
        }
        #endregion

        #region Events

        //MoveForm wird beim Zoomen(Resizen) der MainForm benötigt um immer auf der Lupe zu bleiben
        bool MoveForm = true;
        private void Main_Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                try
                {
                    if (MoveForm)
                    {                        
                        this.DragMove();
                        zsm._PositionTop = Main_Window.Top;
                        zsm._PositionLeft = Main_Window.Left;
                        zsm.SaveAllSettings();
                    }
                    else
                    {
                        MoveForm = true;
                    }
                    
                }
                catch (Exception) { }
        }        
        private void PlayStopImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToggleSlideShowTimer(TimerStartStopToggle.Auto);
        }

        private void Main_Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {          
            if (this.notify != null)
            {
                this.notify.Dispose();
            }
        }

        #endregion

        #region ResizeMainForm
        System.Windows.Forms.Timer ResizeFormTimer = new System.Windows.Forms.Timer();
        private void Main_Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ResizeFormTimer.Tick += ResizeOffestTimer_Tick;
            ResizeFormTimer.Interval = 1000;
            ResizeFormTimer.Start();
        }

        void ResizeOffestTimer_Tick(object sender, EventArgs e)
        {
            double dTemp = Convert.ToDouble(zsm._OpacityOfAmbiFrame);
            BackgroundImageBox.Opacity = dTemp / 100; 

            if (!zsm._WithAmbiFrame)
            {
                BackgroundImageBox.Visibility = System.Windows.Visibility.Hidden;
            }


            ShrinkFormToHideControls();
        }


        public void GrownFormToShowControls()
        {
            MainWindowRectangleGeometriy.Rect = new Rect(new System.Windows.Size(Main_Window.Width, Main_Window.Height));// 367, 315
            ControlsBackgroundGrid.Opacity = 0.6;
        }
        public void ShrinkFormToHideControls()
        {
            double width = Main_Window.Width;
            double height = Main_Window.Height -34;

            if (height < 0)
            {
                height = 0;
            }

            if (width < 0)
            {
                width = 0;
            }

            MainWindowRectangleGeometriy.Rect = new Rect(new System.Windows.Size(width, height));  //367, 281
            ControlsBackgroundGrid.Opacity = 0;
        }

        private void Main_Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            double dTemp = Convert.ToDouble(zsm._OpacityOfAmbiFrame);
            BackgroundImageBox.Opacity = dTemp / 100; 
            BackgroundImageBox.Visibility = System.Windows.Visibility.Visible;  
           
            ResizeFormTimer.Stop();

            GrownFormToShowControls();         
        }


        public void zoomMainForm(int value)
        {
            double verhältnis = 386.0 / 315.0;

            int pixel = value;
            int CursorX = 0;

            Main_Window.Height = Main_Window.Height + pixel;
            MainGrid.Height = MainGrid.Height + pixel;

            
            if (Main_Window.Height * verhältnis >= 357)
            {
                //Main_Window.Width = Main_Window.Width + pixel;
                //MainGrid.Width = MainGrid.Width + pixel;

                Main_Window.Width = Main_Window.Height * verhältnis;
                MainGrid.Width = MainGrid.Height * verhältnis; ;

                CursorX = value;
            }


            zsm._MainWindowHeight = (int)Main_Window.Height;
            zsm._MainWindowWidth = (int)Main_Window.Width;
            zsm.SaveAllSettings();

            //größe von ShowImage und BackgroundImageBox wird durch das Grid beeinflusst   

            MoveForm = false;
            MoveCursor(CursorX, value);

            LoadMyImage(whichImage.LastLoadedFile, zsm._ScanFolderRekursive, false);
        }


        public static void MoveCursor(int deltaX, int deltaY)
        {
            int x = System.Windows.Forms.Cursor.Position.X + deltaX;
            int y = System.Windows.Forms.Cursor.Position.Y + deltaY;
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
        }

        #endregion

        #region ButtonFunctions

        private void CloseImageButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseMyLittleSlideShow();
        }

        private void SettingsImageButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            //UserControl_Settings.Visibility = System.Windows.Visibility.Visible;
            zsm.LoadAllSettings();
            SettingsWindow w = new SettingsWindow(this, zsm);
            w.Owner = this;
            w.ShowDialog();
        }
        
        private void Next_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            LoadMyImage(whichImage.next, zsm._ScanFolderRekursive, false);
        }
        
        private void Prev_Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            LoadMyImage(whichImage.previous, zsm._ScanFolderRekursive, false);
        }

        private void Next_Button_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            LoadMyImage(whichImage.next, zsm._ScanFolderRekursive, false);
        }

        private void Prev_Button_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            LoadMyImage(whichImage.previous, zsm._ScanFolderRekursive, false);
        }

        private void ShowImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            //wenn der rechte maus taste gedrückt ist soll die form größe geändert werden ansonsten bildwechsel
            if (e.RightButton == MouseButtonState.Pressed || holdsStrg) //((System.Windows.Forms.Control.MouseButtons & MouseButtons.Right) != 0)
            {
                // right button is down.
                if (e.Delta < 0)
                {
                    zoomMainForm(10);
                }
                else
                {
                    zoomMainForm(-10);
                }
            }
            else
            {
                if (e.Delta < 0)
                {
                    LoadMyImage(whichImage.previous, zsm._ScanFolderRekursive, false);
                }
                else
                {
                    LoadMyImage(whichImage.next, zsm._ScanFolderRekursive, false);
                }
            }
        }

        private void RotateRightImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show("Soll das Bild, im Uhrzeigersinn gedreht werden?", "Achtung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                iP.RotateAndSaveImageFile(CurrentFile, true, ZZZ.ImageProcessing.Rotation.Right);
                LoadMyImage(whichImage.LastLoadedFile, true, false);
            }
        }
        
        private void RotateLeftImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MessageBoxResult result = System.Windows.MessageBox.Show("Soll das Bild, gegen den Uhrzeigersinn gedreht werden?", "Achtung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                iP.RotateAndSaveImageFile(CurrentFile, true, ZZZ.ImageProcessing.Rotation.Left);
                LoadMyImage(whichImage.LastLoadedFile, true, false);
            }
        }
        
        private void InfoImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentFile))
            {
                InfoWindow i = new InfoWindow(CurrentFile);
                i.ShowDialog();
            }
        }

        private void DeleteImageButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //.Net Methode von VisualBasic
            //im Verweis muss "Microsoft.VisualBasic" hinzugefügt werden
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(CurrentFile, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                LoadMyImage(whichImage.next, zsm._ScanFolderRekursive, true);
            }
            catch (Exception) { }
        }       

        private void ChangeWallpaperImageButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetFileAsWallpaper(CurrentFile, true);
        }

        private void ZoomImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            zoomMainForm(10);
        }
        
        private void ZoomImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            zoomMainForm(-10);
        }

        #endregion

        #region Opacity
        private void RotateRightImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RotateRightImage.Opacity = 1;
        }
        private void SettingsImageButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SettingsImageButton.Opacity = 0.4;
        }

        private void SettingsImageButton_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SettingsImageButton.Opacity = 1;
        }

        private void CloseImageButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CloseImageButton.Opacity = 0.4;
        }

        private void CloseImageButton_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CloseImageButton.Opacity = 1;
        }

        private void Prev_Button_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Prev_Button.Opacity = 0.4;
        }

        private void Prev_Button_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Prev_Button.Opacity = 1;
        }


        private void Next_Button_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Next_Button.Opacity = 1;
        }

        private void Next_Button_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Next_Button.Opacity = 0.4;
        }

        private void RotateRightImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RotateRightImage.Opacity = 0.4;
        }

        private void RotateLeftImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RotateLeftImage.Opacity = 1;
        }
        private void RotateLeftImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RotateLeftImage.Opacity = 0.4;
        }

        private void InfoImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            InfoImage.Opacity = 0.4;
        }

        private void InfoImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            InfoImage.Opacity = 1;
        }

        private void PlayStopImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PlayStopImage.Opacity = 0.4;
        }

        private void PlayStopImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PlayStopImage.Opacity = 1;
        }

        private void DeleteImageButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteImageButton.Opacity = 0.6;
        }
        private void DeleteImageButton_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteImageButton.Opacity = 1;
        }

        private void ChangeWallpaperImageButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ChangeWallpaperImageButton.Opacity = 0.4;
        }

        private void ChangeWallpaperImageButton_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ChangeWallpaperImageButton.Opacity = 1;
        }

        private void ZoomImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ZoomImage.Opacity = 0.4;
            MoveForm = true;
        }

        private void ZoomImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ZoomImage.Opacity = 1;
        }
        #endregion

        private void Main_Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //zsm.SaveAllSettings();
        }

        bool holdsStrg = false;
        private void Main_Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                holdsStrg = true;
            }
        }

        private void Main_Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            holdsStrg = false;
        }
    }
}
