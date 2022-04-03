using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyLittleSlideShow
{
    /// <summary>
    /// Interaktionslogik für UserControl_Settings.xaml
    /// </summary>
    public partial class UserControl_Settings : UserControl
    {
        public UserControl_Settings()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = System.Windows.Visibility.Hidden;
        }

        private void SelectFolder_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void resetPath_Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Autostart_CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void Autostart_CheckBox_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

       
    }
}
