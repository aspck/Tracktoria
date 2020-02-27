using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware.Netduino;
using System.Threading;

namespace SID
{
    public class MOS6581
    {
        private
        SPI SPIBus;
        OutputPort latch;
        OutputPort CS;

        // create some internal registers for binary manipulation
        int voice1_register;
        int voice2_register;
        int voice3_register;
        int filter_register;
        int mode_register;        

        public MOS6581(Cpu.Pin SPI_latch, Cpu.Pin SID_CS)
        {
            SPIBus = new SPI(new SPI.Configuration(
                Pins.ONBOARD_LED, // SS-pin
                false,             // SS-pin active state
                0,                 // The setup time for the SS port
                0,                 // The hold time for the SS port
                true,              // The idle state of the clock
                true,              // The sampling clock edge (this must be "true" for the 74HC595)
                2000,              // The SPI clock rate in KHz
                SPI_Devices.SPI1   // The used SPI bus (refers to a MOSI MISO and SCLK pinset)
            ));
            //MOSI: 13
            //SCLK: 11
            latch = new OutputPort(SPI_latch, true);
            CS = new OutputPort(SID_CS, true);

            Initialize();
        }
        public void Initialize(){
            voice1_register = 0x20;
            voice2_register = 0x20;
            voice3_register = 0x20;
            filter_register = 0x00;
            mode_register = 0x00;
        }
        public void reset()
        {
            // iterate through all the registers and reset them
            for (int i = 0; i < 25; i++)
            {
                transfer(i, 0);
            }
        }
        public void transfer(int address, int value)
        {
            byte[] temp = new byte[] { (byte)address, (byte)value };
            latch.Write(false);
            SPIBus.Write(temp); 
            latch.Write(true);
            //Thread.Sleep(1);
            CS.Write(false);
            //Thread.Sleep(1);
            CS.Write(true);
        }
        public void setVoice(int voice, bool on)
        {
            if (voice == 0)
            {
                if (on)
                {
                    voice1_register |= 0x01;
                }
                else
                {
                    voice1_register &= 0xfe;
                }
                transfer(SID_V1_CT, voice1_register);
            }
            else if (voice == 1)
            {
                if (on)
                {
                    voice2_register |= 0x01;
                }
                else
                {
                    voice2_register &= 0xfe;
                }
                transfer(SID_V2_CT, voice2_register);
            }
            else if (voice == 2)
            {
                if (on)
                {
                    voice3_register |= 0x01;
                }
                else
                {
                    voice3_register &= 0xfe;
                }
                transfer(SID_V3_CT, voice3_register);
            }
        }
        public void setFilterVoice(int voice, bool on)
        { 
            if (voice == 0)
            {
                if (on)
                {
                    filter_register |= SID_FILT_VOICE1;
                }
                else
                {
                    int temp = SID_FILT_VOICE1;
                    filter_register &= (int)(~temp);
                }
            }
            else if (voice == 1)
            {
                if (on)
                {
                    filter_register |= SID_FILT_VOICE2;
                }
                else
                {
                    int temp = SID_FILT_VOICE2;
                    filter_register &= (int)(~temp);
                }
            }
            else if (voice == 2)
            {
                if (on)
                {
                    filter_register |= SID_FILT_VOICE3;
                }
                else
                {
                    int temp = SID_FILT_VOICE3;
                    filter_register &= (int)(~temp);
                }
            }
            transfer(SID_FL_RES_CT, filter_register);
        }
	    // fundamental frequency of waveform generator. 16bit number
        public void setFrequency(int voice, int frequency)
        {
            if (voice == 0)
            {
                voiceFrequency(SID_V1_FL, SID_V1_FH, frequency);
            }
            else if (voice == 1)
            {
                voiceFrequency(SID_V2_FL, SID_V2_FH, frequency);
            }
            else if (voice == 2)
            {
                voiceFrequency(SID_V3_FL, SID_V3_FH, frequency);
            }
        }
	    // duty cycle of square waves. 12bit number
        public void setPulseWidth(int voice, int frequency)
        {
            if (voice == 0)
            {
                voicePulseWidth(SID_V1_PWL, SID_V1_PWH, frequency);
            }
            else if (voice == 1)
            {
                voicePulseWidth(SID_V2_PWL, SID_V2_PWH, frequency);
            }
            else if (voice == 2)
            {
                voicePulseWidth(SID_V3_PWL, SID_V3_PWH, frequency);
            }
        }
        public void setMode(int voice, int mode)
        {
            if (voice == 0)
            {
                voice1_register &= 0x01;
                voice1_register |= mode;
                transfer(SID_V1_CT, voice1_register);
            }
            else if (voice == 1)
            {
                voice2_register &= 0x01;
                voice2_register |= mode;
                transfer(SID_V2_CT, voice2_register);
            }
            else if (voice == 2)
            {
                voice3_register &= 0x01;
                voice3_register |= mode;
                transfer(SID_V3_CT, voice3_register);
            }
            else
            {

            }
        }
        public void setADEnvelope(int voice, int attack, int decay)
        {
            if (voice == 0)
            {
                transfer(SID_V1_AD, (int)((decay & 0x0f) | (attack << 4)));
            }
            else if (voice == 1)
            {
                transfer(SID_V2_AD, (int)((decay & 0x0f) | (attack << 4)));
            }
            else if (voice == 2)
            {
                 transfer(SID_V3_AD, (int)((decay & 0x0f) | (attack << 4)));
            }
        }
        public void setSREnvelope(int voice, int sustain, int release)
        {
            if (voice == 0)
            {
                transfer(SID_V1_SR, (int)((release & 0x0f) | (sustain << 4)));
            }
            else if (voice == 1)
            {
                transfer(SID_V2_SR, (int)((release & 0x0f) | (sustain << 4)));
            }
            else if (voice == 2)
            {
                transfer(SID_V3_SR, (int)((release & 0x0f) | (sustain << 4)));
            }
        }
	    // filter volume and output
        public void volume(int level)
        {
            mode_register &= 0xf0;
            mode_register |= (int)(level & 0x0f);

            transfer(SID_FL_MD_VL, mode_register);
        }
        public void setFilterMode(int mode)
        {
            mode_register &= 0x0f;
            mode_register |= mode;

            transfer(SID_FL_MD_VL, mode_register);
        }
	    // set the filter frequency. 11bit numbers
        public void filterFrequency(int frequency)
        {
            int low = (int)(lowint(frequency) & 0x07);
            int high = (int)(frequency >> 3);
            transfer(SID_FL_FL, low);
            transfer(SID_FL_FH, high);
        }
        public void filterResonance(int resonance)
        {
            filter_register &= 0x0f;

            filter_register |= (int)(resonance << 4);

            transfer(SID_FL_RES_CT, filter_register);
        }

        private void voiceFrequency(int lowAddress, int highAddress, int frequency){
	        int low = lowint(frequency);
	        int high = highint(frequency);
	        transfer(lowAddress, low);
	        transfer(highAddress, high);

        }
        private void voicePulseWidth(int lowAddress, int highAddress, int frequency){
	        int low = lowint(frequency);
	        int high = (int)(highint(frequency) & (0x0f));
	        transfer(lowAddress, low);
	        transfer(highAddress, high);
        }
        int lowint(int word)
        {
            int temp = (int)word;
            return temp;
        }
        int highint(int word)
        {
            int temp = (int)(word >> 8);
            return temp;
        }

        
        #region named constants
        public

        const int SID_NOISE 		=128;
        const int SID_SQUARE		=64;
        const int SID_RAMP		=32;
        const int SID_TRIANGLE	=16;
        const int SID_TEST		=8;
        const int SID_RING		=20;
        const int SID_SYNC		=66;
        const int SID_OFF		=	0;

        const int SID_3OFF		=128;
        const int SID_FILT_HP	=	64;
        const int SID_FILT_BP	=	32;
        const int SID_FILT_LP	=	16;
        const int SID_FILT_OFF	=0;

        const int SID_FILT_VOICE1=	1;
        const int SID_FILT_VOICE2= 2;
        const int SID_FILT_VOICE3= 4;
        const int SID_FILT_EXT = 8;
        #endregion
        #region register definitions
        public
        // VOICE ONE
        const int SID_V1_FL =0;
        const int SID_V1_FH =1;
        const int SID_V1_PWL =2;
        const int SID_V1_PWH =3;
        const int SID_V1_CT =4;
        const int SID_V1_AD =5;
        const int SID_V1_SR =6;

        // VOICE TWO
        const int SID_V2_FL =7;
        const int SID_V2_FH =8;
        const int SID_V2_PWL =9;
        const int SID_V2_PWH =10;
        const int SID_V2_CT =11;
        const int SID_V2_AD =12;
        const int SID_V2_SR =13;

        // VOICE THREE
        const int SID_V3_FL =14;
        const int SID_V3_FH =15;
        const int SID_V3_PWL =16;
        const int SID_V3_PWH =17;
        const int SID_V3_CT =18;
        const int SID_V3_AD= 19;
        const int SID_V3_SR =20;

        //FILTER
        const int SID_FL_FL =21;
        const int SID_FL_FH =22;
        const int SID_FL_RES_CT =23;
        const int SID_FL_MD_VL =24;

        //READ ONLY REGISTERS
        const int SID_POTX =25;
        const int SID_POTY =26;
        const int SID_OSC3_RND =27;
        const int SID_ENV3 =28;
#endregion
    }
}
