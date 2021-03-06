﻿using System;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;

namespace ABElectronics_Win10IOT_Libraries
{

    /// <summary>
    /// Class for accessing the ADCDAC Pi from AB Electronics UK.
    /// </summary>
    public class ADCDACPi
    {

        private double ADCReferenceVoltage = 3.3;
        private const string SPI_CONTROLLER_NAME = "SPI0";
        private const Int32 ADC_CHIP_SELECT_LINE = 0; // ADC on SPI channel select CE0
        private const Int32 DAC_CHIP_SELECT_LINE = 1; // ADC on SPI channel select CE1

        private SpiDevice adc;
        private SpiDevice dac;

        /// <summary>
        /// Event triggers when a connection is established.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return isConnected;
            }

            private set
            {
                isConnected = value;
            }
        }
        private bool isConnected;

        /// <summary>
        /// Create an instance of the ADCDACPi class.
        /// </summary>
        public ADCDACPi()
        {

        }

        /// <summary>
        /// Open a connection to the ADCDAC Pi
        /// </summary>
        public async void Connect()
        {
            try
            {
                var adcsettings = new SpiConnectionSettings(ADC_CHIP_SELECT_LINE);  // Create SPI initialization settings for the ADC
                adcsettings.ClockFrequency = 10000000;                               // SPI clock frequency of 10MHz
                adcsettings.Mode = SpiMode.Mode0;

                string spiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);   // Find the selector string for the SPI bus controller
                var devicesInfo = await DeviceInformation.FindAllAsync(spiAqs);     // Find the SPI bus controller device with our selector string

                adc = await SpiDevice.FromIdAsync(devicesInfo[0].Id, adcsettings);  // Create an ADC connection with our bus controller and SPI settings

                var dacsettings = new SpiConnectionSettings(DAC_CHIP_SELECT_LINE);  // Create SPI initialization settings for the DAC
                dacsettings.ClockFrequency = 2000000;                               // SPI clock frequency of 20MHz
                dacsettings.Mode = SpiMode.Mode0;

                dac = await SpiDevice.FromIdAsync(devicesInfo[0].Id, dacsettings);  // Create a DAC connection with our bus controller and SPI settings



                IsConnected = true; // connection established, set IsConnected to true.

                EventHandler handler = Connected;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }

            }
            /* If initialization fails, display the exception and stop running */
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }

        /// <summary>
        /// Event occurs when connection is made
        /// </summary>
        public event EventHandler Connected;


        /// <summary>
        /// Read the voltage from the selected channel on the ADC
        /// </summary>
        /// <param name="channel">1 or 2</param>
        /// <returns>voltage</returns>
        public double ReadADCVoltage(byte channel)
        {
            if ((channel < 1) || (channel > 2))
            {
                throw new ArgumentOutOfRangeException();
            }

            int raw = ReadADCRaw(channel);
            double voltage = (ADCReferenceVoltage / 4096) * raw; // convert the raw value into a voltage based on the reference voltage.
            return voltage;
        }


        /// <summary>
        /// Read the raw value from the selected channel on the ADC
        /// </summary>
        /// <param name="channel">1 or 2</param>
        /// <returns>Integer</returns>
        public int ReadADCRaw(byte channel)
        {
            if ((channel < 1) || (channel > 2))
            {
                throw new ArgumentOutOfRangeException();
            }

            byte[] writeArray = new byte[] { 0x01, (byte)((1 + channel) << 6), 0x00 }; // create the write bytes based on the input channel
          

            byte[] readBuffer = new byte[3]; // this holds the output data

            adc.TransferFullDuplex(writeArray, readBuffer); // transfer the adc data

            short ret = (short)(((readBuffer[1] & 0x0F) << 8) + (readBuffer[2])); // combine the two bytes into a single 16bit integer
            return ret;
        }

        /// <summary>
        /// set the reference voltage for the analogue to digital converter. The ADC uses the raspberry pi 3.3V power as a voltage reference so using this method to set the reference to match the exact output voltage from the 3.3V regulator will increase the accuracy of the ADC readings.
        /// </summary>
        /// <param name="voltage">double</param>
        public void SetADCrefVoltage(double voltage)
        {
            if ((voltage >= 0.0) && (voltage <= 7.0))
            {
                ADCReferenceVoltage = voltage;
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Set the voltage for the selected channel on the DAC
        /// </summary>
        /// <param name="channel">1 or 2</param>
        /// <param name="voltage">Voltage can be between 0 and 2.047 volts</param>
        public void SetDACVoltage(byte channel, double voltage)
        {
            // Check for valid channel and voltage variables
            if ((channel < 1) || (channel > 2))
            {
                throw new ArgumentOutOfRangeException();
            }

            if ((voltage >= 0.0) && (voltage < 2.048))
            {
                short rawval = (short)(Convert.ToInt16((voltage / 2.048) * 4096)); // convert the voltage into a raw value
                SetDACRaw(channel, rawval);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Set the raw value from the selected channel on the DAC
        /// </summary>
        /// <param name="channel">1 or 2</param>
        /// <param name="voltage">Value between 0 and 4095</param>
        public void SetDACRaw(byte channel, short value)
        {
            // split the raw value into two bytes and send it to the DAC.
            byte lowByte = (byte)(value & 0xff);
            byte highByte = (byte)(((value >> 8) & 0xff) | (channel - 1) << 7 | 0x1 << 5 | 1 << 4);
            byte[] writeBuffer = new byte[] { highByte, lowByte };
            dac.Write(writeBuffer);
        }

    }
}
