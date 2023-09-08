using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace TwitchPlaysBot.ControlBinding
{
    /// <summary>
    /// A ViewModel represending a Control Binding
    /// </summary>
    public class ControlBindingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The message content to look for in order to call the specified key
        /// </summary>
        public string MessageContent
        {
            get
            {
                return messageContent;
            }
            set
            {
                if (messageContent != value)
                {
                    messageContent = value;
                    RaisePropertyChanged(nameof(MessageContent));
                }
            }
        }
        private string messageContent;

        /// <summary>
        /// The key to press when the message is read
        /// </summary>
        public Key TargetKey
        {
            get
            {
                return targetKey;
            }
            set
            {
                if (targetKey != value)
                {
                    targetKey = value;
                    RaisePropertyChanged(nameof(TargetKey));
                }
            }
        }
        private Key targetKey;

        /// <summary>
        /// Ctor for creating a <see cref="ControlBindingViewModel"/>
        /// </summary>
        public ControlBindingViewModel() : this(string.Empty, Key.X)
        {
        }

        /// <summary>
        /// Ctor for creating a <see cref="ControlBindingViewModel"/>
        /// </summary>
        public ControlBindingViewModel(string message, Key key)
        {
            MessageContent = message;
            TargetKey = key;
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
