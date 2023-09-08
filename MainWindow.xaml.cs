using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TwitchPlaysBot.ControlBinding;

namespace TwitchPlaysBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IRC irc;
        private OverlayWindow overlay;
        private Joypad currentJoypad;
        private const int ConsoleMaxLines = 50;

        public BindingList<KeyValuePair<int, string>> AvailableProcesses { get; set; }

        public MainWindow()
        {
            this.DataContext = this;
            PopulateProcessList();
            InitJoypads();
            InitializeComponent();
            InitIRC();
            LoadSettings();
        }

        private void LoadSettings()
        {
            Username.Text = Properties.Settings.Default.Username;
            Channel.Text = Properties.Settings.Default.Channel;
        }

        private void InitJoypads()
        {
            currentJoypad = Joypad.Default;
        }

        private void InitIRC()
        {
            irc = new IRC("irc.twitch.tv", 6667);
            //irc = new IRC("199.9.252.26", 6667);
            irc.eventConnectionStatus += new ConnectionStatus(ConnectionStatusUpdate);
            irc.eventRecievingData += new DataReceived(PrintToConsole);
        }

        private void InitOverlay()
        {
            overlay = new OverlayWindow() { Joypad = currentJoypad };
            irc.eventRecievingMessage += new MessageReceived(overlay.OnMessagedRecieved);
            overlay.Closing += delegate
            {
                irc.eventRecievingMessage -= new MessageReceived(overlay.OnMessagedRecieved);
                overlay = null;
            };
        }

        private void PopulateProcessList()
        {
            if (AvailableProcesses == null)
            {
                AvailableProcesses = new BindingList<KeyValuePair<int, string>>();
            }

            if (AvailableProcesses.Count > 0)
            {
                AvailableProcesses.Clear();
            }

            Process[] processList = Process.GetProcesses();
            Array.Sort(processList, (x1, x2) => x1.ProcessName.CompareTo(x2.ProcessName));
            
            foreach (Process proc in processList)
            {
                // only add processes that have a main window handle
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    AvailableProcesses.Add(new KeyValuePair<int, string>(proc.Id, String.Format("{0} ({1}.exe)", proc.MainWindowTitle, proc.ProcessName)));
                }
            }

            System.Diagnostics.Debug.WriteLine(AvailableProcesses.Count + " processes found");
        }

        private void PrintToConsole(string data)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {

                // add a new line
                if (ConsoleOutput.Text.Length > 0)
                {
                    ConsoleOutput.Text += System.Environment.NewLine;
                }

                // append to the textbox
                ConsoleOutput.Text += data;

                // remove overflowing lines
                while (ConsoleOutput.LineCount > ConsoleMaxLines)
                {
                    ConsoleOutput.Text = ConsoleOutput.Text.Remove(0, ConsoleOutput.GetLineLength(0));
                }

                // scroll down to the end
                ConsoleOutput.SelectionStart = ConsoleOutput.Text.Length;
                ConsoleOutput.ScrollToEnd();

                // write to debug console also
                System.Diagnostics.Debug.WriteLine(data);
            }));
        }

        private void btnProcessListRefresh_Click(object sender, RoutedEventArgs e)
        {
            PopulateProcessList();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // force shutdown of the overlay if it's running
            Application.Current.Shutdown();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (irc.IsConnected)
            {
                irc.Disconnect();
            }
            else
            {
                // clear the console
                ConsoleOutput.Text = "";

                // save IRC configuration
                Properties.Settings.Default.Username = Username.Text;
                Properties.Settings.Default.Password = OAuthToken.SecurePassword;
                Properties.Settings.Default.Channel = (Channel.Text.StartsWith("#") ? Channel.Text : "#" + Channel.Text);
                Properties.Settings.Default.Save();

                // connect to the IRC server asynchronously
                Task.Factory.StartNew(() => { irc.Connect(); });
            }

        }

        private void ConnectionStatusUpdate(bool isConnected, bool hasJoinedChannel)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                // change the connect button text
                btnConnect.Content = isConnected ? "Disconnect" : "Connect to Twitch";
                // enable or disable the console send button
                btnConsoleSend.IsEnabled = isConnected && hasJoinedChannel;
            }));
        }

        private void btnConsoleSend_Click(object sender, RoutedEventArgs e)
        {
            string message = ConsoleInput.Text.Trim();

            if (irc.IsConnected && irc.hasJoinedChannel && message.Length > 0)
            {
                Task.Factory.StartNew(() => { 
                    
                    irc.SendMessage(message);
                    PrintToConsole("< " + message);
                });

                ConsoleInput.Text = "";
            }
        }

        private void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnConsoleSend.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void btnCreateOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (overlay != null)
            {
                overlay.Close();
            }

            InitOverlay();

            if (ProcessList.SelectedValue != null)
            {
                overlay.ProcessId = (int)ProcessList.SelectedValue;

                try
                {
                    overlay.Hook();
                    overlay.Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnCreateOverlay.IsEnabled = true;
        }

        private void MenuItem_ControlBinding_Click(object sender, RoutedEventArgs e)
        {
            var controlBindingEditorViewModel = new ControlBindingEditorViewModel();
            var controlBindingEditor = new ControlBindingEditor();
            controlBindingEditor.DataContext = controlBindingEditorViewModel;
            controlBindingEditorViewModel.ParseFromJoypad(currentJoypad);
            controlBindingEditorViewModel.CurrentBindingName = lblCurrentBinding.Content.ToString();

            if (controlBindingEditor.ShowDialog() == true)
            {
                ControlBindingViewModel[] controlBindings = controlBindingEditorViewModel.ControlBindings.ToArray();

                // Build a dictionary from the control bindings
                var bindingDict = new Dictionary<string, List<Key>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < controlBindings.Length; i++)
                {
                    string message = controlBindings[i].MessageContent;
                    Key key = controlBindings[i].TargetKey;

                    if (bindingDict.ContainsKey(message))
                    {
                        bindingDict[message].Add(key);
                    }
                    else
                    {
                        bindingDict[message] = new List<Key>() { key };
                    }
                }

                // Assign the joypad
                Joypad newJoypad = new Joypad(bindingDict);
                currentJoypad = newJoypad;

                lblCurrentBinding.Content = controlBindingEditorViewModel.CurrentBindingName;
            }
        }
    }
}
