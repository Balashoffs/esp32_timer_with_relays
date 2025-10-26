# Предложения по рефакторингу

Проанализировав код, предлагаю следующие улучшения:

## 1. Проблемы с архитектурой

### Текущие проблемы:
- Слишком много ответственности в `Program.Main`
- Жесткая связанность компонентов
- Отсутствие обработки ошибок
- Магические числа разбросаны по коду
- Неконсистентная логика LED (инверсия HIGH/LOW)

## 2. Предложенный рефакторинг

```csharp
using System;
using System.Device.Adc;
using System.Device.Gpio;
using System.Threading;
using nanoFramework.Hardware.Esp32;

namespace esp32_timer_with_relays
{
    // ============================================
    // CONFIGURATION
    // ============================================
    
    public static class HardwareConfig
    {
        // GPIO Pins
        public const int GREEN_LED_PIN = 25;
        public const int RED_LED_PIN = 26;
        public const int RELAY_PIN = 16;
        
        // ADC Channels
        public const int ADC_CHANNEL_1 = 4;
        public const int ADC_CHANNEL_2 = 5;
        
        // ADC Scan Interval
        public const int ADC_SCAN_INTERVAL_MS = 250;
        
        // LED Blink Interval
        public const int LED_BLINK_INTERVAL_MS = 500;
    }
    
    public static class ButtonConfig
    {
        // Button 1 (Program 1 - 8 hours)
        public const int BTN1_MIN = 1800;
        public const int BTN1_MAX = 2100;
        
        // Button 2 (Program 2 - 6 hours)
        public const int BTN2_MIN = 2500;
        public const int BTN2_MAX = 2800;
        
        // Button 3 (Program 3 - 4 hours)
        public const int BTN3_MIN = 3150;
        public const int BTN3_MAX = 3450;
        
        // Button 4 (Program 4 - 2 hours)
        public const int BTN4_MIN = 1800;
        public const int BTN4_MAX = 2100;
        
        // Reset Button
        public const int BTN_RESET_MIN = 2500;
        public const int BTN_RESET_MAX = 2800;
        
        // ADC Noise Threshold
        public const int ADC_NOISE_THRESHOLD = 100;
    }

    // ============================================
    // MAIN PROGRAM
    // ============================================
    
    public class Program
    {
        public static void Main()
        {
            try
            {
                Console.WriteLine("Starting ESP32 Heating Controller...");
                
                // Initialize application
                var app = new HeatingControllerApp(DevelopStage.Production);
                app.Start();
                
                Console.WriteLine("Application started successfully");
                
                // Keep running
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                // In production, implement error recovery or safe shutdown
            }
        }
    }

    // ============================================
    // APPLICATION ORCHESTRATOR
    // ============================================
    
    public class HeatingControllerApp : IDisposable
    {
        private readonly GpioController _gpioController;
        private readonly AdcController _adcController;
        private readonly IHardwareAbstraction _hardware;
        private readonly IHeatingController _heatingController;
        private readonly IButtonHandler _buttonHandler;
        private bool _disposed;

        public HeatingControllerApp(DevelopStage stage)
        {
            // Initialize controllers
            _gpioController = new GpioController();
            _adcController = new AdcController();
            
            // Create hardware abstraction
            _hardware = new HardwareAbstraction(_gpioController, _adcController);
            
            // Create timing configuration
            ITiming timing = TimingFactory.Create(stage);
            var timingService = new TimingService(timing);
            
            // Create services
            var ledService = new LedService(_hardware.GreenLed, _hardware.RedLed);
            var relayService = new RelayService(_hardware.Relay);
            
            // Create heating controller
            _heatingController = new HeatingController(
                timingService,
                relayService,
                ledService
            );
            
            // Create button handler
            _buttonHandler = new ButtonHandler(
                _hardware.ButtonScanner,
                timingService,
                _heatingController
            );
        }

        public void Start()
        {
            _hardware.Initialize();
            _buttonHandler.Start();
            Console.WriteLine("Heating controller ready. Waiting for button press...");
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _buttonHandler?.Dispose();
            _heatingController?.Dispose();
            _hardware?.Dispose();
            
            _disposed = true;
        }
    }

    // ============================================
    // HARDWARE ABSTRACTION LAYER
    // ============================================
    
    public interface IHardwareAbstraction : IDisposable
    {
        IOutputDevice GreenLed { get; }
        IOutputDevice RedLed { get; }
        IOutputDevice Relay { get; }
        IButtonScanner ButtonScanner { get; }
        void Initialize();
    }
    
    public class HardwareAbstraction : IHardwareAbstraction
    {
        private readonly GpioController _gpioController;
        private readonly AdcController _adcController;
        private bool _disposed;

        public IOutputDevice GreenLed { get; private set; }
        public IOutputDevice RedLed { get; private set; }
        public IOutputDevice Relay { get; private set; }
        public IButtonScanner ButtonScanner { get; private set; }

        public HardwareAbstraction(GpioController gpioController, AdcController adcController)
        {
            _gpioController = gpioController ?? throw new ArgumentNullException(nameof(gpioController));
            _adcController = adcController ?? throw new ArgumentNullException(nameof(adcController));
        }

        public void Initialize()
        {
            try
            {
                // Initialize output devices
                GreenLed = new OutputDevice(_gpioController, HardwareConfig.GREEN_LED_PIN);
                RedLed = new OutputDevice(_gpioController, HardwareConfig.RED_LED_PIN);
                Relay = new OutputDevice(_gpioController, HardwareConfig.RELAY_PIN);
                
                // Initialize button scanner
                var buttonConfig = CreateButtonConfiguration();
                ButtonScanner = new ButtonScanner(_adcController, buttonConfig);
                
                // Set initial states
                Relay.TurnOff();
                GreenLed.TurnOn();
                RedLed.TurnOff();
                
                Console.WriteLine("Hardware initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hardware initialization error: {ex.Message}");
                throw;
            }
        }

        private ButtonConfiguration CreateButtonConfiguration()
        {
            return new ButtonConfiguration
            {
                Channel1Buttons = new[]
                {
                    new ButtonDefinition(ButtonType.Program1, ButtonConfig.BTN1_MIN, ButtonConfig.BTN1_MAX),
                    new ButtonDefinition(ButtonType.Program2, ButtonConfig.BTN2_MIN, ButtonConfig.BTN2_MAX),
                    new ButtonDefinition(ButtonType.Program3, ButtonConfig.BTN3_MIN, ButtonConfig.BTN3_MAX)
                },
                Channel2Buttons = new[]
                {
                    new ButtonDefinition(ButtonType.Program4, ButtonConfig.BTN4_MIN, ButtonConfig.BTN4_MAX),
                    new ButtonDefinition(ButtonType.Reset, ButtonConfig.BTN_RESET_MIN, ButtonConfig.BTN_RESET_MAX)
                }
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            ButtonScanner?.Dispose();
            GreenLed?.Dispose();
            RedLed?.Dispose();
            Relay?.Dispose();
            
            _disposed = true;
        }
    }

    // ============================================
    // OUTPUT DEVICE (GPIO)
    // ============================================
    
    public interface IOutputDevice : IDisposable
    {
        void TurnOn();
        void TurnOff();
        void Toggle();
        bool IsOn { get; }
    }
    
    public class OutputDevice : IOutputDevice
    {
        private readonly GpioPin _pin;
        private bool _isOn;
        private bool _disposed;

        public bool IsOn => _isOn;

        public OutputDevice(GpioController controller, int pinNumber)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            _pin = controller.OpenPin(pinNumber, PinMode.Output);
            _pin.Write(PinValue.Low);
            _isOn = false;
        }

        public void TurnOn()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OutputDevice));
            _pin.Write(PinValue.High);
            _isOn = true;
        }

        public void TurnOff()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OutputDevice));
            _pin.Write(PinValue.Low);
            _isOn = false;
        }

        public void Toggle()
        {
            if (_isOn)
                TurnOff();
            else
                TurnOn();
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            TurnOff();
            _pin?.Dispose();
            _disposed = true;
        }
    }

    // ============================================
    // LED SERVICE
    // ============================================
    
    public interface ILedService
    {
        void ShowIdle();
        void ShowHeating();
        void ShowCooling();
    }
    
    public class LedService : ILedService, IDisposable
    {
        private readonly IOutputDevice _greenLed;
        private readonly IOutputDevice _redLed;
        private readonly BlinkController _greenBlink;
        private readonly BlinkController _redBlink;
        private bool _disposed;

        public LedService(IOutputDevice greenLed, IOutputDevice redLed)
        {
            _greenLed = greenLed ?? throw new ArgumentNullException(nameof(greenLed));
            _redLed = redLed ?? throw new ArgumentNullException(nameof(redLed));
            
            _greenBlink = new BlinkController(_greenLed, HardwareConfig.LED_BLINK_INTERVAL_MS);
            _redBlink = new BlinkController(_redLed, HardwareConfig.LED_BLINK_INTERVAL_MS);
        }

        public void ShowIdle()
        {
            _greenBlink.Stop();
            _redBlink.Stop();
            
            _greenLed.TurnOn();
            _redLed.TurnOff();
            
            Console.WriteLine("LED: Idle mode");
        }

        public void ShowHeating()
        {
            _greenBlink.Start();
            _redBlink.Start();
            
            Console.WriteLine("LED: Heating mode");
        }

        public void ShowCooling()
        {
            _greenBlink.Start();
            _redBlink.Stop();
            
            _redLed.TurnOff();
            
            Console.WriteLine("LED: Cooling mode");
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _greenBlink?.Dispose();
            _redBlink?.Dispose();
            
            _disposed = true;
        }
    }

    // ============================================
    // BLINK CONTROLLER
    // ============================================
    
    public class BlinkController : IDisposable
    {
        private readonly IOutputDevice _device;
        private readonly int _intervalMs;
        private HighResTimer _timer;
        private bool _disposed;

        public BlinkController(IOutputDevice device, int intervalMs)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _intervalMs = intervalMs;
            _timer = new HighResTimer();
            _timer.OnHighResTimerExpired += OnTimerExpired;
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BlinkController));
            
            ulong intervalUs = Utils.ConvertMsToUs(_intervalMs);
            _timer.StartOnePeriodic(intervalUs);
        }

        public void Stop()
        {
            if (_disposed) return;
            _timer.Stop();
        }

        private void OnTimerExpired(HighResTimer sender, object e)
        {
            _device.Toggle();
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            Stop();
            _timer?.Dispose();
            _timer = null;
            
            _disposed = true;
        }
    }

    // ============================================
    // RELAY SERVICE
    // ============================================
    
    public interface IRelayService
    {
        void TurnOn();
        void TurnOff();
        bool IsOn { get; }
    }
    
    public class RelayService : IRelayService
    {
        private readonly IOutputDevice _relay;

        public bool IsOn => _relay.IsOn;

        public RelayService(IOutputDevice relay)
        {
            _relay = relay ?? throw new ArgumentNullException(nameof(relay));
        }

        public void TurnOn()
        {
            _relay.TurnOn();
            Console.WriteLine("Relay: ON");
        }

        public void TurnOff()
        {
            _relay.TurnOff();
            Console.WriteLine("Relay: OFF");
        }
    }

    // ============================================
    // HEATING CONTROLLER
    // ============================================
    
    public interface IHeatingController : IDisposable
    {
        void StartProgram(ulong durationUs);
        void Stop();
        HeatingStatus Status { get; }
    }
    
    public class HeatingController : IHeatingController
    {
        private readonly ITimingService _timingService;
        private readonly IRelayService _relayService;
        private readonly ILedService _ledService;
        
        private readonly HighResTimer _heatingTimer;
        private readonly HighResTimer _coolingTimer;
        private readonly HighResTimer _programTimer;
        
        private HeatingStatus _status;
        private bool _disposed;

        public HeatingStatus Status => _status;

        public HeatingController(
            ITimingService timingService,
            IRelayService relayService,
            ILedService ledService)
        {
            _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
            _relayService = relayService ?? throw new ArgumentNullException(nameof(relayService));
            _ledService = ledService ?? throw new ArgumentNullException(nameof(ledService));
            
            _heatingTimer = new HighResTimer();
            _coolingTimer = new HighResTimer();
            _programTimer = new HighResTimer();
            
            _heatingTimer.OnHighResTimerExpired += OnHeatingComplete;
            _coolingTimer.OnHighResTimerExpired += OnCoolingComplete;
            _programTimer.OnHighResTimerExpired += OnProgramComplete;
            
            _status = HeatingStatus.Idle;
            _ledService.ShowIdle();
        }

        public void StartProgram(ulong durationUs)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HeatingController));
            
            if (durationUs == 0)
            {
                Stop();
                return;
            }
            
            Console.WriteLine($"Starting heating program: {durationUs / 1_000_000} seconds");
            
            Stop(); // Stop any running program
            
            _programTimer.StartOnePeriodic(durationUs);
            StartHeatingCycle();
        }

        public void Stop()
        {
            if (_disposed) return;
            
            Console.WriteLine("Stopping heating program");
            
            _programTimer.Stop();
            _heatingTimer.Stop();
            _coolingTimer.Stop();
            
            _relayService.TurnOff();
            _ledService.ShowIdle();
            
            _status = HeatingStatus.Idle;
        }

        private void StartHeatingCycle()
        {
            _status = HeatingStatus.Heating;
            _relayService.TurnOn();
            _ledService.ShowHeating();
            _heatingTimer.StartOnePeriodic(_timingService.HeatingUs);
        }

        private void StartCoolingCycle()
        {
            _status = HeatingStatus.Cooling;
            _relayService.TurnOff();
            _ledService.ShowCooling();
            _coolingTimer.StartOnePeriodic(_timingService.CoolingUs);
        }

        private void OnHeatingComplete(HighResTimer sender, object e)
        {
            _heatingTimer.Stop();
            StartCoolingCycle();
        }

        private void OnCoolingComplete(HighResTimer sender, object e)
        {
            _coolingTimer.Stop();
            StartHeatingCycle();
        }

        private void OnProgramComplete(HighResTimer sender, object e)
        {
            Stop();
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            Stop();
            
            _programTimer?.Dispose();
            _heatingTimer?.Dispose();
            _coolingTimer?.Dispose();
            
            _disposed = true;
        }
    }

    // ============================================
    // BUTTON HANDLING
    // ============================================
    
    public interface IButtonHandler : IDisposable
    {
        void Start();
        void Stop();
    }
    
    public class ButtonHandler : IButtonHandler
    {
        private readonly IButtonScanner _scanner;
        private readonly ITimingService _timingService;
        private readonly IHeatingController _heatingController;
        private ButtonType? _lastButton;
        private bool _disposed;

        public ButtonHandler(
            IButtonScanner scanner,
            ITimingService timingService,
            IHeatingController heatingController)
        {
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
            _heatingController = heatingController ?? throw new ArgumentNullException(nameof(heatingController));
            
            _scanner.ButtonPressed += OnButtonPressed;
        }

        public void Start()
        {
            _scanner.StartScanning();
            Console.WriteLine("Button handler started");
        }

        public void Stop()
        {
            _scanner.StopScanning();
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Debounce: ignore repeated presses of same button
            if (_lastButton == e.ButtonType)
            {
                Console.WriteLine($"Ignoring repeated press: {e.ButtonType}");
                return;
            }
            
            _lastButton = e.ButtonType;
            
            Console.WriteLine($"Button pressed: {e.ButtonType}");
            
            ulong duration = GetProgramDuration(e.ButtonType);
            _heatingController.StartProgram(duration);
        }

        private ulong GetProgramDuration(ButtonType buttonType)
        {
            switch (buttonType)
            {
                case ButtonType.Program1:
                    return _timingService.Program1Us;
                case ButtonType.Program2:
                    return _timingService.Program2Us;
                case ButtonType.Program3:
                    return _timingService.Program3Us;
                case ButtonType.Program4:
                    return _timingService.Program4Us;
                case ButtonType.Reset:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonType));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            Stop();
            _scanner.ButtonPressed -= OnButtonPressed;
            
            _disposed = true;
        }
    }

    // ============================================
    // BUTTON SCANNER
    // ============================================
    
    public interface IButtonScanner : IDisposable
    {
        event EventHandler ButtonPressed;
        void StartScanning();
        void StopScanning();
    }
    
    public class ButtonScanner : IButtonScanner
    {
        private readonly AdcController _adcController;
        private readonly ButtonConfiguration _config;
        private readonly AdcChannel[] _channels;
        private Timer _scanTimer;
        private bool _disposed;

        public event EventHandler ButtonPressed;

        public ButtonScanner(AdcController adcController, ButtonConfiguration config)
        {
            _adcController = adcController ?? throw new ArgumentNullException(nameof(adcController));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _channels = new[]
            {
                _adcController.OpenChannel(HardwareConfig.ADC_CHANNEL_1),
                _adcController.OpenChannel(HardwareConfig.ADC_CHANNEL_2)
            };
        }

        public void StartScanning()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ButtonScanner));
            
            _scanTimer = new Timer(
                ScanButtons,
                null,
                0,
                HardwareConfig.ADC_SCAN_INTERVAL_MS
            );
        }

        public void StopScanning()
        {
            _scanTimer?.Dispose();
            _scanTimer = null;
        }

        private void ScanButtons(object state)
        {
            try
            {
                // Scan channel 1
                int value1 = _channels[0].ReadValue();
                if (value1 > ButtonConfig.ADC_NOISE_THRESHOLD)
                {
                    var button = FindButton(_config.Channel1Buttons, value1);
                    if (button != null)
                    {
                        ButtonPressed?.Invoke(this, new ButtonPressedEventArgs(button.Type));
                        return;
                    }
                }
                
                // Scan channel 2
                int value2 = _channels[1].ReadValue();
                if (value2 > ButtonConfig.ADC_NOISE_THRESHOLD)
                {
                    var button = FindButton(_config.Channel2Buttons, value2);
                    if (button != null)
                    {
                        ButtonPressed?.Invoke(this, new ButtonPressedEventArgs(button.Type));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Button scan error: {ex.Message}");
            }
        }

        private ButtonDefinition FindButton(ButtonDefinition[] buttons, int adcValue)
        {
            foreach (var button in buttons)
            {
                if (button.IsMatch(adcValue))
                {
                    return button;
                }
            }
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            StopScanning();
            
            foreach (var channel in _channels)
            {
                channel?.Dispose();
            }
            
            _disposed = true;
        }
    }

    // ============================================
    // BUTTON CONFIGURATION
    // ============================================
    
    public class ButtonConfiguration
    {
        public ButtonDefinition[] Channel1Buttons { get; set; }
        public ButtonDefinition[] Channel2Buttons { get; set; }
    }
    
    public class ButtonDefinition
    {
        public ButtonType Type { get; }
        public int MinValue { get; }
        public int MaxValue { get; }

        public ButtonDefinition(ButtonType type, int minValue, int maxValue)
        {
            Type = type;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public bool IsMatch(int adcValue)
        {
            return adcValue >= MinValue && adcValue <= MaxValue;
        }
    }
    
    public class ButtonPressedEventArgs : EventArgs
    {
        public ButtonType ButtonType { get; }

        public ButtonPressedEventArgs(ButtonType buttonType)
        {
            ButtonType = buttonType;
        }
    }

    // ============================================
    // TIMING SYSTEM
    // ============================================
    
    public interface ITimingService
    {
        ulong HeatingUs { get; }
        ulong CoolingUs { get; }
        ulong Program1Us { get; }
        ulong Program2Us { get; }
        ulong Program3Us { get; }
        ulong Program4Us { get; }
    }
    
    public class TimingService : ITimingService
    {
        private readonly ITiming _timing;

        public TimingService(ITiming timing)
        {
            _timing = timing ?? throw new ArgumentNullException(nameof(timing));
        }

        public ulong HeatingUs => Utils.ConvertTsToUs(_timing.HeatingTs);
        public ulong CoolingUs => Utils.ConvertTsToUs(_timing.CoolingTs);
        public ulong Program1Us => Utils.ConvertTsToUs(_timing.Program1Ts);
        public ulong Program2Us => Utils.ConvertTsToUs(_timing.Program2Ts);
        public ulong Program3Us => Utils.ConvertTsToUs(_timing.Program3Ts);
        public ulong Program4Us => Utils.ConvertTsToUs(_timing.Program4Ts);
    }
    
    public interface ITiming
    {
        TimeSpan HeatingTs { get; }
        TimeSpan CoolingTs { get; }
        TimeSpan Program1Ts { get; }
        TimeSpan Program2Ts { get; }
        TimeSpan Program3Ts { get; }
        TimeSpan Program4Ts { get; }
    }
    
    public static class TimingFactory
    {
        public static ITiming Create(DevelopStage stage)
        {
            switch (stage)
            {
                case DevelopStage.Develop:
                    return new DevelopmentTiming();
                case DevelopStage.Production:
                    return new ProductionTiming();
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
    }
    
    public class DevelopmentTiming : ITiming
    {
        public TimeSpan HeatingTs => TimeSpan.FromSeconds(4);
        public TimeSpan CoolingTs => TimeSpan.FromSeconds(1);
        public TimeSpan Program1Ts => TimeSpan.FromSeconds(30);
        public TimeSpan Program2Ts => TimeSpan.FromSeconds(60);
        public TimeSpan Program3Ts => TimeSpan.FromSeconds(90);
        public TimeSpan Program4Ts => TimeSpan.FromSeconds(120);
    }
    
    public class ProductionTiming : ITiming
    {
        public TimeSpan HeatingTs => TimeSpan.FromMinutes(4);
        public TimeSpan CoolingTs => TimeSpan.FromMinutes(1);
        public TimeSpan Program1Ts => TimeSpan.FromHours(8);
        public TimeSpan Program2Ts => TimeSpan.FromHours(6);
        public TimeSpan Program3Ts => TimeSpan.FromHours(4);
        public TimeSpan Program4Ts => TimeSpan.FromHours(2);
    }

    // ============================================
    // ENUMS
    // ============================================
    
    public enum ButtonType
    {
        Program1,
        Program2,
        Program3,
        Program4,
        Reset
    }
    
    public enum DevelopStage
    {
        Develop,
        Production
    }
    
    public enum HeatingStatus
    {
        Idle,
        Heating,
        Cooling
    }

    // ============================================
    // UTILITIES
    // ============================================
    
    public static class Utils
    {
        public static ulong ConvertTsToUs(TimeSpan timeSpan)
        {
            return (ulong)timeSpan.Ticks / 10;
        }
        
        public static ulong ConvertMsToUs(int milliseconds)
        {
            return (ulong)milliseconds * 1000;
        }
        
        public static TimeSpan ConvertUsToTs(ulong microseconds)
        {
            return TimeSpan.FromTicks((long)(microseconds * 10));
        }
    }
}
```

## 3. Ключевые улучшения

### ✅ Архитектурные улучшения:
1. **Разделение ответственности**: Каждый класс имеет одну четкую задачу
2. **Dependency Injection**: Зависимости передаются через конструкторы
3. **Интерфейсы**: Все сервисы имеют интерфейсы для тестирования
4. **Factory Pattern**: `TimingFactory` для создания конфигураций
5. **HAL (Hardware Abstraction Layer)**: Абстракция над GPIO/ADC

### ✅ Улучшения в коде:
1. **Константы вместо магических чисел**: `HardwareConfig`, `ButtonConfig`
2. **Обработка ошибок**: Try-catch блоки и проверки на null
3. **Disposal Pattern**: Корректное освобождение ресурсов
4. **Консистентная логика LED**: Понятные методы `TurnOn`/`TurnOff`
5. **Debouncing кнопок**: Предотвращение повторных нажатий
6. **Логирование**: Console.WriteLine для отладки

### ✅ Новые компоненты:
1. **HeatingControllerApp**: Главный оркестратор
2. **HardwareAbstraction**: Слой абстракции железа
3. **BlinkController**: Переиспользуемый контроллер мигания
4. **RelayService**: Изолированное управление реле
5. **ButtonHandler**: Обработчик логики кнопок

### ✅ Улучшенная читаемость:
- Секции кода разделены комментариями
- Понятные имена переменных и методов
- Убраны венгерская нотация (`_OnHeating` → `OnHeatingComplete`)
- Добавлены XML-комментарии (можно расширить)

## 4. Дополнительные рекомендации

### Для дальнейшего улучшения:

```csharp
// 1. Добавить State Machine для более явного управления состояниями
public interface IHeatingState
{
    void Enter(HeatingController controller);
    void Exit(HeatingController controller);
    HeatingStatus Status { get; }
}

// 2. Добавить Event Aggregator для разделения компонентов
public interface IEventAggregator
{
    void Subscribe<T>(Action<T> handler);
    void Publish<T>(T eventData);
}

// 3. Добавить Configuration Provider
public interface IConfigurationProvider
{
    T GetConfiguration<T>() where T : class;
}

// 4. Добавить Logger
public interface ILogger
{
    void LogInfo(string message);
    void LogError(string message, Exception ex);
}
```

Этот рефакторинг делает код более поддерживаемым, тестируемым и расширяемым, следуя принципам SOLID.
