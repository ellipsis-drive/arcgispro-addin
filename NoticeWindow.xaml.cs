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

namespace TestAddIn
{
    /// <summary>
    /// Interaction logic for ProWindow1.xaml
    /// </summary>
    public partial class NoticeWindow : ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        public NoticeWindow()
        {
            InitializeComponent();
        }

        public void SetUrl(string url)
        {
            this.urlTextBox.Text = url;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
