using System;
using System.Device.Adc;
using System.Device.Gpio;
using System.Threading;
using nanoFramework.Hardware.Esp32;

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

    nanoff --listports
    nanoff --platform ESP32 --serialport [COM_PORT] --devicedetails
    nanoff --update --target ESP32_REV3 --serialport [COM_PORT] --masserase true --reset
     */


    public class Program
    {
        public static void Main()
        {
            GpioController gpioController = new GpioController();
            AdcController adcController = new AdcController();

            CustomOutputGpio heatingLed = new CustomOutputGpio(gpioController, 26);
            CustomOutputGpio jobyLed = new CustomOutputGpio(gpioController, 25);
            CustomOutputGpio relayPin = new CustomOutputGpio(gpioController, 16);


            TimingService timingService = new TimingService(ITiming.BuildOn(DevelopStage.Develop));
            HeatService heatService = new HeatService(timingService.HeatingUs, timingService.CoolingUs);
            LedInformationService ledInformationService = new LedInformationService(jobyLed, heatingLed);

            heatService.HeatingActionEventHandler += (_, args) =>
            {
                HeatingStatus status = ((HeatingActionEventArgs)args).Status;
                Console.WriteLine("Status: " + status.ToString());
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
            AdcService adcService = new AdcService(new[] { 4, 5 }, adcController);
            adcService.OnPressButonEventHandler += (_, args) =>
            {
                ButtonType type = ((AnalogButtonEventArgs)args).BtnType;
                ulong delay;
                switch (type)
                {
                    case ButtonType.Program1:
                        delay = timingService.Program1Us;
                        break;
                    case ButtonType.Program2:
                        delay = timingService.Program2Us;
                        break;
                    case ButtonType.Program3:
                        delay = timingService.Program3Us;
                        break;
                    case ButtonType.Program4:
                        delay = timingService.Program4Us;
                        break;
                    case ButtonType.Reset:
                        delay = 0;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                heatService.ExecuteHeating(delay);
            };
            adcService.StartScan();
            relayPin.WritePin(PinValue.Low);
            Thread.Sleep(Timeout.Infinite);
        }
    }

    public class TimingService
    {
        private ITiming _timing;

        public TimingService(ITiming timing)
        {
            _timing = timing;
        }

        public ulong HeatingUs => _convertTsToUs(_timing.HeatingTs);
        public ulong CoolingUs => _convertTsToUs(_timing.CoolingTs);
        public ulong Program1Us => _convertTsToUs(_timing.Program1Ts);
        public ulong Program2Us => _convertTsToUs(_timing.Program2Ts);
        public ulong Program3Us => _convertTsToUs(_timing.Program3Ts);
        public ulong Program4Us => _convertTsToUs(_timing.Program4Ts);

        ulong _convertTsToUs(TimeSpan ts) => (ulong)ts.Ticks / 10;
    }

    public interface ITiming
    {
        static ITiming BuildOn(DevelopStage stage)
        {
            switch (stage)
            {
                case DevelopStage.Develop:
                    return new DevTiming();
                case DevelopStage.Production:
                    return new ProdTiming();
                default:
                    throw new Exception();
            }
        }

        public TimeSpan HeatingTs { get; }
        public TimeSpan CoolingTs { get; }
        public TimeSpan Program1Ts { get; }
        public TimeSpan Program2Ts { get; }
        public TimeSpan Program3Ts { get; }
        public TimeSpan Program4Ts { get; }
    }

    public class DevTiming : ITiming
    {
        public TimeSpan HeatingTs => TimeSpan.FromSeconds(4);
        public TimeSpan CoolingTs => TimeSpan.FromSeconds(1);
        public TimeSpan Program1Ts => TimeSpan.FromSeconds(30);
        public TimeSpan Program2Ts => TimeSpan.FromSeconds(60);
        public TimeSpan Program3Ts => TimeSpan.FromSeconds(90);
        public TimeSpan Program4Ts => TimeSpan.FromSeconds(120);
    }

    public class ProdTiming : ITiming
    {
        public TimeSpan HeatingTs => TimeSpan.FromMinutes(4);
        public TimeSpan CoolingTs => TimeSpan.FromMinutes(1);
        public TimeSpan Program1Ts => TimeSpan.FromHours(8);
        public TimeSpan Program2Ts => TimeSpan.FromHours(6);
        public TimeSpan Program3Ts => TimeSpan.FromHours(4);
        public TimeSpan Program4Ts => TimeSpan.FromHours(2);
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
        protected readonly HighResTimer LedTimer;
        protected readonly CustomOutputGpio Gpio;

        protected LedController(CustomOutputGpio gpio)
        {
            Gpio = gpio;
            LedTimer = new HighResTimer();
            LedTimer.OnHighResTimerExpired += (_, _) =>
            {
                Gpio.WritePin(PinValue.Low);
                Thread.Sleep(125);
                Gpio.WritePin(PinValue.High);
            };
        }
    }

    public class HeatingLedController : LedController
    {
        public HeatingLedController(CustomOutputGpio gpio) : base(gpio)
        {
            Gpio.WritePin(PinValue.High);
        }

        public void TurnOn()
        {
            ulong us = (ulong)(TimeSpan.FromMilliseconds(500).Ticks / 10);
            LedTimer.StartOnePeriodic(us);
            Gpio.WritePin(PinValue.High);
        }

        public void TurnOff()
        {
            LedTimer.Stop();
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
            LedTimer.Stop();
            Gpio.WritePin(PinValue.Low);
        }

        public void Running()
        {
            ulong us = (ulong)(TimeSpan.FromMilliseconds(500).Ticks / 10);
            LedTimer.StartOnePeriodic(us);
            Gpio.WritePin(PinValue.High);
        }
    }

    public class CustomOutputGpio
    {
        private readonly GpioPin _gpio;


        public CustomOutputGpio(GpioController gpioController, int pin)
        {
            _gpio = gpioController.OpenPin(pin, PinMode.Output);
        }

        public void WritePin(PinValue pinValue)
        {
            _gpio.Write(pinValue);
        }
    }

    public class HeatService
    {
        private readonly HighResTimer _heatingTimer;
        private readonly HighResTimer _coolingTimer;
        private readonly HighResTimer _jobTimer;
        private readonly ulong _heatingUs;
        private readonly ulong _coolingUs;

        public EventHandler HeatingActionEventHandler;

        public HeatService(ulong heatingUs, ulong coolingUs)
        {
            _coolingUs = coolingUs;
            _heatingUs = heatingUs;
            _heatingTimer = new HighResTimer();
            _coolingTimer = new HighResTimer();
            _jobTimer = new HighResTimer();

            _heatingTimer.OnHighResTimerExpired += _OnHeating;
            _coolingTimer.OnHighResTimerExpired += _OnCooling;
            _jobTimer.OnHighResTimerExpired += _OnStop;
        }

        public void ExecuteHeating(ulong delay)
        {
            Console.WriteLine("ExecuteHeating:" + delay.ToString());
            if (delay == 0)
            {
                _reset();
            }
            else
            {
                _jobTimer.StartOnePeriodic(delay);
                _heatingTimer.StartOnePeriodic(_heatingUs);
                HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Heating));
            }
        }

        private void _OnHeating(HighResTimer sender, object e)
        {
            _heatingTimer.Stop();
            _coolingTimer.StartOnePeriodic(_coolingUs);
            HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Cooling));
        }

        private void _OnCooling(HighResTimer sender, object e)
        {
            _coolingTimer.Stop();
            _heatingTimer.StartOnePeriodic(_heatingUs);
            HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(HeatingStatus.Heating));
        }

        private void _OnStop(HighResTimer sender, object e)
        {
            Console.WriteLine("On stop");
            _reset();
        }


        private void _reset()
        {
            Console.WriteLine("On reset");
            _coolingTimer.Stop();
            _heatingTimer.Stop();
            _jobTimer.Stop();
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


    public class AnalogButtonEventArgs : EventArgs
    {
        public readonly ButtonType BtnType;

        public AnalogButtonEventArgs(ButtonType type)
        {
            BtnType = type;
        }
    }

    public class AdcService
    {
        private readonly AnalogButton[][] _buttonsSet;
        private readonly AnalogReader[] _adcSets;
        private Timer _timer;
        public EventHandler OnPressButonEventHandler;

        public AdcService(int[] channels, AdcController controller)
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

        public void StartScan()
        {
            _timer = new Timer(_OnScanButtons, null, 0, 250);
        }


        private void _OnScanButtons(object e)
        {
            AnalogButton btn = GetPressedButton();
            if (btn != null && OnPressButonEventHandler != null)
            {
                OnPressButonEventHandler.Invoke(this, new AnalogButtonEventArgs(btn.Type));
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
            AnalogButton hours8Btn = new AnalogButton(ButtonType.Program1, 1800, 2100);
            AnalogButton hours6Btn = new AnalogButton(ButtonType.Program2, 2500, 2800);
            AnalogButton hours4Btn = new AnalogButton(ButtonType.Program3, 3150, 3450);
            return new[] { hours8Btn, hours6Btn, hours4Btn, };
        }

        private AnalogButton[] InitSecondButtonSet()
        {
            AnalogButton hours2Btn = new AnalogButton(ButtonType.Program4, 1800, 2100);
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
        Program1,
        Program2,
        Program3,
        Program4,
        Reset,
    }

    public enum DevelopStage
    {
        Develop,
        Production,
    }

    public enum HeatingStatus
    {
        Heating,
        Cooling,
        Stop,
    }

    public static class Utils
    {
        public static ulong ConvertFromTsToUs(TimeSpan timeSpan) => (ulong)timeSpan.Ticks / 10;
    }
}