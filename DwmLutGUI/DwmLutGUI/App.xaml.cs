using System.Windows;
using System.Windows.Input;

namespace DwmLutGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public static KeyboardListener KListener = new KeyboardListener();

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            KListener.Dispose();
        }
    }
}