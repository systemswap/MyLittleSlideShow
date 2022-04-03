using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace ZZZ
{
    public class ChangeWindowsWallpaper
    {

        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDWININICHANGE = 0x02;

        public ChangeWindowsWallpaper()
        {
            WindowsWallpaperPath = GetCurrentWallpaperPath();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        string WindowsWallpaperPath = string.Empty;


        public enum WallpaperStyle : int
        {
          Zentriert, Gestreckt, Angepasst
        }

        /// <summary>
        /// Path wird geprüft ob diese Datei auch Existiert
        /// </summary>
        /// <param name="path"></param>
        /// <param name="style"></param>
        public void SetWallpaper(string path, WallpaperStyle style)
        {
            if (System.IO.File.Exists(path))
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", true);                             

                switch (style)
                {                  
                    case WallpaperStyle.Zentriert:
                        key.SetValue(@"WallpaperStyle", "1");
                        key.SetValue(@"TileWallpaper", "0");
                        break;

                    case WallpaperStyle.Gestreckt:
                        key.SetValue(@"WallpaperStyle", "2");
                        key.SetValue(@"TileWallpaper", "0");
                        break; 
              
                    
                    case WallpaperStyle.Angepasst:
                        key.SetValue(@"WallpaperStyle", "4");
                        key.SetValue(@"TileWallpaper", "0");
                        break;
                }
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            else
            {
                throw new System.IO.FileNotFoundException();
            }
        }

        private static string GetCurrentWallpaperPath()
        {
            RegistryKey wallPaper = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", false);
            string WallpaperPath = wallPaper.GetValue("WallPaper").ToString();
            wallPaper.Close();
            return WallpaperPath;
        }
    }

}

