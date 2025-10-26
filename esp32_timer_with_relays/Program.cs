using System;
using System.Device.Adc;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace esp32_timer_with_relays
{
    /*
      GPIO and ADC configuration for ESP32
       1. Gpios as output:
        - Green ready led - GPIO 25
        - Red job led - GPIO 26
        - Relay control pin - GPIO 16
       2 . ADC channels for buttons:
        - Channel 4
        - Channel 5
       3. Buttons and ADC Values:
        button program 1 - (ch 4, max 2000, min 1900)
        button program 2 - (ch 4, max 2700, min 2600)
        button program 3 - (ch 4, max 3350, min 3250)
        button program 4 - (ch 5, max 2000, min 1900)
        button reset- (ch 5, max 2700, min 2600)

    Led Info logic:
    Heating 1, 2, 4, 6 or 8 hours;
    Green led (GL) - user selected program type:
    - GL is turned on - device are waiting for user selecting
    - GL is blinked - user selected program

    Red led (RL) - internal relay status
    - RL is blinked - relay is turned on
    - RL is turned off - relay is turned off

    -------- For production use: --------
    button program 1 - 8 hours (ch 4, max 2000, min 1900)
    button program 2 - 6 hours (ch 4, max 2700, min 2600)
    button program 3 - 4 hours (ch 4, max 3350, min 3250)
    button program 4 - 2 hours (ch 5, max 2000, min 1900)

    -------- For develop use: --------
    button program 1 - 8 minutes hours (ch 4, max 2000, min 1900)
    button program 2 - 6 minutes (ch 4, max 2700, min 2600)
    button program 3 - 4 minutes (ch 4, max 3350, min 3250)
    button program 4 - 2 minutes (ch 5, max 2000, min 1900)
     */


    public class Program
    {
        public static void Main()
        {
            GpioController gpioController = new GpioController();
            AdcController adcController = new AdcController();

            AnalogValueController t = new AnalogValueController(new[] { 4, 5 }, adcController);
            // HeatService heatService = new HeatService(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(5));
            HeatService heatService = new HeatService(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(5));

            CustomOutputGpio heatingLed = new CustomOutputGpio(gpioController, 26);
            CustomOutputGpio jobyLed = new CustomOutputGpio(gpioController, 25);
            CustomOutputGpio relayPin = new CustomOutputGpio(gpioController, 16);
            LedInformationService ledInformationService = new LedInformationService(jobyLed, heatingLed);
            

            heatService.HeatingActionEventHandler += (sender, args) =>
            {
                HeatingStatus status = ((HeatingActionEventArgs)args).Status;
                if (status == HeatingStatus.Heating)
                {
                    relayPin.WritePin(1);
                    ledInformationService.TurnOnHeating();
                }
                else if (status == HeatingStatus.Cooling)
                {
                    relayPin.WritePin(0);
                    ledInformationService.TurnOffHeating();
                }
                else
                {
                    relayPin.WritePin(0);
                    ledInformationService.TurnOnDefault();
                }
            };
            t.OnPressButonEventHandler += (sender, args) =>
            {
                ButtonType type = ((PressButtonEventArgs)args).BtnType;
                TimeSpan timeSpan;
                switch (type)
                {
                    case ButtonType.Hours8:
                        timeSpan = TimeSpan.FromSeconds(480);
                        break;
                    case ButtonType.Hours6:
                        timeSpan = TimeSpan.FromSeconds(360);
                        break;
                    case ButtonType.Hours4:
                        timeSpan = TimeSpan.FromSeconds(240);
                        break;
                    case ButtonType.Hours2:
                        timeSpan = TimeSpan.FromSeconds(120);
                        break;
                    default:
                        timeSpan = TimeSpan.Zero;
                        break;
                }

                heatService.ExecuteHeating(timeSpan);
            };

            t.SetTimer();
            jobyLed.WritePin(PinValue.Low);
            relayPin.WritePin(PinValue.Low);
            Thread.Sleep(Timeout.Infinite);
        }
    }

    class LedInformationService
    {
        private readonly JobLedController _jobLedController;
        private readonly HeatingLedController _heatingLedController;

        public LedInformationService(CustomOutputGpio jobyLed, CustomOutputGpio heatingLed)
        {
            _jobLedController = new JobLedController(jobyLed);
            _heatingLedController = new HeatingLedController(heatingLed);
        }

        public void TurnOnDefault()
        {
            _jobLedController.Waiting();
            _heatingLedController.TurnOff();
        }

        public void TurnOnHeating()
        {
            _jobLedController.Running();
            _heatingLedController.TurnOn();
        }

        public void TurnOffHeating()
        {
            _heatingLedController.TurnOff();
        }
    }

    public abstract class LedController
    {
        protected readonly Timer LedTimer;
        protected readonly CustomOutputGpio Gpio;

        protected LedController(CustomOutputGpio gpio)
        {
            Gpio = gpio;
            LedTimer = new Timer(TimerCallback, null, -1, -1);
        }

        protected abstract void TimerCallback(object state);
    }

    public class HeatingLedController : LedController
    {
        public HeatingLedController(CustomOutputGpio gpio) : base(gpio)
        {
            Gpio.WritePin(PinValue.High);
        }

        public void TurnOn()
        {
            LedTimer.Change(0, 500);
            Gpio.WritePin(PinValue.High);
        }

        public void TurnOff()
        {
            
            LedTimer.Change(-1, -1);
            Gpio.WritePin(PinValue.High);
        }

        protected override void TimerCallback(object state)
        {
            Gpio.WritePin(PinValue.Low);
            Thread.Sleep(125);
            Gpio.WritePin(PinValue.High);
        }
    }

    public class JobLedController : LedController
    {
        public JobLedController(CustomOutputGpio gpio) : base(gpio)
        {
            Gpio.WritePin(PinValue.Low);
        }

        public void Waiting()
        {
            LedTimer.Change(-1, -1);
            Gpio.WritePin(PinValue.Low);
        }

        public void Running()
        {
            LedTimer.Change(0, 500);
        }

        protected override void TimerCallback(object state)
        {
            Gpio.WritePin(PinValue.Low);
            Thread.Sleep(125);
            Gpio.WritePin(PinValue.High);
        }
    }

    public class CustomOutputGpio
    {
        private readonly GpioPin _gpio;


        public CustomOutputGpio(GpioController gpioController, int pin)
        {
            _gpio = gpioController.OpenPin(pin, PinMode.Output);
            WritePin(PinValue.High);
        }

        public void WritePin(PinValue pinValue)
        {
            _gpio.Write(pinValue);
        }
    }

    public class HeatService
    {
        private readonly Timer _heatingTimer;
        private readonly Timer _coolingTimer;
        private readonly Timer _jobTimer;
        private readonly TimeSpan _heatingTs;
        private readonly TimeSpan _coolingTs;

        public EventHandler HeatingActionEventHandler;

        public HeatService(TimeSpan heatingTs, TimeSpan coolingTs)
        {
            _coolingTs = coolingTs;
            _heatingTs = heatingTs;
            _heatingTimer = new Timer(_OnHeating, null, -1, -1);
            _coolingTimer = new Timer(_OnCooling, null, -1, -1);
            _jobTimer = new Timer(_OnStop, null, -1, -1);
        }

        public void ExecuteHeating(TimeSpan delay)
        {
            if (delay == TimeSpan.Zero)
            {
                _reset();
            }
            else
            {
                _jobTimer.Change(delay, TimeSpan.FromMilliseconds(-1));
                _heatingTimer.Change(_heatingTs, TimeSpan.FromMilliseconds(-1));
                HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Heating));
            }
        }

        private void _OnHeating(object state)
        {
            _heatingTimer.Change(-1, -1);
            _coolingTimer.Change(_coolingTs, TimeSpan.FromMilliseconds(-1));
            HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Cooling));
        }

        private void _OnCooling(object state)
        {
            _coolingTimer.Change(-1, -1);
            _heatingTimer.Change(_heatingTs, TimeSpan.FromMilliseconds(-1));
            HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Heating));
        }

        private void _OnStop(object state)
        {
            _reset();
        }


        private void _reset()
        {
            
            _coolingTimer.Change(-1, -1);
            _heatingTimer.Change(-1, -1);
            _jobTimer.Change(-1, -1);
            HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Stop));
        }

        public void Dispose()
        {
            _coolingTimer.Dispose();
            _heatingTimer.Dispose();
            _jobTimer.Dispose();
        }
    }

    public class HeatingActionEventArgs : EventArgs
    {
        public HeatingStatus Status { get; }

        public HeatingActionEventArgs(HeatingStatus status)
        {
            Status = status;
        }
    }


    public class PressButtonEventArgs : EventArgs
    {
        public readonly ButtonType BtnType;

        public PressButtonEventArgs(ButtonType type)
        {
            BtnType = type;
        }
    }

    public class AnalogValueController
    {
        private readonly AnalogButton[][] _buttonsSet;
        private readonly AnalogReader[] _adcSets;
        private Timer _timer;
        public EventHandler OnPressButonEventHandler;

        public AnalogValueController(int[] channels, AdcController controller)
        {
            _adcSets = new AnalogReader[channels!.Length];
            _buttonsSet = new AnalogButton[channels.Length][];
            for (int i = 0; i < channels.Length; i++)
            {
                AnalogReader set = new AnalogReader(controller, channels[i]);
                _adcSets[i] = set;
                switch (i)
                {
                    case 0:
                        _buttonsSet[i] = InitFirstButtonSet();
                        break;
                    case 1:
                        _buttonsSet[i] = InitSecondButtonSet();
                        break;
                }
            }
        }

        public void SetTimer(int dueTime = 0, int period = 200)
        {
            _timer = new Timer(ScanButtons, null, dueTime, period);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void ScanButtons(object state)
        {
            AnalogButton btn = GetPressedButton();
            if (btn != null && OnPressButonEventHandler != null)
            {
                OnPressButonEventHandler.Invoke(this, new PressButtonEventArgs(btn.Type));
            }
        }

        private AnalogButton GetPressedButton()
        {
            for (int i = 0; i < _adcSets.Length; i++)
            {
                int value = _adcSets[i].ReadValue();
                for (int j = 0; j < _buttonsSet[i].Length; j++)
                {
                    AnalogButton btn = _buttonsSet[i][j];
                    if (btn.IsThisBtn(value))
                    {
                        Console.WriteLine("The button " + btn.Type.ToString() + " was pressed");
                        return btn;
                    }
                }
            }

            return null;
        }

        private AnalogButton[] InitFirstButtonSet()
        {
            AnalogButton hours8Btn = new AnalogButton(ButtonType.Hours8, 1800, 2100);
            AnalogButton hours6Btn = new AnalogButton(ButtonType.Hours6, 2500, 2800);
            AnalogButton hours4Btn = new AnalogButton(ButtonType.Hours4, 3150, 3450);
            return new[] { hours8Btn, hours6Btn, hours4Btn, };
        }

        private AnalogButton[] InitSecondButtonSet()
        {
            AnalogButton hours2Btn = new AnalogButton(ButtonType.Hours2, 1800, 2100);
            AnalogButton resetBtn = new AnalogButton(ButtonType.Reset, 2500, 2800);
            return new[] { hours2Btn, resetBtn };
        }
    }

    public class AnalogReader
    {
        private readonly AdcChannel _cnl;

        public AnalogReader(AdcController adc, int chlPin)
        {
            _cnl = adc.OpenChannel(chlPin);
        }

        public int ReadValue()
        {
            int v = _cnl.ReadValue();
            if (v > 100) Console.WriteLine(v.ToString());
            return v;
        }
    }

    public class AnalogButton
    {
        private readonly int _min, _max;
        public readonly ButtonType Type;

        public AnalogButton(ButtonType type, int min, int max)
        {
            this.Type = type;
            _max = max;
            _min = min;
        }

        public bool IsThisBtn(int value) => value > _min && value < _max;
    }

    public enum ButtonType
    {
        Hours8,
        Hours6,
        Hours4,
        Hours2,
        Reset,
    }

    public enum JobStatus
    {
        Waiting,
        Running,
    }

    public enum HeatingStatus
    {
        Heating,
        Cooling,
        Stop,
    }
}