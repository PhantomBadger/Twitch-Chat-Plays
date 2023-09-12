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
using TwitchPlaysBot.OverlayWindow;

namespace TwitchPlaysBot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private IRC irc;
        private VirtualKeyPresser virtualKeyPresser;
        private OverlayWindow.OverlayWindow overlay;
        private Joypad currentJoypad;

        private bool processSelected;
        private bool twitchConnected;
        private bool chatPlaying;

        private const int ConsoleMaxLines = 50;

        public event PropertyChangedEventHandler PropertyChanged;

        public BindingList<KeyValuePair<int, string>> AvailableProcesses 
        { 
            get
            {
                return availableProcesses;
            }
            set
            {
                if (availableProcesses != value)
                {
                    availableProcesses = value;
                    RaisePropertyChanged(nameof(AvailableProcesses));
                }
            }
        }
        private BindingList<KeyValuePair<int, string>> availableProcesses;

        public MainWindow()
        {
            this.DataContext = this;
            InitializeComponent();
            PopulateProcessList();
            LoadSettings();
            InitIRC();

            processSelected = false;
            twitchConnected = false;
            chatPlaying = false;
            UpdateStartPlayingEnabledState();
            UpdateCreateOverlayEnabledState();
        }

        private void LoadSettings()
        {
            Username.Text = Properties.Settings.Default.Username;
            OAuthToken.Password = Properties.Settings.Default.PasswordPlain;
            Channel.Text = Properties.Settings.Default.Channel;

            InitJoypads(Properties.Settings.Default.LastBindingFile);
        }

        private void InitJoypads(string lastBindingFile)
        {
            if (string.IsNullOrWhiteSpace(lastBindingFile))
            {
                currentJoypad = Joypad.Default;
            }
            else
            {
                currentJoypad = ControlBindingEditorViewModel.ParseFromFile(lastBindingFile) ?? Joypad.Default;
                lblCurrentBinding.Content = currentJoypad.Name;
            }
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
            var overlayWindowViewModel = new OverlayWindowViewModel(Application.Current.Dispatcher, virtualKeyPresser);

            overlay = new OverlayWindow.OverlayWindow(overlayWindowViewModel);
            virtualKeyPresser.OnWatchedProcessClosed += CloseOverlay;

            overlay.Closing += delegate
            {
                overlay = null;
                virtualKeyPresser.OnWatchedProcessClosed -= CloseOverlay;
                virtualKeyPresser.Dispose();
            };
        }

        private void CloseOverlay()
        {
            overlay?.Close();
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
            processSelected = false;

            UpdateStartPlayingEnabledState();
            UpdateCreateOverlayEnabledState();
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

                if (virtualKeyPresser != null)
                {
                    virtualKeyPresser.Dispose();
                    virtualKeyPresser = null;

                    VirtualKeyPresserStatusUpdate();
                }
            }
            else
            {
                // clear the console
                ConsoleOutput.Text = "";

                // save IRC configuration
                Properties.Settings.Default.Username = Username.Text;
                Properties.Settings.Default.Password = OAuthToken.SecurePassword;
                Properties.Settings.Default.PasswordPlain = OAuthToken.Password;
                Properties.Settings.Default.Channel = (Channel.Text.StartsWith("#") ? Channel.Text : "#" + Channel.Text);
                Properties.Settings.Default.Save();

                // connect to the IRC server asynchronously
                Task.Factory.StartNew(() => { irc.Connect(); });
            }
        }
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (virtualKeyPresser != null)
            {
                virtualKeyPresser.Dispose();
                virtualKeyPresser = null;

                VirtualKeyPresserStatusUpdate();
            }
            else
            {
                if (ProcessList.SelectedValue != null)
                {
                    virtualKeyPresser = new VirtualKeyPresser(currentJoypad, irc, (int)ProcessList.SelectedValue);

                    VirtualKeyPresserStatusUpdate();
                }
            }
        }

        private void btnCreateOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (overlay != null)
            {
                CloseOverlay();
            }

            if (virtualKeyPresser == null)
            {
                return;
            }

            if (ProcessList.SelectedValue != null)
            {
                try
                {
                    InitOverlay();
                    overlay.Show();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }
            }
        }

        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            processSelected = true;

            UpdateStartPlayingEnabledState();
            UpdateCreateOverlayEnabledState();
        }

        private void VirtualKeyPresserStatusUpdate()
        {
            if (virtualKeyPresser != null)
            {
                chatPlaying = true;

                btnStart.Content = "Stop Playing";
                UpdateStartPlayingEnabledState();
                UpdateCreateOverlayEnabledState();

                lblCurrentProcessLabel.Visibility = Visibility.Visible;
                lblCurrentProcess.Visibility = Visibility.Visible;
                lblCurrentProcess.Content = virtualKeyPresser.ProcessName;
            }
            else
            {
                chatPlaying = false;

                btnStart.Content = "Start Playing";
                UpdateStartPlayingEnabledState();
                UpdateCreateOverlayEnabledState();

                lblCurrentProcessLabel.Visibility = Visibility.Collapsed;
                lblCurrentProcess.Visibility = Visibility.Collapsed;
                lblCurrentProcess.Content = "Unknown";
            }
        }

        private void UpdateStartPlayingEnabledState()
        {
            bool shouldEnable = (chatPlaying && twitchConnected) || (processSelected && twitchConnected);

            if (shouldEnable)
            {
                btnStart.IsEnabled = true;
            }
            else
            {
                btnStart.IsEnabled = false;
                lblCurrentProcessLabel.Visibility = Visibility.Collapsed;
                lblCurrentProcess.Visibility = Visibility.Collapsed;
                lblCurrentProcess.Content = "Unknown";
            }
        }

        private void UpdateCreateOverlayEnabledState()
        {
            bool shouldEnable = chatPlaying && twitchConnected;

            if (shouldEnable)
            {
                btnCreateOverlay.IsEnabled = true;
            }
            else
            {
                btnCreateOverlay.IsEnabled = false;
            }
        }

        private void ConnectionStatusUpdate(bool isConnected, bool hasJoinedChannel)
        {
            Dispatcher.BeginInvoke(new Action(() => 
            {
                twitchConnected = isConnected;

                // change the connect button text
                btnConnect.Content = isConnected ? "Disconnect" : "Connect to Twitch";

                // enable or disable the console send button
                btnConsoleSend.IsEnabled = isConnected && hasJoinedChannel;

                UpdateStartPlayingEnabledState();
                UpdateCreateOverlayEnabledState();
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

                currentJoypad = ControlBindingEditorViewModel.ParseFromControlBindings(controlBindingEditorViewModel.CurrentBindingName, controlBindings) ?? Joypad.Default;
                lblCurrentBinding.Content = currentJoypad.Name;
            }
        }

        /// <summary>
        /// Invokes the <see cref="PropertyChanged"/> event
        /// </summary>
        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
