using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;

using System.Windows.Forms;

namespace ZZZ
{
    public class StartUpManager
    {
        string AppPath_with_Name = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        string FullAssemblyName = Assembly.GetExecutingAssembly().FullName;

        string myDocumentsFile = string.Empty;
        string MyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        public string getCurrentLocationOfApplication
        {
            get { return MyLocation; }
        }
        
        string AutostartFile = string.Empty;
        
        RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        RegistryKey all_key = null;

        bool is_the_same_location = false;

        public StartUpManager()
        {
            myDocumentsFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\" + appName + ".txt";            
            AutostartFile = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + appName + ".url";
            try { all_key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true); }
            catch (Exception) { }
            is_the_same_location = is_same_location();
        }
        
        
        public bool IsOnStartup()
        {
            bool _isonstartup = false;
            if (key.GetValue(FullAssemblyName) != null && is_the_same_location)
            {
                _isonstartup = true;
            }

            try
            {
                if (all_key != null)
                {
                    if (all_key.GetValue(FullAssemblyName) != null && is_the_same_location)
                    {
                        _isonstartup = true;
                    }
                }
            }
            catch (Exception)
            {
            }

            if (File.Exists(AutostartFile) && is_the_same_location)
            {
                _isonstartup = true;
            }
            return _isonstartup;
        }

        private bool is_same_location()
        {
            bool _location = false;
         

            if (MyLittleSlideShow.Properties.Settings.Default.LastPathOfApplication == MyLocation)
            {
                _location = true;
            }

            return _location;
        }

       

        private void set_locationInformation()
        {
            try
            {               
                MyLittleSlideShow.Properties.Settings.Default.LastPathOfApplication = MyLocation;
                MyLittleSlideShow.Properties.Settings.Default.Save();
            }
            catch (Exception)
            {
                
                throw;
            }
        }

        public void AddApplicationToCurrentUserStartup()
        {
            key.SetValue(FullAssemblyName, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
            set_locationInformation();
        }

        public void AddApplicationToAllUserStartup()
        {
            try
            {
                all_key.SetValue(FullAssemblyName, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                set_locationInformation();
            }
            catch (Exception ex)
            {
                throw new Exception("Es kann nicht für alle Benutzer zum Windows Start hinzugefügt werden,\r\nda der angemeldete Benutzer keine Adminrechte besitzt.", ex);
            }
        }


        public void AddShortcutToAutostart()
        {           
            using (StreamWriter writer = new StreamWriter(AutostartFile))
            {
                string app = Process.GetCurrentProcess().MainModule.FileName; //System.Reflection.Assembly.GetExecutingAssembly().Location;
                writer.WriteLine("[InternetShortcut]");
                writer.WriteLine("URL=file:///" + app);
                writer.WriteLine("IconIndex=0");
                string icon = app.Replace('\\', '/');
                writer.WriteLine("IconFile=" + icon);
                writer.Flush();
                set_locationInformation();
            }
        }

        public void RemoveApplicationFromStartup()
        {
            try
            {
                key.DeleteValue(FullAssemblyName, false);               

                if (File.Exists(AutostartFile))
                {
                    File.Delete(AutostartFile);
                }

                if (File.Exists(myDocumentsFile))
                {
                    File.Delete(myDocumentsFile);
                }

                if(all_key != null) all_key.DeleteValue(FullAssemblyName, false);
            }
            catch (Exception)
            {
            }
        }

      

        public bool IsUserAdministrator()
        {
            //bool value to hold our return value
            bool isAdmin;
            try
            {
                //get the currently logged in user
                WindowsIdentity user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException)
            {
                isAdmin = false;
            }
            catch (Exception)
            {
                isAdmin = false;
            }
            return isAdmin;
        }
    }
}

