using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using System.IO.Ports;

namespace UsbLib
{
    public class UsbPort
    {
        private int PacketNum = 0;
        public SerialPort _serial;

        public UsbPort(string comport, int baudrate)
        {
            _serial = new SerialPort(comport, baudrate);
            _serial.DataReceived += new SerialDataReceivedEventHandler(ProcessPacket);
            _serial.Open();
        }

        public UsbPort(string comport, int baudrate, SerialDataReceivedEventHandler packetreceive)
        {
            _serial = new SerialPort(comport, baudrate);
            _serial.DataReceived += packetreceive;
            _serial.Open();
        }

        private void ProcessPacket(object sender, SerialDataReceivedEventArgs a)
        {
            byte[] inBuf = null;

            int available;
            do {
                available = _serial.BytesToRead;
            } while (available < 14);                       //wait for all bytes before operating on data

            inBuf = new byte[_serial.BytesToRead];
            _serial.Read(inBuf, 0, inBuf.Length);
            string Packet = new string(System.Text.Encoding.UTF8.GetChars(inBuf));      

            int RemoteChecksum = Int16.Parse(Packet.Substring(11, 3));  //extract sender's checksum
            int LocalChecksum = 0;
            string data = Packet.Substring(3, 8);
            foreach (char digit in data) LocalChecksum += digit;        //compute our checksum
            LocalChecksum %= 1000;

            if (RemoteChecksum != LocalChecksum) return;    //stop packet processing if checksums don't match

            string packetType = Packet.Substring(0, 3);

            //if (packetType == "$$$")                        //do SID Protocol
            //{
            //    string command = data.Substring(0, 3);
            //    string value = data.Substring(3);

            //    switch (command) {
            //        case "ENV" :    //set voice envelope
            //            break;
            //        case "VFQ" :    //voice frequency
            //            break;
            //        case "VPW" :    //voice puslewidth
            //            break;
            //        case "FFQ" :    //filter frequency
            //            break;
            //        case "FRS" :    //filter resonance
            //            break;
            //        case "SVF" :    //voice filter mode
            //            break;
            //        case "VEN" :    //voice enable: on/off
            //            break;
            //        case "VWF" :    //voice waveform
            //            break;
            //        case "AMP" :    //set volume
            //            break;
            //        case "FMO" :    //filter mode (bp, hp, lp)
            //            break;              
            //    }
            //}
            #region boring
            if (packetType == "###")                        //do Mayes Protocol
            {                                                        
                D0.Write(data[0] == '1' ? true : false);
                D1.Write(data[1] == '1' ? true : false);
                D2.Write(data[2] == '1' ? true : false);
                D3.Write(data[3] == '1' ? true : false);
                D4.Write(data[4] == '1' ? true : false);
                D5.Write(data[5] == '1' ? true : false);
                D6.Write(data[6] == '1' ? true : false);
                D7.Write(data[7] == '1' ? true : false);
            }        
            #endregion      
        }

        public void SendPacket()
        {
            int checksum = 0;
            if (PacketNum++ > 999) PacketNum = 0;

            //get all the data in string form
            String num = PacketNum.ToString("D3");
            String A0val = A0.ReadRaw().ToString("D4");
            String A1val = A1.ReadRaw().ToString("D4");
            String A2val = A2.ReadRaw().ToString("D4");
            String A3val = A3.ReadRaw().ToString("D4");
            String A4val = A4.ReadRaw().ToString("D4");
            String A5val = A5.ReadRaw().ToString("D4");
            char D0val = D0.Read() ? '1' : '0';
            char D1val = D1.Read() ? '1' : '0';
            char D2val = D2.Read() ? '1' : '0';
            char D3val = D3.Read() ? '1' : '0';
            char D4val = D4.Read() ? '1' : '0';
            char D5val = D5.Read() ? '1' : '0';
            char D6val = D6.Read() ? '1' : '0';
            char D7val = D7.Read() ? '1' : '0';
            char Btnval = Btn.Read() ? '1' : '0';

            //concat the data
            String packet = num + A0val + A1val + A2val + A3val + A4val + A5val + Btnval + D0val + D1val + D2val + D3val + D4val + D5val + D6val + D7val;

            //compute checksum
            foreach (char digit in packet) checksum += (digit);
            checksum %= 1000;

            //append checksum and protocol frame stuff
            packet = "###" + packet + checksum + "\r\n";

            //convert to byte array for sending
            byte[] bytepacket = System.Text.Encoding.UTF8.GetBytes(packet);

            //send the packet
            _serial.Write(bytepacket, 0, bytepacket.Length);
        }

        #region pins
        public
         InputPort Btn = new InputPort(Pins.ONBOARD_BTN, false, ResistorModes.Disabled);
        //static InputPort D0 = new InputPort(Cpu.Pin.GPIO_Pin0, false, ResistorModes.Disabled);
        //static InputPort D1 = new InputPort(Cpu.Pin.GPIO_Pin1, false, ResistorModes.Disabled);
        //static InputPort D2 = new InputPort(Cpu.Pin.GPIO_Pin2, false, ResistorModes.Disabled);
        //static InputPort D3 = new InputPort(Cpu.Pin.GPIO_Pin3, false, ResistorModes.Disabled);
        //static InputPort D4 = new InputPort(Pins.GPIO_PIN_D4, false, ResistorModes.PullUp);
        //static InputPort D5 = new InputPort(Pins.GPIO_PIN_D5, false, ResistorModes.PullUp);
        //static InputPort D6 = new InputPort(Pins.GPIO_PIN_D6, false, ResistorModes.PullUp);
        //static InputPort D7 = new InputPort(Pins.GPIO_PIN_D7, false, ResistorModes.PullUp);

        public OutputPort D0 = new OutputPort(Pins.GPIO_PIN_D0, false);
         public OutputPort D1 = new OutputPort(Pins.GPIO_PIN_D1, false);
         public OutputPort D2 = new OutputPort(Pins.GPIO_PIN_D2, false);
         public OutputPort D3 = new OutputPort(Pins.GPIO_PIN_D3, false);
         public OutputPort D4 = new OutputPort(Pins.GPIO_PIN_D4, false);
         public OutputPort D5 = new OutputPort(Pins.GPIO_PIN_D5, false);
         public OutputPort D6 = new OutputPort(Pins.GPIO_PIN_D6, false);
         public OutputPort D7 = new OutputPort(Pins.GPIO_PIN_D7, false);

         AnalogInput A0 = new AnalogInput(Cpu.AnalogChannel.ANALOG_0);
         AnalogInput A1 = new AnalogInput(Cpu.AnalogChannel.ANALOG_1);
         AnalogInput A2 = new AnalogInput(Cpu.AnalogChannel.ANALOG_2);
         AnalogInput A3 = new AnalogInput(Cpu.AnalogChannel.ANALOG_3);
         AnalogInput A4 = new AnalogInput(Cpu.AnalogChannel.ANALOG_4);
         AnalogInput A5 = new AnalogInput(Cpu.AnalogChannel.ANALOG_5);
        #endregion
    }
}
