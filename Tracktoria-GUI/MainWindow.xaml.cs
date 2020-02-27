using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Threading;
using System.Windows.Threading;

namespace Tracktoria
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    ///         SerialPort _serial = new SerialPort();
    ///         

    public partial class MainWindow : Window
    {
        public delegate void UpdateTextDelegate(string message);

        //timer for play function
        static DispatcherTimer playback;
        //variables to hold state of playback
        static int step = 0;
        static List<Instrument> play_instruments = new List<Instrument> { };
        static List<int> play_channels = new List<int> { };

        public static bool debug = true;
        public static SerialPort _serial = new SerialPort();

        //reference to the instrument open in editor
        public static Instrument activeInstr = new Instrument();
        
        //list of all the instruments user makes
        ObservableCollection<Instrument> instrList = new ObservableCollection<Instrument> { new Instrument() };

        public MainWindow()
        {
            InitializeComponent();
            //set up serial paramters that dont change
            _serial.BaudRate = 250000;
            _serial.Handshake = System.IO.Ports.Handshake.None;
            _serial.Parity = Parity.None;
            _serial.DataBits = 8;
            _serial.StopBits = StopBits.One;
            _serial.ReadTimeout = 2000;
            _serial.WriteTimeout = 500;
            _serial.DataReceived += new SerialDataReceivedEventHandler(SerialReceived);

            playback = new System.Windows.Threading.DispatcherTimer();
            playback.Tick += new EventHandler(play_step);

            //add default track to track viewer
            addTrackRow(new Track(), Convert.ToInt32(numColumns.Text));
            //load up default instrument in that panel
            LoadInstrument(new Instrument());
        }

        private void SerialReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            string data = _serial.ReadLine();   //get serial data
            Console.WriteLine(data);
            Dispatcher.Invoke(DispatcherPriority.Send, new UpdateTextDelegate(ProcessPacket), data);    //push the update function onto the dispatcher with the new serial data
        }

        private void ProcessPacket(string Packet)
        {
            //PrintMessage(Packet.Length.ToString());
            if (Packet.Length == 43)                        //1. check packet size
            {
                if (Packet.Substring(0, 3) == "###")        //2. check start code
                {
                    int RemoteChecksum = Int16.Parse(Packet.Substring(39, 3));
                    int LocalChecksum = 0;
                    string data = Packet.Substring(3, 36);
                    foreach (char digit in data) LocalChecksum += (digit);
                    LocalChecksum %= 1000;

                    if (LocalChecksum == RemoteChecksum)     //3. check checksums
                    {                                        //4. extract values                                                        
                        int A0 = int.Parse(data.Substring(3, 4));
                        int A1 = int.Parse(data.Substring(7, 4));
                        int A2 = int.Parse(data.Substring(11, 4));
                        int A3 = int.Parse(data.Substring(15, 4));
                        int A4 = int.Parse(data.Substring(19, 4));
                        int A5 = int.Parse(data.Substring(23, 4));

                        bool btn = data[27] == '1' ? true : false;
                        bool D0 = data[28] == '1' ? true : false;
                        bool D1 = data[29] == '1' ? true : false;
                        bool D2 = data[30] == '1' ? true : false;
                        bool D3 = data[31] == '1' ? true : false;
                        bool D4 = data[32] == '1' ? true : false;
                        bool D5 = data[33] == '1' ? true : false;
                        bool D6 = data[34] == '1' ? true : false;
                        bool D7 = data[35] == '1' ? true : false;

                        //stop playback if button pressed
                        if (btn) playback.Stop();

                        //USE a0 TO SET TEMPO
                        double Ascale = (double)A0 / 4095;
                        //Console.WriteLine(Ascale.ToString());
                        int bpmOffset = (int)(100 * Ascale);
                        bpmBox.Text = (50 + bpmOffset).ToString();
                    }
                }
            }
        }

        private void addTrackRow(Track _track, int colNum)
        {
            TrackPanel trackRow = new TrackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 30,
                Background = new SolidColorBrush { Color = Color.FromRgb(65, 71, 75) },
                Margin = new Thickness(2),
                //childTrack = _track
            };
            trackViewer.Children.Add(trackRow);

            Button destroy = new Button
            {
                Content = "X",
                Height = 25,
                Width = 20,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            destroy.Click += new RoutedEventHandler(deleteTrack);
            trackRow.Children.Add(destroy);

            TextBox trackTxt = new TextBox
            {
                Name = "trackTitle",
                Height = 30,
                Width = 60,
                Text = _track.Name,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            trackTxt.TextChanged += new TextChangedEventHandler(renameTrack);
            trackRow.Children.Add(trackTxt);

            ComboBox instSelect = new ComboBox
            {
                Name = "trackInstr",
                Width = 70,
                ItemsSource = instrList,
                SelectedItem = _track.instr,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            instSelect.SelectionChanged += new SelectionChangedEventHandler(selectInstrument);
            trackRow.Children.Add(instSelect);

            ComboBox channelSelect = new ComboBox
            {
                Name = "trackChannel",
                Width = 70,
                ItemsSource = channelList,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            channelSelect.SelectionChanged += new SelectionChangedEventHandler(selectChannel);
            trackRow.Children.Add(channelSelect);

            for (int i = 0; i < colNum; i++)
            {
                string celldata;
                if (i > _track.events.Count - 1)
                    celldata = "0";
                else celldata = _track.events[i].ToString();

                trackRow.Children.Add(new TextBox
                {
                    Name = "cell" + i.ToString(),
                    Height = 30,
                    Width = 30,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Text = celldata
                });
            }
        }

        public static void SendPacket(string command, string value) //sid command version
        {
            if (_serial.IsOpen == false) return;

            string packet = command + value;

            int checksum = 0;                                       //calc checksum
            foreach (char digit in packet) checksum += digit;
            checksum %= 1000;

            packet = "$$$" + packet + checksum.ToString();      //put packet together

            if(debug==true)Console.WriteLine(packet);
            _serial.Write(packet);
        }

        public static void SendPacket(string packet)                //GPIO output version
        {
            if (_serial.IsOpen == false) return;

            int checksum = 0;
            foreach (char digit in packet) checksum += digit;
            checksum %= 1000;

            packet = "###" + packet + checksum.ToString();

            if(debug==true)Console.WriteLine(packet);
            _serial.Write(packet);
        }

        void Log(string message)
        {
            DateTime now = System.DateTime.Now;
            string timestamp = now.Hour.ToString("D2") + ":" + now.Minute.ToString("D2") + ":" + now.Second.ToString("D2") + "> ";
            console_txtbox.Text += timestamp + message + "\n";

        }

        void updateADSRdisplay(Envelope _env)
        {
            attackLine.X2 = 70 * _env.attack / 15;

            Canvas.SetLeft(decayLine, attackLine.X2);
            decayLine.X2 = 70 * _env.decay / 15;
            decayLine.Y2 = 100 * _env.sustain / 15 + 5;

            Canvas.SetLeft(sustainLine, decayLine.X2 + attackLine.X2);
            sustainLine.Y2 = decayLine.Y2;
            sustainLine.Y1 = sustainLine.Y2;

            Canvas.SetLeft(releaseLine, decayLine.X2 + attackLine.X2 + sustainLine.X2);
            releaseLine.Y1 = sustainLine.Y2;
            releaseLine.X2 = 50 * _env.release / 15;

            double canvasScale = (double)(58 - _env.attack - _env.decay - _env.release) / (double)58;
            int canvasLeft = (int)(canvasScale * 110);
            ADSRdisplay.Margin = new Thickness(canvasLeft, 5, 5, 5);
        }

        void LoadInstrument(Instrument i)
        {
            instrumentTxt.Text = i.Name;
            activeInstr = i;
            Abox.SelectedIndex = i.ADSR.attack;
            Dbox.SelectedIndex = i.ADSR.decay;
            Sbox.SelectedIndex = i.ADSR.sustain;
            Rbox.SelectedIndex = i.ADSR.release;

            pwSlider.Value = i.pulsewidth;
            switch (i.waveform)
            {
                case (0)://tri
                    {
                        trbutton.IsChecked = true;
                        break;
                    }
                case (1): //saw
                    {
                        swbutton.IsChecked = true;
                        break;
                    }
                case (2)://sq
                    {
                        sqbutton.IsChecked = true;
                        break;
                    }
                case (3)://noise
                    {
                        nobutton.IsChecked = true;
                        break;
                    }
            }

            filtBox.IsChecked = i.filter.EN;
            switch (i.filter.mode)
            {
                case (0)://lp?
                    {
                        lpbtn.IsChecked = true;
                        break;
                    }
                case (1):
                    {
                        break;
                    }
                case (2):
                    {
                        break;
                    }
            }
            freqslider.Value = i.filter.frequency;
            resslider.Value = i.filter.resonance;
        }

        int CharToIndex(char c)
        {
            switch (c)
            {
                case ('C') :
                    
                        return 11;                        
                    
                case ('B') :
                    
                        return 0;
                    
                case ('A'):
                    
                        return 2;
                    
                case 'G':
                    
                        return 4;
                    
                case 'F':
                        
                            return 6;
                        
                case 'E':
                            return 7;

                case 'D':
                            return 9;

                default :
                        return 99;
            }
        }

        #region event handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            for (int octave = 0; octave < 7; octave++)
            {
                for (int letter = 0; letter < 7; letter++)
                {
                    var keybutton = new Button();
                    keybutton.Background = (SolidColorBrush)Resources["PianoKey"];
                    keybutton.Name = 'k' + letter.ToString() + octave.ToString();
                    keybutton.Click += new RoutedEventHandler(key_Click);
                    keyboard.Children.Add(keybutton);
                }
            }

            Log("Welcome to Tracktoria!");

            #region timing-sensitive event handlers
            Abox.SelectionChanged += new SelectionChangedEventHandler(ADSR_SelectionChanged);
            Dbox.SelectionChanged += new SelectionChangedEventHandler(ADSR_SelectionChanged);
            Sbox.SelectionChanged += new SelectionChangedEventHandler(ADSR_SelectionChanged);
            Rbox.SelectionChanged += new SelectionChangedEventHandler(ADSR_SelectionChanged);
            Abox.ItemsSource = attackList;
            Dbox.ItemsSource = decayList;
            Sbox.ItemsSource = ampList;
            Rbox.ItemsSource = decayList;
            pwSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(pwSlider_ValueChanged);
            //menu_openInstrument.ItemsSource = instrList;

            #endregion
        }
        
        private void playbutton_Click(object sender, RoutedEventArgs e)
        {
            //calculate timer interval from bpm box
            double bpm = Convert.ToDouble(bpmBox.Text);
            double period = 1 / (bpm / 60);
            playback.Interval = TimeSpan.FromMilliseconds((int)(period * 1000));

            //collect basic info from tracks: instrument & channel
            play_instruments.Clear();
            play_channels.Clear();
            foreach (TrackPanel track in trackViewer.Children)
            {
                ComboBox i = (ComboBox)track.Children[2];
                play_instruments.Add((Instrument)(i.SelectedItem));
                if (debug == true) Console.WriteLine((i.SelectedItem).ToString());
                ComboBox j = (ComboBox)track.Children[3];
                string voice = (string)j.SelectedItem;
                play_channels.Add(voice[6]-0X30);
            }

            //start timer
            playback.Start();            
        }

        private void stopbutton_Click(object sender, RoutedEventArgs e)
        {
            playback.Stop();
        }

        private void play_step(object sender, EventArgs e)
        {
            int columns = Convert.ToInt32(numColumns.Text);

            if (++step >= columns) step = 0;

            //display current step on leds
            string leds = Convert.ToString((1 << step % 8), 2);
            SendPacket(leds.PadLeft(8, '0'));

            int numTracks = trackViewer.Children.Count;
            //get cell at step for each track panel
            for (int i = 0; i < numTracks; i++ )
            {
                TrackPanel _track = (TrackPanel)trackViewer.Children[i];
                TextBox t = (TextBox)(_track.Children)[step + 4];
                int octave = 0;
                string note = t.Text;
                if (note == "0")
                {
                    if (debug == true) Console.WriteLine("empty cell, skipping");
                }
                else
                {
                    int letter = CharToIndex(note[0]);
                    if (note[1] == 's')
                    {
                        letter++;
                        octave = note[2] - 0x30;
                    }
                    else
                    {
                        octave = note[1] - 0x30;
                    }

                    if (debug == true) Console.WriteLine("playing " + letter.ToString() + octave.ToString());
                    play_instruments[i].Play(play_channels[i], Notes.table[letter, octave]);
                    Thread.Sleep(2);
                }

            }
        }

        public void menuclick_openPort(Object sender, RoutedEventArgs e)
        {
            if (_serial.IsOpen)
            {
                Log("Closing serial connection on " + _serial.PortName);
                _serial.Close();
            }

            MenuItem clicked = sender as MenuItem;
            _serial.PortName = clicked.Header.ToString(); //Com Port Name               
            _serial.Open();
            Log("Opening serial connection on " + _serial.PortName);

        }

        private void key_Click(object sender, RoutedEventArgs e)
        {
            Button clickedKey = sender as Button;
            string note = clickedKey.Name.ToString();

            string freqHex = Notes.table[note[1] - 0x30, note[2] - 0x30];

            activeInstr.Play(0, freqHex);
            if (debug == true) Console.WriteLine("play a note:");
        }

        private void getCOMPorts(object sender, MouseEventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();

            this.menu_openPort.Items.Clear();

            foreach (string port in ports)
            {
                MenuItem portItem = new MenuItem();
                portItem.Click += new RoutedEventHandler(menuclick_openPort);
                portItem.Header = port;
                this.menu_openPort.Items.Add(portItem);
            }
        }

        private void getInstruments(object sender, MouseEventArgs e)
        {
            this.menu_openInstrument.Items.Clear();
            foreach (var item in instrList)
            {
                MenuItem portItem = new MenuItem();
                portItem.Click += new RoutedEventHandler(menuclick_openInstrument);
                portItem.Header = item.Name;
                this.menu_openInstrument.Items.Add(portItem);
            }
        }

        public void menuclick_openInstrument(Object sender, RoutedEventArgs e)
        {
            MenuItem o = (MenuItem)sender;
            MenuItem p = (MenuItem)o.Parent;
            LoadInstrument(instrList[p.Items.IndexOf(o)]);
        }

        private void btn_closePort_Click(object sender, RoutedEventArgs e)
        {
            if (_serial.IsOpen == true)
            {
                _serial.Close();
                Log("Closing serial connection on " + _serial.PortName);
            }
        }

        private void newtrackbutton_Click(object sender, RoutedEventArgs e)
        {
            addTrackRow(new Track(), Convert.ToInt32(numColumns.Text));
        }

        public void renameTrack(object sender, TextChangedEventArgs e)
        {
           // TextBox o = (TextBox)sender;
           // TrackPanel p = (TrackPanel)o.Parent;
            ///p.childTrack.Name = o.Text;
        }

        public void selectInstrument(object sender, SelectionChangedEventArgs e)
        {
        //    ComboBox o = (ComboBox)sender;
        //    TrackPanel p = (TrackPanel)o.Parent;
        //    p.childTrack.instr = (Instrument)o.SelectedItem;
        }

        public void selectChannel(object sender, SelectionChangedEventArgs e)
        {
            //ComboBox o = (ComboBox)sender;
            //TrackPanel p = (TrackPanel)o.Parent;
            //p.childTrack.channel = ((string)o.SelectedItem)[6] - 0x30;
        }

        private void ADSR_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //update current instrument with new envelope
            activeInstr.ADSR = new Envelope { attack = Convert.ToInt32(Abox.SelectedIndex), decay = Convert.ToInt32(Dbox.SelectedIndex), sustain = Convert.ToInt32(Sbox.SelectedIndex), release = Convert.ToInt32(Rbox.SelectedIndex) };

            //update the envelope display
            updateADSRdisplay(activeInstr.ADSR);
        }

        private void pwSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            pwLabel.Text = Math.Round((pwSlider.Value / 40.95), 1).ToString() + " %";
            activeInstr.pulsewidth = (int)e.NewValue;
        }

        private void sqbutton_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.waveform = 2;
        }

        private void trbutton_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.waveform = 0;
        }

        private void swbutton_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.waveform = 1;
        }

        private void nobutton_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.waveform = 3;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.filter.EN = (bool)((CheckBox)sender).IsChecked;
        }

        private void lpbtn_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.filter.mode = 0;
        }

        private void bpbtn_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.filter.mode = 1;
        }

        private void hpbtn_Checked(object sender, RoutedEventArgs e)
        {
            activeInstr.filter.mode = 2;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            activeInstr.filter.frequency = (int)e.NewValue;
        }

        private void Slider_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            activeInstr.filter.resonance = (int)e.NewValue;
        }
        //add/replace instrument in list
        private void menu_saveInstrument_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine(activeInstr.Name);
            //if name hasn't changed, overwrite the original
            if (activeInstr.Name == instrumentTxt.Text)
            {
                //lets try to find the itme with the right name in this list
                for (int i = 0; i < instrList.Count - 1; i++)
                {
                    if (debug == true) Console.WriteLine("checking" + instrList[i].Name);
                    if (instrList[i].Name.Equals(activeInstr.Name))//ok match
                    {
                        //remove the existing one
                        if (debug == true) Console.WriteLine("delete entry:" + instrList[i].Name);
                        instrList.RemoveAt(i);
                        //write new one
                        //MAKE A NEW OBJECT                        
                        instrList.Add(new Instrument(activeInstr));
                        return; //stop wrecking stuff
                    }
                }
            }
            else
            { //otherwise this is a band new instrument dont forget to get th enew name
                if (debug == true) Console.WriteLine("new entry:" + instrumentTxt.Text);
                activeInstr.Name = instrumentTxt.Text;
                instrList.Add(new Instrument(activeInstr));
            }
        }

        private void deleteTrack(object sender, RoutedEventArgs e)
        {
            Button o = (Button)sender;
            TrackPanel p = (TrackPanel)o.Parent;
            StackPanel pp = (StackPanel)p.Parent;
            pp.Children.Remove(p);
        }

        private void debugger_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox o = (CheckBox)sender;
            debug = o.IsChecked == true;
        }
        
        #endregion

        #region misc classes
        public class Instrument
        {
            public string Name { get; set; }
            public int waveform { get; set; }
            public Envelope ADSR { get; set; }
            public FilterSettings filter { get; set; }
            public int pulsewidth { get; set; }

            public Instrument()
            {
                Name = "Untitled";
                waveform = 2;
                ADSR = new Envelope();
                filter = new FilterSettings();
                pulsewidth = 0;
            }

            public Instrument(Instrument clone)
            {
                Name = clone.Name;
                waveform = clone.waveform;
                ADSR = clone.ADSR;
                filter = clone.filter;
                pulsewidth = clone.pulsewidth;
            }

            public void Play(int voice, int _frequency)
            {
                string frequency = _frequency.ToString();


                var ins = new List<Instruction> {
                new Instruction {code = "TRG", value = (char)(voice + 0x30) + "0000"},                          //turn off the voice
                new Instruction {code = "VWF", value = (char)(voice + 0x30) + waveform.ToString("D4")},         //set desired waveform
                new Instruction {code = "VFQ", value = (char)(voice + 0x30) + frequency},                       //set voice frequency
                new Instruction {code = "ENV", value = (char)(voice + 0x30) + ADSR.ToString()},                 //set envelope
            };

                if (waveform == 2) ins.Add(
                    new Instruction { code = "VPW", value = (char)(voice + 0x30) + pulsewidth.ToString("D4") });    //set pulsewidth if square wave selected

                if (filter.EN)
                {
                    ins.Add(new Instruction { code = "FFQ", value = filter.frequency.ToString() });                 //set filter frequnecy
                    ins.Add(new Instruction { code = "FRS", value = filter.resonance.ToString() });                 //set resonance level
                    ins.Add(new Instruction { code = "VFL", value = (char)(voice + 0x30) + "0001" });               //enable filter on chosen voice
                    ins.Add(new Instruction { code = "FMO", value = "000" + filter.mode.ToString() });              //set filter mode
                }
                else
                    ins.Add(new Instruction { code = "VFL", value = (char)(voice + 0x30) + "0000" });

                ins.Add(new Instruction { code = "TRG", value = (char)(voice + 0x30) + "0001" });                   //finally, set the Gate bit of the voice con register

                foreach (var command in ins)
                {
                    SendPacket(command.code, command.value);
                    Thread.Sleep(3);//doesn't work
                    //spin wheels instead
                    //for (int i = 0; i < 9000000; i++) ;
                }
            }

            public void Play(int voice, string frequency)
            {
                var ins = new List<Instruction> {
                new Instruction {code = "TRG", value = (char)(voice + 0x30) + "0000"},                          //turn off the voice
                new Instruction {code = "VWF", value = (char)(voice + 0x30) + waveform.ToString("D4")},         //set desired waveform
                new Instruction {code = "VFQ", value = (char)(voice + 0x30) + frequency},                       //set voice frequency
                new Instruction {code = "ENV", value = (char)(voice + 0x30) + ADSR.ToString()},                 //set envelope
            };

                if (waveform == 2) ins.Add(
                    new Instruction { code = "VPW", value = (char)(voice + 0x30) + pulsewidth.ToString("D4") });    //set pulsewidth if square wave selected

                if (filter.EN)
                {
                    ins.Add(new Instruction { code = "FFQ", value = filter.frequency.ToString() });                 //set filter frequnecy
                    ins.Add(new Instruction { code = "FRS", value = filter.resonance.ToString() });                 //set resonance level
                    ins.Add(new Instruction { code = "VFL", value = (char)(voice + 0x30) + "0001" });               //enable filter on chosen voice
                    ins.Add(new Instruction { code = "FMO", value = "000" + filter.mode.ToString() });              //set filter mode
                }
                else
                    ins.Add(new Instruction { code = "VFL", value = (char)(voice + 0x30) + "0000" });

                ins.Add(new Instruction { code = "TRG", value = (char)(voice + 0x30) + "0001" });                   //finally, set the Gate bit of the voice con register

                foreach (var command in ins)
                {
                    SendPacket(command.code, command.value);
                    Thread.Sleep(3);//doesn't work
                    //for (int i = 0; i < 9000000; i++) ;
                }
            }

            public override string ToString()
            {
                return Name.ToString();
            }
        }

        public class Envelope
        {
            public int attack { get; set; }
            public int decay { get; set; }
            public int sustain { get; set; }
            public int release { get; set; }

            public Envelope()
            {
                attack = 8;
                decay = 8;
                sustain = 0;
                release = 8;
            }

            override public string ToString()
            {
                return attack.ToString("X") + decay.ToString("X") + sustain.ToString("X") + release.ToString("X");
            }
        }

        public class FilterSettings
        {
            public bool EN { get; set; }
            public int frequency { get; set; }
            public int resonance { get; set; }
            public int mode { get; set; }  //3=off, 2=hp, 1=bp, 0=lp

            public FilterSettings()
            {
                EN = false;
                frequency = 0;
                resonance = 0;
                mode = 3;
            }
        }

        public class Instruction
        {
            public string code { get; set; }
            public string value { get; set; }
        }

        public class Track
        {
            public string Name { get; set; }
            public Instrument instr { get; set; }
            public int channel { get; set; }
            public List<int> events { get; set; }
            public override string ToString()
            {
                return Name.ToString();
            }

            public Track()
            {
                Name = "Untitled";
                instr = new Instrument();
                channel = 0;
                events = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0 };
            }
        }

        public static class Notes
        {
            public static string[,] table = new string[,] {
           {"0206","040C","0818","102F","205E","40BC","8178", "F2F0"}, //B0
           {"01E9","03D2","07A4","0F47","1E8D","3D1A","7A34","F467"}, //As1
           {"01CE","039B","0736","0E6B","1CD6","39AC","7358","E6B0"}, //A2
           {"01B4","0367","06CE","0D9C","1B38","3670","6CDF","D9BD"},//Gs 3
            {"019C","0337","066D","0CD9","19B1","3362","66C3","CD85"},//G4
          {"0184","0308","0610","0C20","1840","307F","60FE","C1FC"}, //Fs 5
            {"016F","02DD","05B9","0B72","16E4","2DC7","5B8D","B719"},//f6
            {"015A","02B4","0567","0ACE","159B","2B35","5669","ACD2"},//e7
            {"0147","028D","0519","0A32","1464","28C8","5190","A31F"},//ds8
            {"0134","0268","04D0","09A0","133F","267E","4CFC","99F7"},//d9
            {"0123","0246","048B","0916","122B","2455","48AA","9153"},//cs10
            {"0113","0225","044A","0893","1126","224B","4496","892B"}//c11
        };
        }

        public class TrackPanel : StackPanel
        {
           // public Track childTrack;
        }
        #endregion

        #region list constants
        List<string> channelList = new List<string> { "Voice 0", "Voice 1", "Voice 2" };
        List<string> attackList = new List<string> { "2 ms", "8 ms", "16 ms", "24 ms", "38 ms", "56 ms", "68 ms", "80 ms", "100 ms", "250 ms", "500 ms", "800 ms", "1 s", "3 s", "5 s", "8 s" };
        List<string> decayList = new List<string> { "6 ms", "24 ms", "48 ms", "72 ms", "114 ms", "168 ms", "204 ms", "240 ms", "300 ms", "750 ms", "1.5 s", "2.4 s", "3 s", "9 s", "15 s", "24 s" };
        List<string> ampList = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15" };
        #endregion

 
    }
}
