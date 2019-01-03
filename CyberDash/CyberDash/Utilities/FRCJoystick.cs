using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberDash.Utilities
{
    public class FRCJoystick
    {
        public int Index { get; set; }
        public Joystick dJoystick { get; protected set; } = null;
        public Controller xJoystick { get; protected set; } = null;

        public FRCJoystick(Joystick joystick)
        {
            dJoystick = joystick;
            Connect();
        }

        public FRCJoystick(Controller joystick)
        {
            xJoystick = joystick;
        }

        public void Connect()
        {
            if (dJoystick != null)
            {
                try
                {
                    dJoystick.Acquire();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public void Disconnect()
        {
            if (dJoystick != null)
            {
                try
                {
                    dJoystick.Unacquire();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public override string ToString()
        {
            string prefix = "";
            if (dJoystick != null)
            {
                try
                {
                    JoystickState joystickState = new JoystickState();
                    dJoystick.GetCurrentState(ref joystickState);

                    foreach (bool b in joystickState.Buttons)
                    {
                        if (b)
                        {
                            prefix = "(*)->";
                            break;
                        }
                        else
                        {
                            prefix = "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                return prefix + dJoystick.Information.InstanceName;
            }
            else if (xJoystick != null)
            {
                return prefix + xJoystick.ToString();
            }
            else
            {
                return "";
            }
        }
    }
}
