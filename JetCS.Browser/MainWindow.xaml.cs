using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JetCS.Browser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void cmbServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //cmbServer.Items.Add(cmbServer.SelectedItem as string);
        }

        private void butConnect_Click(object sender, RoutedEventArgs e)
        {

        }

        private void cmbServer_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter) 
                
                {
                    cmbServer.Items.Add(new ListBoxItem() { Content = cmbServer.Text });
                    Console.WriteLine(cmbServer.Items.Count);
                }
               

            }
            catch (Exception ex)
            {
                
            }
        }

        private void txtPort_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void txtPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}