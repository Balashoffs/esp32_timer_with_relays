using System;
using System.Device.Adc;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;

namespace esp32_timer_with_relays
{
    /*
     * „етыре временных интервалов
     *  - 2 часа (ch 4, max 2000, min 1900)
     *  - 1.5 часа (ch 4, max 2700, min 2600)
     *  - 1 час (ch 4, max 3350, min 3250)
     *  - 0.5 часа (ch 5, max 2000, min 1900)
     *   нопка сброса (ch 5, max 2700, min 2600)
     */
    public class Program
    {
        public static void Main()
        {
            TimerController t = new TimerController(new int[] { 4, 5 });
            TimerProvider timerProvider = new TimerProvider();
            TimerWorkLed ledProvider = new TimerWorkLed(25);
            CustomGpio readyLed = new CustomGpio(27);
            timerProvider.stopTimerEventhadler += (sender, args) =>
            {
                ledProvider.changeLedState(((TimerEventArgs)args).isStop);

            };
            t.onPressButoneventHandler += (sender, args) =>
            {
                Button btn = ((PressButtonEventArgs)args).pressedButton;
                Debug.WriteLine("Button is " + btn.type.ToString());
                switch (btn.type)
                {
                    case ButtonType.min120:
                        timerProvider.startCount(16000);
                        break;
                    case ButtonType.min90:
                        timerProvider.startCount(8000);
                        break;
                    case ButtonType.min60:
                        timerProvider.startCount(4000);
                        Debug.WriteLine("min60: start");
                        break;
                    case ButtonType.min30:
                        timerProvider.startCount(2000);
                        break;
                    default:
                        timerProvider.ResetTimer();
                        break;
                }
            };
            t.startScanButtons();
            readyLed.writePin(PinValue.Low);
            Thread.Sleep(Timeout.Infinite);
        }
    }

    public interface ITimerWorkedState
    {
        public void changeLedState(bool isStop);
    }

    public class TimerWorkLed : CustomGpio, ITimerWorkedState
    {
        private Timer timer;

        public TimerWorkLed(int pin):base(pin)
        {
            timer = new Timer(TimerCallback, null, 1000, 2000);
        }
        public void changeLedState(bool isStop)
        {
            if (isStop)
            {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                timer.Change(0, 500);
            }
        }

        private void TimerCallback(object state)
        {
            writePin(PinValue.Low);
            Thread.Sleep(125);
            writePin(PinValue.High);
        }
    }

    public class CustomRelay : CustomGpio, ITimerWorkedState
    {
        public CustomRelay(int pin) : base(pin) { }

        public void changeLedState(bool isStop)
        {
            if (isStop)
            {
                writePin(PinValue.Low);
            }
            else
            {
                writePin(PinValue.High);
            }
        }
    }

    public class CustomGpio
    {
        private GpioPin runLed;
        

        public CustomGpio(int pin)
        {
            runLed = new GpioController().OpenPin(pin, PinMode.Output);
            writePin(PinValue.High);
        }

        public void writePin(PinValue pinValue)
        {
            runLed.Write(pinValue);
        }
    }

    public class TimerProvider
    {
        private Timer timer;
        public EventHandler stopTimerEventhadler;

        public TimerProvider()
        {
            timer = new Timer(StopTimer, null, 0, 0);
        }
        public void startCount(int delay)
        {
            Debug.WriteLine("Start timer for " + delay.ToString() + " mills");
            timer.Change(delay, Timeout.Infinite);
            stopTimerEventhadler?.Invoke(this, new TimerEventArgs(false));
            Debug.WriteLine(DateTime.UtcNow.ToUnixTimeSeconds().ToString() + ": start");
        }


        public void StopTimer(object state)
        {
            Debug.WriteLine(DateTime.UtcNow.ToUnixTimeSeconds().ToString() + ": stop");
            stopTimerEventhadler?.Invoke(this, new TimerEventArgs(true));
        }

        public void ResetTimer()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            stopTimerEventhadler?.Invoke(this, new TimerEventArgs(true));
        }
    }

    public class PressButtonEventArgs : EventArgs {
        public readonly Button pressedButton;

        public PressButtonEventArgs(Button onPressedBtn)
        {
            this.pressedButton = onPressedBtn;
        }
    }

    public class TimerEventArgs : EventArgs
    {
        public readonly bool isStop;

        public TimerEventArgs(bool isStop)
        {
            this.isStop = isStop;
        }
    }

    public class TimerController
    {
        Button[][] buttonsSet;
        AnanlogButtonSet[] adcSets;

        public EventHandler onPressButoneventHandler;
        public TimerController(int[] chls)
        {
            adcSets = new AnanlogButtonSet[chls.Length];
            buttonsSet = new Button[chls.Length][];

            for (int i = 0; i < chls.Length; i++)
            {
                AnanlogButtonSet set = new AnanlogButtonSet(chls[i]);
                adcSets[i] = set;
                switch (i)
                {
                    case 0:
                        buttonsSet[i] = InitFirstButtonSet(set);
                        break;
                    case 1:
                        buttonsSet[i] = InitSecondButtonSet(set);
                        break;
                }
            }
        }

        public void startScanButtons()
        {
            new Thread(() =>
            {
                while (true)
                {
                    Button btn = scan();
                    if (btn != null && onPressButoneventHandler != null)
                    {
                        onPressButoneventHandler.Invoke(this, new PressButtonEventArgs(btn));
                    }
                    Thread.Sleep(250);
                }
            }).Start();
        }

        private Button scan()
        {
            for (int i = 0; i < adcSets.Length; i++)
            {
                int value = adcSets[i].readValue();
                for (int j = 0; j < buttonsSet[i].Length; j++)
                {
                    Button btn = buttonsSet[i][j];
                    if (btn.isNeedBtn(value))
                    {
                        Console.WriteLine("The button " + btn.type.ToString() + " was pressed");
                        return btn;
                    }
                }
            }
            return null;
        }

        private Button[] InitFirstButtonSet(AnanlogButtonSet set)
        {
            Button btn120Minutes = new Button(ButtonType.min120, 1800, 2100);
            Button btn90Minutes = new Button(ButtonType.min90, 2500, 2800);
            Button btn60Minutes = new Button(ButtonType.min60, 3150, 3450);
            return new Button[] { btn120Minutes, btn90Minutes, btn60Minutes, };
        }

        private Button[] InitSecondButtonSet(AnanlogButtonSet set)
        {
            Button btn30Minutes = new Button(ButtonType.min30, 1800, 2100);
            Button reset = new Button(ButtonType.reset, 2500, 2800);
            Button dev = new Button(ButtonType.dev, 3150, 3450);
            return new Button[] { btn30Minutes, reset, dev, };
        }

    }

    
    public class AnanlogButtonSet
    {
        private AdcController adc;
        private AdcChannel cnl;
        
        public AnanlogButtonSet(int chlPin)
        {
            adc = new AdcController();
            cnl = adc.OpenChannel(chlPin);
        }

        public int readValue()
        {
            int v = cnl.ReadValue();
            if(v > 100) Console.WriteLine(v.ToString());
            return v;
        }

    }

    public class Button
    {
        private readonly int min, max;
        public readonly ButtonType type;

        public Button(ButtonType type, int min, int max)
        {
            this.type = type;
            this.max = max;
            this.min = min;
        }

        public bool isNeedBtn(int value) => value > min && value < max;

    }

    public enum ButtonType
    {
        min120,
        min90,
        min60,
        min30,
        reset,dev
    }
}
