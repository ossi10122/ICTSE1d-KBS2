using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using PiaNotes.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.Core;
using Windows.Devices.Midi;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using PiaNotes.Models;
using System.Numerics;
using PiaNotes.Interfaces;
using Melanchall.DryWetMidi.Smf.Interaction;

namespace PiaNotes.Views
{
    /// <summary>
    /// Practice Page. Contains Sheet Music and Keyboard.
    /// </summary>
    public sealed partial class PracticePage : Page
    {
        //DispatcherTimer is the regular timer. It fires its Tick event on the UI thread, you can do anything you want with the UI. System.Timers.Timer is an asynchronous timer, its Elapsed event runs on a thread pool thread. You have to be very careful in your event handler, you are not allowed to touch any UI component or data-bound variables. And you'll need to use the lock statement where ever you access class members that are also used on the UI thread.
        private DispatcherTimer timerGameUI;
        private static Timer timerGameLogic;
        private SheetMusic SM;

        public bool KeyboardIsOpen { get; set; } = true;

        //List for White keys and Black keys of the keyboard
        private List<Rectangle> keysWhite = new List<Rectangle>();
        private List<Rectangle> keysBlack = new List<Rectangle>();

        // Length of 127 because of 127 notes
        private Rectangle[] Notes = new Rectangle[127];
        private int Keys = SettingsPages.MIDI_SettingsPage.OctaveAmount * 12;
        //private double oldWidth;
        private enum PianoKey { C = 0, D = 2, E = 4, F = 5, G = 7, A = 9, B = 11 };
        private enum PianoKeySharp { CSharp = 1, DSharp = 3, FSharp = 6, GSharp = 8, ASharp = 10 };

        // GameCanvas
        private int windowWidth;
        private int windowHeight;

        private int staffStart;
        private int staffEnd;
        private int tickCount;
        private double tickDistance;

        private double gameCanvasWidth;
        private double gameCanvasHeight;

        // Needs to become a settings
        private int FPS = 60;
        private int UPS = 100;


        // Assets
        List<IGameObject> gameObjects = new List<IGameObject>();

        public PracticePage()
        {
            this.InitializeComponent();

            // Initialize the page.
            var appView = ApplicationView.GetForCurrentView();
            appView.Title = "Practice";

            //Titlebar
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;

            // Initialize variables
            windowWidth = Convert.ToInt32(Window.Current.Bounds.Width);
            windowHeight = Convert.ToInt32(Window.Current.Bounds.Height);

            // Subscribes a handler for the MessageReceived event.
            Settings.midiInPort.MessageReceived += MidiInPort_MessageReceived;

            //Generate the amount of Keys
            if (Views.SettingsPages.MIDI_SettingsPage.OctaveAmount != 0)
            {
                Keys = Settings.octaveAmount * 12;
            }
            else Keys = 12;

            //Create the keyboard to show on the screen and set a timer
            CreateKeyboard();
            GameTimerUI();

        }

        private void MidiInPort_MessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            // Converts received message into IMidiMessage.
            IMidiMessage receivedMidiMessage = args.Message;
            System.Diagnostics.Debug.WriteLine(receivedMidiMessage.Timestamp.ToString());

            // Checks if a key has been pressed.
            if (receivedMidiMessage.Type == MidiMessageType.NoteOn)
            {
                // Debug lines to show the Channel Note and Velocity output from the keyboard
                System.Diagnostics.Debug.WriteLine("Channel: " + ((MidiNoteOnMessage)receivedMidiMessage).Channel);
                System.Diagnostics.Debug.WriteLine("Note: " + ((MidiNoteOnMessage)receivedMidiMessage).Note);
                System.Diagnostics.Debug.WriteLine("Velocity: " + ((MidiNoteOnMessage)receivedMidiMessage).Velocity);

                // Retrieves channel, note from the MidiMessage, and sets the velocity.
                byte channel = ((MidiNoteOnMessage)receivedMidiMessage).Channel;
                byte note = ((MidiNoteOnMessage)receivedMidiMessage).Note;
                byte velocity;

                // If the player releases the key there should be no sound
                if (((MidiNoteOnMessage)receivedMidiMessage).Velocity != 0)
                {
                    if (Settings.disableUserFeedback)
                    {
                        // Retrieves the velocity from the played note and then adds the amount of volume the user has set.
                        velocity = ((MidiNoteOnMessage)receivedMidiMessage).Velocity;

                        //If the velocity surpases the limits of MIDI it will go to the limit, otherwise it will act normally
                        if (velocity + DoubleToByte(Settings.volume) <= 127 && velocity + DoubleToByte(Settings.volume) >= 0)
                        {
                            velocity += DoubleToByte(Settings.volume);
                        } else if (velocity + DoubleToByte(Settings.volume) > 127)
                        {
                            velocity = 127;
                        } else
                        {
                            velocity = 0;
                        }
                        // Else use the static velocity the user chose.
                    } else velocity = DoubleToByte(Settings.velocity);
                    // Else use velocity from the midimessage, if below a certain amount, no sound will be played.
                } else velocity = ((MidiNoteOnMessage)receivedMidiMessage).Velocity;

                // Creates the message that will be send to play.
                IMidiMessage midiMessageToSend = new MidiNoteOnMessage(channel, note, velocity);
                FillKey(midiMessageToSend);
                Settings.midiOutPort.SendMessage(midiMessageToSend);
            }

            // Checks if note has been released.
            if (receivedMidiMessage.Type == MidiMessageType.NoteOff)
            {
                //Debug lines to show the Channel Note and Velocity output from the keyboard
                System.Diagnostics.Debug.WriteLine(((MidiNoteOffMessage)receivedMidiMessage).Channel);
                System.Diagnostics.Debug.WriteLine(((MidiNoteOffMessage)receivedMidiMessage).Note);
                System.Diagnostics.Debug.WriteLine(((MidiNoteOffMessage)receivedMidiMessage).Velocity);

                // Retrieves channel, note and velocity from the MidiMessage.
                byte channel = ((MidiNoteOffMessage)receivedMidiMessage).Channel;
                byte note = ((MidiNoteOffMessage)receivedMidiMessage).Note;
                byte velocity = ((MidiNoteOffMessage)receivedMidiMessage).Velocity;

                // Creates a message solely for the purpose of changing the key-color back to its default color.
                IMidiMessage midiMessageToSend = new MidiNoteOffMessage(channel, note, velocity);
                UnFillKey(midiMessageToSend);
            }

        }

        // Method for coloring the played key
        private async void FillKey(IMidiMessage IM)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                byte note = ((MidiNoteOnMessage)IM).Note;

                // Try-catch for coloring the keys
                try
                {
                    byte neg = 25;

                    // If it is a black key, the color will be slightly darker (-25 on all values except A) than a white key.
                    // Colors will be pulled from Settings.cs.
                    if (Notes[note].Name.Contains("Sharp"))
                    {
                        Notes[note].Fill = new SolidColorBrush(Color.FromArgb(255, DoubleToByte(Settings.redValue - neg), DoubleToByte(Settings.greenValue - neg), DoubleToByte(Settings.blueValue - neg)));
                    }
                    else
                    {
                        Notes[note].Fill = new SolidColorBrush(Color.FromArgb(255, Settings.redValue, Settings.greenValue, Settings.blueValue));
                    }
                }
                catch (Exception e)
                {
                    // If it doesn't work, display the error message in the debug console
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }
            });
        }

        // Method for changing the color of the played key back to its default color.
        private async void UnFillKey(IMidiMessage IM)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                byte note = ((MidiNoteOffMessage)IM).Note;
                try
                {
                    // The keys original colour will be restored
                    if (Notes[note].Name.Contains("Sharp"))
                    {
                        Notes[note].Fill = new SolidColorBrush(Windows.UI.Colors.Black);
                    } else
                    {
                        Notes[note].Fill = new SolidColorBrush(Windows.UI.Colors.White);
                    }
                }
                catch (Exception e)
                {
                    // If it doesn't work, display the error message in the debug console
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }
            });
        }

        // Method for converting a double to byte.
        public static byte DoubleToByte(double doubleVal)
        {
            byte byteVal = 0;

            // Double to byte conversion can overflow.
            try
            {
                byteVal = Convert.ToByte(doubleVal);
                return byteVal;
            }
            catch (OverflowException)
            {
                //If it doesn't work, display the error message in the debug console
                System.Diagnostics.Debug.WriteLine("Overflow in double-to-byte conversion.");
            }

            // Byte to double conversion cannot overflow.
            doubleVal = System.Convert.ToDouble(byteVal);
            return byteVal;
        }

        // Menustrip: View > Keyboard
        private void ViewKeyboard_Click(object sender, RoutedEventArgs e)
        {
            ToggleKeyboard();
        }

        // Toggles the keyboard to show/hide.
        public void ToggleKeyboard()
        {
            // Iterates through all keyboard items to hide/show them.
            if (KeyboardIsOpen)
            {
                foreach (Rectangle key in KeysWhiteSP.Children)
                {
                    key.Width = 0;
                }
                foreach (Rectangle key in KeysBlackSP.Children)
                {
                    key.Width = 0;
                }
                KeyboardBG.MinHeight = 0;
            }
            else
            {
                CreateKeyboard();
                KeyboardBG.MinHeight = 200;
            }
            KeyboardIsOpen = !KeyboardIsOpen;
        }


        // Creates the keyboard.
        public void CreateKeyboard()
        {
            int oct = 0;

            //First go through each Octave to make keys
            for (int i = Settings.startingOctave; i < (Settings.octaveAmount + Settings.startingOctave); i++)
            {
                //In each Octave make 12 keys, 7 white and 5 black keys
                for (int j = 0; j < 12; j++)
                {
                    //See if the key is a white or black key
                    switch (j)
                    {
                        case 1:
                            AddKey(false, j, i);
                            break;
                        case 3:
                            AddKey(false, j, i);
                            break;
                        case 6:
                            AddKey(false, j, i);
                            break;
                        case 8:
                            AddKey(false, j, i);
                            break;
                        case 10:
                            AddKey(false, j, i);
                            break;
                        default:
                            AddKey(true, j, i);
                            break;
                    }
                    oct = i + 1;
                }
            }
            //Add an extra C note at the end
            AddKey(true, 0, oct);
            UpdateKeyboard();
        }

        public void AddKey(bool white, int j, int i)
        {
            if(white)
            {
                Rectangle keyWhiteRect = new Rectangle
                {
                    Name = $"{((PianoKey)j).ToString()}{i}",
                    Stroke = new SolidColorBrush(Colors.Black),
                    Fill = new SolidColorBrush(Colors.White),
                    StrokeThickness = 4,
                    Height = 200
                };
                KeysWhiteSP.Children.Add(keyWhiteRect);
                System.Diagnostics.Debug.WriteLine(keyWhiteRect.Name);
                if (j == 0)
                {
                    if (i == 0) Notes[j] = (keyWhiteRect);
                    else Notes[(i * 12)] = (keyWhiteRect);
                }
                else
                {
                    if (i == 0) Notes[j] = (keyWhiteRect);
                    else Notes[(j + (i * 12))] = (keyWhiteRect);
                }
            }
            else
            {
                Rectangle keyBlackRect = new Rectangle
                {
                    Name = $"{((PianoKeySharp)j).ToString()}{i}",
                    Fill = new SolidColorBrush(Colors.Black),
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 4,
                    Height = 150
                };
                KeysBlackSP.Children.Add(keyBlackRect);
                if (i == 0) Notes[j] = (keyBlackRect);
                else Notes[(j + (i * 12))] = (keyBlackRect);
            }
        }

        // Updates the keyboard. Is used after first initializing the keyboard or after resizing the window width.
        public void UpdateKeyboard()
        {
            double keyWidthWhite = 40;

            // Counts amount of white keys.
            int keyWhiteAmount = 8;
            if (Settings.octaveAmount != 0) keyWhiteAmount = (Keys - (Settings.octaveAmount * 5) + 1);

            // Sets width for white keys.
            foreach (Rectangle key in KeysWhiteSP.Children)
            {
                try
                {
                    // Calculates width for the white keys.
                    keyWidthWhite = (windowWidth / keyWhiteAmount);
                    key.Width = keyWidthWhite;
                }
                catch (Exception)
                {
                    // If width can't be calculated, change width to a set value.
                    key.Width = keyWidthWhite;
                }
            }

            // Sets width and location for black keys.
            bool initialCsharp = true;
            foreach (Rectangle key in KeysBlackSP.Children)
            {
                //double keyWhiteWidth;
                try
                {
                    // Calculates width for the black keys.
                    key.Width = keyWidthWhite / 100 * 60;

                    if (key.Name.Contains("CSharp"))
                    {
                        // Calculates location for C# key.
                        // The first key has a different calculation than the rest, because there are no prior keys.
                        double location;
                        if (initialCsharp)
                        {
                            location = keyWidthWhite - (key.Width / 2);
                            initialCsharp = false;
                        }
                        else location = (keyWidthWhite - (key.Width / 2)) * 2;
                        key.Margin = new Thickness(location, 0, 0, 50);
                    }
                    else if (key.Name.Contains("DSharp") || key.Name.Contains("GSharp") || key.Name.Contains("ASharp"))
                    {
                        // Calculates location for D#/G#/A# keys.
                        double location = keyWidthWhite - key.Width;
                        key.Margin = new Thickness(location, 0, 0, 50);
                    }
                    else if (key.Name.Contains("FSharp"))
                    {
                        // Calculates location for F# key.
                        double location = keyWidthWhite * 2 - key.Width;
                        key.Margin = new Thickness(location, 0, 0, 50);
                    }
                } catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }
            }
        }

        /// <summary>
        /// Logic Thread where game logic gets updated
        /// </summary>

        private void GameTimerLogic()
        {
            // Create a timer with a sixty-fourth tick which represents the 1/64 note.
            timerGameLogic = new Timer(1000 / UPS)
            {
                AutoReset = true,
                Enabled = true
            };

            // Hook up the Elapsed event for the timer. 
            timerGameLogic.Elapsed += GameTickLogic;
        }

        private void GameTickLogic(Object source, ElapsedEventArgs e)
        {
            tickCount += 1000000/UPS;

            for (int i = 0; i < SM.notes.Count && i < 6; i++)
            {
                if (tickCount >= SM.notes[i].MetricTiming.TotalMicroseconds)
                {
                    gameObjects.Add(SM.notes[i]);
                    SM.notes.Remove(SM.notes[i]);
                }
            }

            for (int i = 0; i < gameObjects.Count; i++)
            {
                if (gameObjects[i].Location.X <= staffStart)
                { 
                    gameObjects.Remove(gameObjects[i]);
                    return;
                }
                gameObjects[i].Location = new Vector2(gameObjects[i].Location.X - (float)tickDistance,gameObjects[i].Location.Y);
            }
        }

        /// <summary>
        /// GameCanvas is a Win2D canvas which makes 2D graphics rendering with GPU acceleration possible.
        /// This includes all kind of cool stuf as particles, effects etc...
        /// </summary> 

        private void GameTimerUI()
        {
            timerGameUI = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000 / FPS)
            };
            timerGameUI.Tick += GameTickUI;
            timerGameUI.Start();
        }

        private void GameTickUI(object sender, object e)
        {
            // Redraw screen.
            GameCanvas.Invalidate();
        }

        // Initialize images and stuff.
        private void GameCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            staffStart = 24;
            staffEnd = windowWidth - staffStart;
            tickDistance = (windowWidth * 0.80) / 600;
            gameCanvasHeight = sender.ActualHeight;
            gameCanvasWidth = sender.ActualWidth;

            //Create staff

            args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
        }

        private async Task CreateResourcesAsync(CanvasControl sender)
        {
            // Add the resources to the ContentPipeline for reuse purposes
            ContentPipeline.ParentCanvas = sender;
            
            await ContentPipeline.AddImage("1", @"Assets/Notes/WholeNote.png");
            await ContentPipeline.AddImage("0,5", @"Assets/Notes/HalfNote.png");
            await ContentPipeline.AddImage("0,25", @"Assets/Notes/QuarterNote.png");
            await ContentPipeline.AddImage("0,125", @"Assets/Notes/EighthNote.png");
            await ContentPipeline.AddImage("0,0625", @"Assets/Notes/SixteenthNote.png");
            await ContentPipeline.AddImage("0,03125", @"Assets/Notes/ThirtySecondNote.png");
            
            int tpqn = SM.TicksPerQuaterNote;
            
            // Create array with all flat keys in one octave.
            int[] keyFlatOctave = new int[] { 0, 2, 4, 5, 7, 9, 11 };
            // Create list for all flat keys.
            List<int> flatKeysAll = new List<int>();
            // Add all flat key numbers to list.
            for (int i = 0; i < 11; i++)
            {
                for (int j = 0; j < keyFlatOctave.Length; j++)
                {
                    flatKeysAll.Add(keyFlatOctave[j] + 12*(i+1));
                }
            }
            // Get highest and lowest notes.
            int max = 0;
            int min = 127;
            for (int i = 0; i < SM.notes.Count; i++)
            {
                if (SM.notes[i].Number > max)
                {
                    max = SM.notes[i].Number;
                }
                if (SM.notes[i].Number < min)
                {
                    min = SM.notes[i].Number;
                }
            }

            // Get highest and lowest octave.
            int highestOctave = 0;
            int lowestOctave = 9;
            for (int i = 0; i < 10; i++)
            {
                if (i*12 < max)
                {
                    highestOctave = i + 1;
                }
            }
            for (int i = 10; i > 0; i--)
            {
                if (i * 12 > min)
                {
                    lowestOctave = i + 1;
                }
            }

            // Give the notes a bitmap
            for (int i = 0; i < SM.notes.Count; i++)
            {
                SM.notes[i].SetBitmap(SM.notes[i].NoteType.ToString());
                //SM.notes[i].SetBitmap("0.25");
                SM.notes[i].SetSize(30, 30);
                int staffSpacing = 8;
                int key = 0;

                // Check if key is flat.
                if (flatKeysAll.Contains(SM.notes[i].Number))
                {
                    // Key is flat.
                    key = SM.notes[i].Number;
                }
                else
                {
                    // Key is not flat.
                    key = SM.notes[i].Number - 1;
                }
                
                
                // Set location of each note.
                int index = flatKeysAll.IndexOf(SM.notes[i].Number);
                int negativeNote = (highestOctave * 7 * -1) + index;
                int notePos = Math.Abs(negativeNote * staffSpacing) - 27;
                SM.notes[i].Location = new Vector2(staffEnd, notePos);
                
                
            }
            GameTimerLogic();
        }
        
        private void GameCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            int staffMargin = 24;
            int staffWidth = (int)gameCanvasWidth - staffMargin;
            int staffSpacing = 8;
            
            // Create lines for right hand.
            for (int i = 0; i < 13; i++)
            {
                int y = staffSpacing * (i + 1) + staffMargin;
                if (i == 0 || i == 12)
                {
                    args.DrawingSession.DrawLine(staffMargin, y, staffWidth, y, Colors.Transparent);
                }
                else if (i % 2 == 0)
                {
                    args.DrawingSession.DrawLine(staffMargin, y, staffWidth, y, Colors.White);
                }
            }

            // Draw line.
            args.DrawingSession.DrawLine(staffMargin * 3,
                staffSpacing * (2 + 1) + staffMargin,
                staffMargin * 3,
                staffSpacing * (10 + 1) + staffMargin,
                Colors.White);
            
            
            // Create lines for left hand.
            for (int i = 0; i < 13; i++)
            {
                int y = staffSpacing * (i + 1) + 12 * staffSpacing + (staffMargin);
                if (i == 0 || i == 12)
                {
                    args.DrawingSession.DrawLine(staffMargin, y, staffWidth, y, Colors.Transparent);
                }
                else if (i % 2 == 0)
                {
                    args.DrawingSession.DrawLine(staffMargin, y, staffWidth, y, Colors.White);
                }
            }

            // Draw line.
            args.DrawingSession.DrawLine(staffMargin * 3,
                staffSpacing * (2 + 1) + 12 * staffSpacing + staffMargin,
                staffMargin * 3,
                staffSpacing * (10 + 1) + 12 * staffSpacing + staffMargin,
                Colors.White);
            
            // Draw notes.
            for (int i = 0; i < gameObjects.Count; i++)
            {
                if (gameObjects[i] != null)
                    args.DrawingSession.DrawImage(gameObjects[i].Bitmap, gameObjects[i].Location);
            }
        }

        /// <summary>
        /// On click events navigation
        /// </summary>

        // Navigate to the settings page
        private void NavSettings_Click(object sender, RoutedEventArgs e) => this.Frame.Navigate(typeof(SettingsPage));

        // Navigate to the credits page
        private void NavCredits_Click(object sender, RoutedEventArgs e) => this.Frame.Navigate(typeof(CreditsPage));

        // Navigate to the selection page
        private void NavSelection_Click(object sender, RoutedEventArgs e) => this.Frame.Navigate(typeof(SelectionPage));

        /// <summary>
        /// Page events
        /// </summary>

        // Handler for when navigated to this page.
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            SM = (SheetMusic)e.Parameter;
        }

        // Handler for when the page is unloaded
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop the GameLoop and UIloop
            timerGameLogic.Stop();
            timerGameUI.Stop();

            // Unsubscribe the MidiInPort_MessageReceived
            Settings.midiInPort.MessageReceived -= MidiInPort_MessageReceived;

            // Dispose of the Win2D resources
            this.GameCanvas.RemoveFromVisualTree();
            this.GameCanvas = null;

            // Clear the ContentPipeline
            ContentPipeline.ImageDictionary.Clear();
        }

        // Handler for when the page is resized
        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            
            if (KeyboardIsOpen)
                // If the keyboard is shown, it will be updated.
                UpdateKeyboard();

            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
        }
    }
}
