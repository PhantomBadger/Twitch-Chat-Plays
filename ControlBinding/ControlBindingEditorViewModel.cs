using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Newtonsoft.Json;
using MessageBox = System.Windows.MessageBox;

namespace TwitchPlaysBot.ControlBinding
{
    /// <summary>
    /// A ViewModel for the <see cref="ControlBindingEditor"/>
    /// </summary>
    public class ControlBindingEditorViewModel : INotifyPropertyChanged
    {
        private const string HeaderTextPre = "Control Binding Editor";
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// A list of <see cref="ControlBindingViewModel"/>
        /// </summary>
        public ObservableCollection<ControlBindingViewModel> ControlBindings
        { 
            get
            {
                return controlBindings;
            }
            set
            {
                if (controlBindings != value)
                {
                    controlBindings = value;
                    RaisePropertyChanged(nameof(ControlBindings));
                }
            }
        }
        private ObservableCollection<ControlBindingViewModel> controlBindings;

        /// <summary>
        /// The text for the window header
        /// </summary>
        public string HeaderText
        {
            get
            {
                return headerText;
            }
            set
            {
                if (headerText != value)
                {
                    headerText = value;
                    RaisePropertyChanged(nameof(HeaderText));
                }
            }
        }
        private string headerText;

        /// <summary>
        /// The name of the current binding
        /// </summary>
        public string CurrentBindingName
        {
            get
            {
                return currentBindingName;
            }
            set
            {
                if (currentBindingName != value)
                {
                    currentBindingName = value;
                    HeaderText = $"{HeaderTextPre} - {currentBindingName}";
                    RaisePropertyChanged(nameof(CurrentBindingName));
                }
            }
        }
        private string currentBindingName;

        /// <summary>
        /// The currently selected Control Binding
        /// </summary>
        public ControlBindingViewModel SelectedControlBinding
        {
            get
            {
                return selectedControlBinding;
            }
            set
            {
                if (selectedControlBinding != value)
                {
                    selectedControlBinding = value;
                    RaisePropertyChanged(nameof(SelectedControlBinding));
                }
            }
        }
        private ControlBindingViewModel selectedControlBinding;

        /// <summary>
        /// Ctor for creating a <see cref="ControlBindingEditorViewModel"/>
        /// </summary>
        public ControlBindingEditorViewModel()
        {
            ControlBindings = new ObservableCollection<ControlBindingViewModel>();
            HeaderText = $"{HeaderTextPre} - New...";
            CurrentBindingName = "Default";

            AddNewControlBinding();

            SetupCommands();
        }

        #region Commands
        /// <summary>
        /// A command for adding a new Control Binding from our list
        /// </summary>
        public DelegateCommand AddNewControlBindingCommand { get; private set; }

        /// <summary>
        /// A command for removing a Control Binding from our list
        /// </summary>
        public DelegateCommand RemoveSelectedControlBindingCommand { get; private set; }

        /// <summary>
        /// A command for saving a set of bindings
        /// </summary>
        public DelegateCommand SaveBindingCommand { get; private set; }

        /// <summary>
        /// A command for loading a set of bindings
        /// </summary>
        public DelegateCommand LoadBindingCommand { get; private set; }

        /// <summary>
        /// A command for applying a set of bindings
        /// </summary>
        public DelegateCommand<Window> ApplyBindingCommand { get; private set; }

        /// <summary>
        /// A command for closing without applying a set of bindings
        /// </summary>
        public DelegateCommand<Window> CloseCommand { get; private set; }

        /// <summary>
        /// A command to reset bindings to an acceptable default
        /// </summary>
        public DelegateCommand ResetBindingCommand { get; private set; }

        /// <summary>
        /// Sets up all the delegate bindings for this ViewModel
        /// </summary>
        private void SetupCommands()
        {
            AddNewControlBindingCommand = new DelegateCommand(_ => { AddNewControlBinding(); } );
            RemoveSelectedControlBindingCommand = new DelegateCommand(_ => { RemoveControlBinding(); });
            SaveBindingCommand = new DelegateCommand(_ => { SaveBindings(); });
            LoadBindingCommand = new DelegateCommand(_ => { LoadBindings(); });
            ResetBindingCommand = new DelegateCommand(_ => { ResetBinding(); });
            ApplyBindingCommand = new DelegateCommand<Window>((Window window) => { CloseAndApplyBindings(window); });
            CloseCommand = new DelegateCommand<Window>((Window window) => { CloseWithoutApplyingBindings(window); });
        }

        #endregion

        /// <summary>
        /// Closes the provided window, setting the <see cref="Window.DialogResult"/> to false to indicate the binding should not be applied
        /// </summary>
        private void CloseWithoutApplyingBindings(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }

        /// <summary>
        /// Closes the provided window, setting the <see cref="Window.DialogResult"/> to true to indicate the binding should be applied
        /// </summary>
        private void CloseAndApplyBindings(Window window)
        {
            window.DialogResult = true;
            window.Close();
        }

        /// <summary>
        /// Resets the bindings to an acceptable default
        /// </summary>
        private void ResetBinding()
        {
            ParseFromJoypad(Joypad.Default);
            CurrentBindingName = "Default";
        }

        /// <summary>
        /// Loads bindings from disk and replaces the current set with them
        /// </summary>
        private void LoadBindings()
        {
            try
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "Control Bindings (*.cb)|*.cb|All files (*.*)|*.*";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK
                        && !string.IsNullOrWhiteSpace(openFileDialog.FileName))
                    {
                        // Get the load stream
                        using (Stream loadStream = openFileDialog.OpenFile())
                        {
                            if (loadStream == null)
                            {
                                // Something went wrong with loading
                                return;
                            }

                            string jsonContent = string.Empty;
                            using (StreamReader streamReader = new StreamReader(loadStream))
                            {
                                jsonContent = streamReader.ReadToEnd();
                            }

                            // Deserialize the Json into the current bindings
                            ControlBindingViewModel[] controlBindings = JsonConvert.DeserializeObject<ControlBindingViewModel[]>(jsonContent);

                            // Apply to our observable collection
                            ControlBindings = new ObservableCollection<ControlBindingViewModel>(controlBindings);

                            CurrentBindingName = Path.GetFileName(openFileDialog.FileName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        /// <summary>
        /// Saves the current bindings to disk
        /// </summary>
        private void SaveBindings()
        {
            try
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Control Bindings (*.cb)|*.cb|All files (*.*)|*.*";
                    saveFileDialog.RestoreDirectory = true;

                    // If the user successfully picked a location
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Get the save stream
                        using (Stream saveStream = saveFileDialog.OpenFile())
                        {
                            if (saveStream == null)
                            {
                                // Something went wrong with saving
                                return;
                            }

                            // Serialize the current bindings as JSON
                            string jsonOutput = JsonConvert.SerializeObject(ControlBindings.ToArray());

                            // Write out the json
                            using (StreamWriter streamWriter = new StreamWriter(saveStream))
                            {
                                streamWriter.Write(jsonOutput);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        public void ParseFromJoypad(Joypad joypad)
        {
            ControlBindings.Clear();

            foreach (var kvp in joypad.CommandKeyPairs)
            {
                foreach (var key in kvp.Value)
                {
                    AddNewControlBinding(kvp.Key, (Key)KeyInterop.KeyFromVirtualKey((int)key));
                }
            }
        }

        /// <summary>
        /// Adds a new Control Binding to the list
        /// </summary>
        public void AddNewControlBinding()
        {
            ControlBindings.Add(new ControlBindingViewModel());
            SelectedControlBinding = ControlBindings.Last();
        }

        /// <summary>
        /// Adds a new Control Binding to the list
        /// </summary>
        public void AddNewControlBinding(string message, Key key)
        {
            ControlBindings.Add(new ControlBindingViewModel(message, key));
        }

        /// <summary>
        /// Removes the selected Control Binding from the list
        /// </summary>
        public void RemoveControlBinding()
        {
            if (SelectedControlBinding == null)
            {
                return;
            }

            ControlBindings.Remove(SelectedControlBinding);
            SelectedControlBinding = ControlBindings.LastOrDefault();
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
