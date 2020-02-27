using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using SID;
using UsbLib;
using System.IO.Ports;


namespace SidController
{
    public class Program
    {
        static UsbPort _usb;
        static MOS6581 sid;

        public static void Main()
        {
            sid = new MOS6581(Pins.GPIO_PIN_D10, Pins.GPIO_PIN_D9);
            _usb = new UsbPort("COM4", 250000, _ProcessPacket);
            //Timer usbstatusreport = new Timer(new TimerCallback(_usb.SendPacket), null, 1000, 700);

            sid.reset();
            sid.volume(15); // set volume to the maximum, 15.

            sid.setMode(0, SID_TRIANGLE); //set voice 0 to a ramp waveform
            sid.setADEnvelope(0,0,0); //Set voice 0's Attack and Decay envelope
            sid.setSREnvelope(0,15,0); //Set voice 0's Sustain and Release envelope

            sid.setFrequency(0, A[4]);

            sid.setVoice(0,true); //Set voice 0 to 'on'.
          //  sid.setVoice(2, true);
          //  sid.setVoice(3, true);

            while (true)
            {
                Thread.Sleep(900);
                _usb.SendPacket();
            }

        }

        static void _ProcessPacket(object sender, SerialDataReceivedEventArgs a)
        {
            //long startTime = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            byte[] inBuf = null;
            int available;
            do
            {
                available = _usb._serial.BytesToRead;
            } while (available < 14);                       //wait for all bytes before operating on data
            
            inBuf = new byte[14];
            _usb._serial.Read(inBuf, 0, inBuf.Length);            

            string Packet = new string(System.Text.Encoding.UTF8.GetChars(inBuf));
           // Debug.Print(Packet);
            int RemoteChecksum = Int16.Parse(Packet.Substring(11, 3));  //extract sender's checksum
            int LocalChecksum = 0;
            string data = Packet.Substring(3, 8);
            for (int i = 3; i < 11; i++)
            {
                LocalChecksum += Packet[i];
            }
            //compute our checksum
            LocalChecksum %= 1000;

            if (RemoteChecksum != LocalChecksum) return;    //stop packet processing if checksums don't match
            string packetType = Packet.Substring(0, 3);

            if (packetType == "$$$")                        //do SID Protocol
            {
                //Debug.Print(Packet);
                string command = data.Substring(0, 3);
                string value = data.Substring(3);

                switch (command)
                {
                    case "ENV":    //set voice envelope
                        {
                            int voice = GetHex(value[0]);
                            int attack = GetHex(value[1]);
                            int decay = GetHex(value[2]);
                            int sustain = GetHex(value[3]);
                            int release = GetHex(value[4]);
                            sid.setADEnvelope(voice, attack, decay);
                            sid.setSREnvelope(voice, sustain, release);
                            break;
                        }
                        
                    case "VFQ":    //voice frequency
                        {
                            int voice = GetHex(value[0]);
                            int freq = Convert.ToInt32(value.Substring(1), 16);
                            sid.setFrequency(voice, freq);
                            break;
                        }

                    case "VPW":    //voice puslewidth
                        {
                            int voice = GetHex(value[0]);
                            int pw = Convert.ToInt32(value.Substring(1), 16);
                            sid.setPulseWidth(voice, pw);
                            break;
                        }
                    case "FFQ":    //filter frequency
                        {
                            int freq = Convert.ToInt32(value, 16);
                            sid.filterFrequency(freq);
                            break; 
                        }
                    case "FRS":    //filter resonance
                        {
                            int res = Convert.ToInt32(value, 16);
                            break;
                        }
                    case "VFL":    //voice filter mode
                        {
                            int voice = GetHex(value[0]);
                            bool on = value[4] == '1' ? true : false;
                            sid.setFilterVoice(voice, on);
                            break;
                        }
                    case "TRG":    //voice enable: on/off
                        {
                            int voice = GetHex(value[0]);
                            bool on = value[4] == '1' ? true : false;
                            sid.setVoice(voice, on); 
                            break;
                        }
                    case "VWF":    //voice waveform
                        {
                            int voice = GetHex(value[0]);
                            int _waveform = GetHex(value[4]);
                            int waveform = 1 << (4 + _waveform);
                            sid.setMode(voice, waveform);
                            break;
                        }                        
                    case "AMP":    //set volume
                        {
                            int volume = GetHex(value[4]);
                            sid.volume(volume);
                            break;
                        }
                    case "FMO":    //filter mode (bp, hp, lp)
                        {
                            int mode = GetHex(value[4]);
                            sid.setFilterMode(1<<mode);
                            break;
                        }
                }
            }
            #region boring
            else if (packetType == "###")                        //do Mayes Protocol
            {
                _usb.D0.Write(data[0] == '1' ? true : false);
                _usb.D1.Write(data[1] == '1' ? true : false);
                _usb.D2.Write(data[2] == '1' ? true : false);
                _usb.D3.Write(data[3] == '1' ? true : false);
                _usb.D4.Write(data[4] == '1' ? true : false);
                _usb.D5.Write(data[5] == '1' ? true : false);
                _usb.D6.Write(data[6] == '1' ? true : false);
                _usb.D7.Write(data[7] == '1' ? true : false);
            }
            #endregion
           // long endTime = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            //Debug.Print((endTime - startTime).ToString());
        }

        static int GetHex(char hexChar)
        {
            return (int)hexChar < (int)'A' ?
                ((int)hexChar - (int)'0') :
                10 + ((int)hexChar - (int)'A');
        }

        #region Note Arrays
        static readonly int[] B = { 0x206, 0x40C, 0x818, 0x102F, 0x205E, 0x40BC, 0x8178 };
        static readonly int[] As = { 0x1E9, 0x3D2, 0x7A4, 0xF47, 0x1E8D, 0x3D1A, 0x7A34, 0xF467 };
        static readonly int[] A = { 0x1CE,0x39B,0x736,0xE6B,0x1CD6,0x39AC,0x7358,0xE6B0  };
        static readonly int[] Gs = { 0x1B4, 0x367, 0x6CE, 0xD9C, 0x1B38, 0x3670, 0x6CDF, 0xD9BD };
        static readonly int[] G = { 412, 823, 1645, 3289, 6577, 13154, 26307, 52613 };
        static readonly int[] Fs = { 388, 776, 1552, 3104, 6208, 12415, 24830, 49660 };
        static readonly int[] F = { 367, 733, 1465, 2930, 5860, 11719, 23437, 46873 };
        static readonly int[] E = { 346, 692, 1383, 2766, 5531, 11061, 22121, 44242 };
        static readonly int[] Ds = { 327, 653, 1305, 2610, 5220, 10440, 20880, 41759 };
        static readonly int[] D = { 308, 616, 1232, 2464, 4927, 9854, 19708, 39415 };
        static readonly int[] Cs = { 291, 582, 1163, 2326, 4651, 9301, 18602, 37203 };
        static readonly int[] C = { 275, 549, 1098, 2195, 4390, 8779, 17558, 35115 };

        #endregion
        #region named constants
        const byte SID_NOISE = 128;
        const byte SID_SQUARE = 64;
        const byte SID_RAMP = 32;
        const byte SID_TRIANGLE = 16;
        const byte SID_TEST = 8;
        const byte SID_RING = 20;
        const byte SID_SYNC = 66;
        const byte SID_OFF = 0;

        const byte SID_3OFF = 128;
        const byte SID_FILT_HP = 64;
        const byte SID_FILT_BP = 32;
        const byte SID_FILT_LP = 16;
        const byte SID_FILT_OFF = 0;

        const byte SID_FILT_VOICE1 = 1;
        const byte SID_FILT_VOICE2 = 2;
        const byte SID_FILT_VOICE3 = 4;
        const byte SID_FILT_EXT = 8;
        #endregion
        #region register definitions

        // VOICE ONE
        const byte SID_V1_FL = 0;
        const byte SID_V1_FH = 1;
        const byte SID_V1_PWL = 2;
        const byte SID_V1_PWH = 3;
        const byte SID_V1_CT = 4;
        const byte SID_V1_AD = 5;
        const byte SID_V1_SR = 6;

        // VOICE TWO
        const byte SID_V2_FL = 7;
        const byte SID_V2_FH = 8;
        const byte SID_V2_PWL = 9;
        const byte SID_V2_PWH = 10;
        const byte SID_V2_CT = 11;
        const byte SID_V2_AD = 12;
        const byte SID_V2_SR = 13;

        // VOICE THREE
        const byte SID_V3_FL = 14;
        const byte SID_V3_FH = 15;
        const byte SID_V3_PWL = 16;
        const byte SID_V3_PWH = 17;
        const byte SID_V3_CT = 18;
        const byte SID_V3_AD = 19;
        const byte SID_V3_SR = 20;

        //FILTER
        const byte SID_FL_FL = 21;
        const byte SID_FL_FH = 22;
        const byte SID_FL_RES_CT = 23;
        const byte SID_FL_MD_VL = 24;

        //READ ONLY REGISTERS
        const byte SID_POTX = 25;
        const byte SID_POTY = 26;
        const byte SID_OSC3_RND = 27;
        const byte SID_ENV3 = 28;
        #endregion
        
    }

}
