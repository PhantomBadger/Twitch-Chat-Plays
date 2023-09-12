using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace TwitchPlaysBot.OverlayWindow
{
    public class OverlayWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ActionLog 
        {
            get
            {
                return actionLog;
            }
            set
            {
                if (actionLog != value)
                {
                    actionLog = value;
                    RaisePropertyChanged(nameof(ActionLog));
                }
            }
        }
        private string actionLog;

        public string GameTitle
        {
            get { return this.gameTitle; }
            set
            {
                string formattedValue = $"Twitch Plays \n{value}";
                if (gameTitle != formattedValue)
                {
                    gameTitle = formattedValue;
                    RaisePropertyChanged(nameof(GameTitle));
                }
            }
        }
        private string gameTitle;

        public VirtualKeyPresser VirtualKeyPresser { get; private set; }

        private readonly Dispatcher uiDispatcher;

        public OverlayWindowViewModel(Dispatcher uiDispatcher, VirtualKeyPresser virtualKeyPresser)
        {
            this.uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
            this.VirtualKeyPresser = virtualKeyPresser ?? throw new ArgumentNullException(nameof(virtualKeyPresser));

            GameTitle = virtualKeyPresser.ProcessName;
            ActionLog = "";

            virtualKeyPresser.OnKeyPressed += PrintToActionLog;
        }

        public void Dispose()
        {
            VirtualKeyPresser.OnKeyPressed -= PrintToActionLog;
        }

        public void PrintToActionLog(string username, string message)
        {
            uiDispatcher.BeginInvoke(new Action(() =>
            {
                // add a new line
                if (ActionLog.Length > 0)
                {
                    ActionLog += System.Environment.NewLine;
                }

                string line = $"{username}: {message}";

                // append to the textbox
                ActionLog += line;

                // write to debug console also
                System.Diagnostics.Debug.WriteLine(line);
            }));
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
