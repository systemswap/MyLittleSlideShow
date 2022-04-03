using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MyLittleSlideShow
{
    /// <summary>
    /// Interaktionslogik für MyMessageBox.xaml
    /// </summary>
    public partial class MyMessageBox : Window
    {
        #region DLLImport
        //ausblenden aus der Taskleiste
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        #endregion

        MessageBoxResult _MyMessageBoxResult = MessageBoxResult.No;
       

        public MyMessageBox(string Title, string Message)
        {
            InitializeComponent();
            this.Title = Title;
            Message_Label.Text = Message;

        }

        public static MessageBoxResult ShowDialog(string Title, string Message, System.Windows.Window owner)
        {
            MyMessageBox ShowMyMessageBox = new MyMessageBox(Title, Message);
            ShowMyMessageBox.Owner = owner;
            ShowMyMessageBox.ShowDialog();
            return ShowMyMessageBox._MyMessageBoxResult;
        }

        private void No_Button_Click(object sender, RoutedEventArgs e)
        {
            _MyMessageBoxResult = MessageBoxResult.No;
            this.Close();
        }

        public MessageBoxResult MyMessageBoxResult
        {
            get { return _MyMessageBoxResult; }
        }

        private void Yes_Button_Click(object sender, RoutedEventArgs e)
        {
            _MyMessageBoxResult = MessageBoxResult.Yes;
            this.Close();
        }

        private void MyMessageBox1_Loaded(object sender, RoutedEventArgs e)
        {
            ////Entfernt Minimize und Maximize sowie das Icon
            //var hwnd = new WindowInteropHelper(this).Handle;
            //SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            this.ShowInTaskbar = false;
        }
    }
}
