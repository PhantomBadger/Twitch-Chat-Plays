using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using TwitchPlaysBot.Windows;

namespace TwitchPlaysBot
{
    /// <summary>
    /// An implementation of <see cref="IDisposable"/> which presses keys identified by chat
    /// </summary>
    public class VirtualKeyPresser : IDisposable
    {
        public delegate void OnKeyPressedDelegate(string username, string message);
        public delegate void OnWatchedProcessClosedDelegate();

        public event OnKeyPressedDelegate OnKeyPressed;
        public event OnWatchedProcessClosedDelegate OnWatchedProcessClosed;

        public string ProcessName
        { 
            get
            {
                if (process != null)
                {
                    return process.ProcessName;
                }
                else
                {
                    return "Unknown";
                }
            }
        }

        private readonly Process process;
        private readonly IntPtr processHandle;
        private readonly BlockingCollection<Keys[]> keyPressQueue;
        private readonly Thread keyPressQueueThread;
        private readonly IRC irc;
        private readonly Joypad joypad;

        public bool CloseProcessOnExit { get; set; }

        /// <summary>
        /// Ctor for creating a <see cref="VirtualKeyPresser"/>
        /// </summary>
        public VirtualKeyPresser(Joypad joypad, IRC irc, int processId)
        {
            this.irc = irc ?? throw new ArgumentNullException(nameof(irc));
            this.joypad = joypad ?? throw new ArgumentNullException(nameof(joypad));

            var internalKeyPressQueue = new ConcurrentQueue<Keys[]>();
            keyPressQueue = new BlockingCollection<Keys[]>(internalKeyPressQueue);
            keyPressQueueThread = new Thread(ProcessPendingKeys);
            keyPressQueueThread.IsBackground = true;
            keyPressQueueThread.Start();

            irc.eventRecievingMessage += new MessageReceived(OnMessagedRecieved);

            // Hook into the specified process
            try
            {
                process = Process.GetProcessById(processId);

                if (process != null)
                {
                    // wait for the process to become available before getting the window handle
                    process.WaitForInputIdle();
                    processHandle = process.MainWindowHandle;

                    // close the overlay if the process exits
                    process.Exited += delegate { OnWatchedProcessClosed?.Invoke(); };

                    System.Diagnostics.Debug.WriteLine("Hooked process ID: " + process.Id);
                    System.Diagnostics.Debug.WriteLine("Hooked process Handle: " + processHandle);

                    // focus process
                    FocusProcessWindow(processHandle);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error when hooking process: " + e.Message, e.InnerException);
            }
        }

        /// <summary>
        /// Implements <see cref="IDisposable.Dispose"/>
        /// </summary>
        public void Dispose()
        {
            irc.eventRecievingMessage -= new MessageReceived(OnMessagedRecieved);

            if (keyPressQueueThread != null)
            {
                // stop processing the queue
                keyPressQueueThread.Abort();
            }

            if (CloseProcessOnExit && process != null)
            {
                // close the process
                process.CloseMainWindow();
                process.Close();
            }
        }

        /// <summary>
        /// Processing thread for dealing with pending keys
        /// </summary>
        private void ProcessPendingKeys()
        {
            while (true)
            {
                if (joypad == null || process == null)
                {
                    continue;
                }

                // Use a blocking collection for efficiency
                Keys[] keys = keyPressQueue.Take();

                if (!ProcessHasFocus(process.Id))
                {
                    FocusProcessWindow(processHandle);
                }

                joypad.PressManyKeys(keys);

                System.Diagnostics.Debug.WriteLine("Using joypad layout: " + joypad.Name);
                System.Diagnostics.Debug.WriteLine("Key press: " + String.Join(" ", keys.Select(k => k.ToString()).ToArray()));
            }
        }

        /// <summary>
        /// Called when the IRC client receives a message
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        private void OnMessagedRecieved(string username, string message)
        {
            message = message.ToLower();

            List<Keys> keys;
            if (joypad.CommandKeyPairs.TryGetValue(message, out keys))
            {
                keyPressQueue.Add(keys.ToArray());
                OnKeyPressed?.Invoke(username, message);
            }
        }

        /// <summary>
        /// Focuses the watched process
        /// </summary>
        private void FocusProcessWindow(IntPtr processHandle)
        {
            if (processHandle == null)
            {
                return;
            }

            // show windows that have been hidden
            ExternalWrappers.ShowWindow(processHandle, WindowShowStyle.Show);
            // show windows that have been minimized
            ExternalWrappers.ShowWindow(processHandle, WindowShowStyle.Restore);
            // finally focus the window
            ExternalWrappers.SetForegroundWindow(processHandle);
        }

        /// <summary>
        /// Checks if the watched process has focus
        /// </summary>
        private bool ProcessHasFocus(int processId)
        {
            if (process == null)
            {
                // process has not been created
                return false;
            }

            // handle for the window that currently has focus
            var activatedHandle = ExternalWrappers.GetForegroundWindow();

            if (activatedHandle == IntPtr.Zero)
            {
                // no window is currently activated
                return false;
            }

            // get the window's process ID
            int activeProcessId;
            ExternalWrappers.GetWindowThreadProcessId(activatedHandle, out activeProcessId);

            // compare it to the embedded process' ID
            return activeProcessId == processId;
        }
    }
}
