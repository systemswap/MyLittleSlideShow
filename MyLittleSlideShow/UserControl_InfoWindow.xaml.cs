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
    /// Interaktionslogik für UserControl_Changelog.xaml
    /// </summary>
    public partial class UserControl_InfoWindow : UserControl
    {
        public UserControl_InfoWindow()
        {
            InitializeComponent();
        }

     
        private void CloseInfoWindow_Click(object sender, RoutedEventArgs e)
        {
            UserControl_Changelog1.Visibility = System.Windows.Visibility.Hidden;
        }
    }
}
