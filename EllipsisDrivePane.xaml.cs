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
using Ellipsis.Api;
using Ellipsis.Drive;

namespace ArcGISProAddIn
{
    /// <summary>
    /// Interaction logic for EllipsisDrivePaneView.xaml
    /// </summary>
    public partial class EllipsisDrivePaneView : UserControl
    {
        public EllipsisDrivePaneView()
        {
            InitializeComponent();

            connect = new Connect();
            drive = new DriveView(DriveTree, connect, searchBox, null, browserButton, null);
            NameBox.TabIndex = 0;
            PassBox.TabIndex = 1;
            LogButton.TabIndex = 2;
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            bool login = false;
            if (connect != null && connect.GetStatus() == false)
                login = connect.LoginRequest();
            else if (connect != null && connect.GetStatus() == true)
                login = connect.LogoutRequest();
            else if (connect == null || (!connect.GetStatus() && !connect.LoginRequest()))
            {
                NameBox.Text = "";
                PassBox.Password = "";
                connect.SetUsername("");
                connect.SetPassword("");
            }

            if (login == false)
            {
                DriveTree.Visibility = Visibility.Collapsed;
                NameBox.Visibility = Visibility.Visible;
                PassBox.Visibility = Visibility.Visible;
                NameLabel.Visibility = Visibility.Visible;
                PassLabel.Visibility = Visibility.Visible;
                browserButton.Visibility = Visibility.Collapsed;
                searchBox.Visibility = Visibility.Collapsed;
                stopSearch.Visibility = Visibility.Collapsed;
                LogButton.Content = "Login";
                UpdateLayout();
            }

            if (login == true)
            {
                NameBox.Text = "";
                PassBox.Password = "";
                NameBox.Visibility = Visibility.Collapsed;
                PassBox.Visibility = Visibility.Collapsed;
                NameLabel.Visibility = Visibility.Collapsed;
                PassLabel.Visibility = Visibility.Collapsed;
                LogButton.Content = "Logout";
                DriveTree.Visibility = Visibility.Visible;
                browserButton.Visibility = Visibility.Visible;
                stopSearch.Visibility = Visibility.Visible;
                browserButton.IsEnabled = false;
                searchBox.Visibility = Visibility.Visible;
                UpdateLayout();

                if (drive != null)
                {
                    //If the drive view was already loaded before, it's nice to
                    //reset the nodes for the user when it's shown again.
                    drive.resetNodes();
                    return;
                }

                //TODO add textbox for searching, refreshbutton and 'open in browser'-button.
                drive = new DriveView(DriveTree, connect, null, null, null, null);
                drive.resetNodes();
                //Quick example of how to listen for events.
                drive.onLayerClick += (block, layer) => { };
                drive.onTimestampClick += (block, timestamp, visualization, protocol) => { };
            }
        }

        private void nameChanged(object sender, TextChangedEventArgs e)
        {
            if (connect != null)
                connect.SetUsername(NameBox.Text);
        }

        private void PassChanged(object sender, RoutedEventArgs e)
        {
            if (connect != null)
                connect.SetPassword(PassBox.Password);
        }

        private Connect connect;
        private DriveView drive;

        private void stopSearch_Click(object sender, RoutedEventArgs e)
        {
            if (searchBox.Text != "Search...")
                searchBox.Text = "";
        }
    }
}
