using OpenMacroBoard.SDK;
using StreamDeckSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vJoyInterfaceWrap;

namespace ElgatoStreamDeckSupport
{
    /// <summary>
    /// This is a support package to enable StreamDeck support. This library will only operate properly with 64bit architecture
    /// </summary>
    public class CKStreamDeck
    {

        private vJoy VirtualJoystick = new vJoy();
        private IStreamDeckBoard Deck;
        private object lockObj = new object();

        // The ID must be between 1 and 16. 
        private readonly uint JoystickID;

        private List<Bitmap> ButtonImages = new List<Bitmap>();

        public CKStreamDeck(int vJoyId)
        {
            JoystickID = (uint)vJoyId;

            InitJoystick();
        }

        // Handles stream deck key presses.
        private void StreamDeckKeyPressed(object Sender, KeyEventArgs EventArgs)
        {
            lock (lockObj)
            {
                VirtualJoystick.SetBtn(EventArgs.IsDown, 1, (uint)EventArgs.Key + 1);
            }
        }

        public void ForceReinitJoystick()
        {
            try
            {
                lock (lockObj)
                {
                    if (VirtualJoystick != null)
                        VirtualJoystick.RelinquishVJD(JoystickID);

                    VirtualJoystick = new vJoy();
                }
            } catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
            }

            try
            {
                InitJoystick();
            } catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
            }
        }

        private void InitJoystick()
        {
            lock (lockObj)
            {
                // Verify the vJoy driver is enabled.
                if (!VirtualJoystick.vJoyEnabled())
                    throw new VJoyNotEnabledException("vJoy is not enabled! Please install and enable vJoy!");

                // Get the state of the requested device.
                VjdStat Status = VirtualJoystick.GetVJDStatus(JoystickID);

                switch (Status)
                {
                    case VjdStat.VJD_STAT_OWN:
                    case VjdStat.VJD_STAT_FREE:
                        break;

                    case VjdStat.VJD_STAT_BUSY:
                    case VjdStat.VJD_STAT_MISS:
                    default:
                        throw new VJoyAccessException("Cannot access vJoy! Code: " + Status.ToString());

                };

                // Acquire the target joystick.
                if (!VirtualJoystick.AcquireVJD(JoystickID))
                    throw new VJoyAcquisitionException("Could not acquire vJoy with ID: " + JoystickID.ToString());

                // Open the Stream Deck device.
                try
                {
                    Deck = StreamDeck.OpenDevice();
                }
                catch (Exception ex)
                {
                    //Catch the internal StreamDeck error and handle it our way.
                    Deck = null;
#if DEBUG
                    Console.WriteLine(ex.ToString());
#endif
                }

                if (Deck == null || !Deck.IsConnected)
                    throw new StreamDeckAccessException("Stream Deck could not be opened!");

                // Set the brightness of the keys.
                Deck.SetBrightness(100);

                // Register the key pressed event handler.
                Deck.KeyStateChanged += StreamDeckKeyPressed;

            }

            ReloadButtonImages();
        }

        public void ReloadButtonImages()
        {
            lock (lockObj)
            {
                ButtonImages.Clear();
                string imgPath = Directory.GetCurrentDirectory() + @"\img\";
                DirectoryInfo d = new DirectoryInfo(imgPath);
                foreach (FileInfo f in d.GetFiles("*.png"))
                {
                    ButtonImages.Add(new Bitmap(f.FullName));
                }

                for (int i = 0; i < Math.Min(Deck.Keys.Count, ButtonImages.Count); i++)
                {
                    Deck.SetKeyBitmap(i, KeyBitmap.Create.FromBitmap(ButtonImages[i]));
                }
            }
        }

        public bool IsConnected()
        {
            if (Deck != null)
                return Deck.IsConnected;
            else
                return false;
        }
    }
}
