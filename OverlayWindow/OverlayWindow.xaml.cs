using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TwitchPlaysBot.OverlayWindow;

namespace TwitchPlaysBot.OverlayWindow
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        private readonly OverlayWindowViewModel overlayWindowViewModel;

        public OverlayWindow(OverlayWindowViewModel overlayWindowViewModel)
        {
            this.overlayWindowViewModel = overlayWindowViewModel ?? throw new ArgumentNullException(nameof(overlayWindowViewModel));
            this.DataContext = this.overlayWindowViewModel;

            InitializeComponent();
        }

        public void OnActionLogUpdated(object sender, TextChangedEventArgs e)
        {
            // remove overflowing lines
            while (ActionLog.LineCount > 15)
            {
                ActionLog.Text = ActionLog.Text.Remove(0, ActionLog.GetLineLength(0));
            }

            // scroll down to the end
            ActionLog.SelectionStart = ActionLog.Text.Length;
            ActionLog.ScrollToEnd();
        }
    }
}
