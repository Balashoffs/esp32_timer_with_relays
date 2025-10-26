# Подробное описание пакетов nanoFramework

## Обзор nanoFramework

**nanoFramework** — это бесплатная открытая платформа для разработки встраиваемых приложений на языке C# для микроконтроллеров с ограниченными ресурсами. Она представляет собой минимизированную версию .NET Framework, оптимизированную для устройств IoT.

## Используемые пакеты

### 1. System (основной пакет)

**Описание:**  
Базовый пакет .NET, содержащий фундаментальные типы и классы.

**Используемые компоненты в программе:**

#### System.EventArgs
```csharp
public class HeatingActionEventArgs : EventArgs
{
    public HeatingStatus Status { get; }
}
```
- Базовый класс для всех данных событий
- Используется для создания пользовательских аргументов событий
- Позволяет передавать информацию между компонентами через события

#### System.EventHandler
```csharp
public EventHandler HeatingActionEventHandler;
public EventHandler OnPressButonEventHandler;
```
- Делегат для обработки событий
- Сигнатура: `void EventHandler(object sender, EventArgs e)`
- Используется для связывания компонентов через события

#### System.TimeSpan
```csharp
public TimeSpan HeatingTs => TimeSpan.FromMinutes(4);
public TimeSpan Program1Ts => TimeSpan.FromHours(8);
```
- Представляет временной интервал
- Методы создания:
    - `TimeSpan.FromSeconds()`
    - `TimeSpan.FromMinutes()`
    - `TimeSpan.FromHours()`
    - `TimeSpan.FromMilliseconds()`
- Свойство `Ticks`: количество 100-наносекундных интервалов
- Используется для точного представления временных интервалов

#### System.Threading.Thread
```csharp
Thread.Sleep(Timeout.Infinite);
Thread.Sleep(125);
Thread.Sleep(500);
```
- Управление потоками выполнения
- `Thread.Sleep(ms)`: приостанавливает текущий поток на указанное время
- `Timeout.Infinite`: бесконечное ожидание
- Используется для:
    - Блокировки главного потока
    - Задержек между операциями с GPIO
    - Стабилизации состояния светодиодов

#### System.Threading.Timer
```csharp
_timer = new Timer(_OnScanButtons, null, 0, 250);
```
- Периодический таймер для выполнения колбэков
- Конструктор: `Timer(callback, state, dueTime, period)`
    - `callback`: метод для вызова
    - `state`: объект состояния (обычно null)
    - `dueTime`: задержка до первого вызова (мс)
    - `period`: интервал между вызовами (мс)
- Работает в отдельном потоке
- Используется для периодического опроса кнопок (каждые 250 мс)

#### System.Exception
```csharp
throw new Exception();
throw new ArgumentOutOfRangeException();
```
- Базовый класс для всех исключений
- Используется для обработки ошибок
- В программе выбрасывается при неизвестных значениях enum

---

### 2. System.Device.Gpio

**NuGet пакет:** `nanoFramework.System.Device.Gpio`  
**Версия:** обычно 1.1.x или выше  
**Назначение:** Управление цифровыми входами/выходами (GPIO) микроконтроллера

**Документация:** https://docs.nanoframework.net/api/System.Device.Gpio.html

#### GpioController
```csharp
GpioController gpioController = new GpioController();
```

**Описание:**  
Главный класс для работы с GPIO. Предоставляет доступ к пинам микроконтроллера.

**Основные методы:**
- `OpenPin(int pinNumber, PinMode mode)`: открывает пин для использования
- Автоматически управляет ресурсами GPIO
- Поддерживает множественные пины одновременно

**Особенности:**
- Singleton-подобное поведение для одного контроллера
- Автоматическая инициализация hardware
- Освобождение ресурсов при закрытии пинов

#### GpioPin
```csharp
_gpio = gpioController.OpenPin(pin, PinMode.Output);
_gpio.Write(pinValue);
```

**Описание:**  
Представляет отдельный GPIO пин.

**Основные методы:**
- `Write(PinValue value)`: устанавливает состояние пина
- `Read()`: читает состояние пина (для входов)
- `Toggle()`: переключает состояние пина

**Режимы пинов (PinMode):**
- `PinMode.Output`: выход (используется для светодиодов и реле)
- `PinMode.Input`: вход с высоким импедансом
- `PinMode.InputPullUp`: вход с подтяжкой к питанию
- `PinMode.InputPullDown`: вход с подтяжкой к земле

**В программе используется:**
```csharp
public CustomOutputGpio(GpioController gpioController, int pin)
{
    _gpio = gpioController.OpenPin(pin, PinMode.Output);
}

public void WritePin(PinValue pinValue)
{
    _gpio.Write(pinValue);
}
```

#### PinValue
```csharp
public enum PinValue
{
    Low = 0,
    High = 1
}
```

**Описание:**  
Перечисление состояний GPIO пина.

**Значения:**
- `PinValue.Low` (0): низкий уровень (0V, логический 0)
- `PinValue.High` (1): высокий уровень (3.3V для ESP32, логическая 1)

**Использование в программе:**
```csharp
relayPin.WritePin(PinValue.Low);   // Реле выключено
relayPin.WritePin(1);              // Реле включено (неявное преобразование)
Gpio.WritePin(PinValue.High);      // Светодиод (инвертированная логика)
```

**Важно для ESP32:**
- Выходное напряжение HIGH: 3.3V
- Максимальный ток на пин: 40 mA (рекомендуется до 20 mA)
- Необходимо использовать транзисторы/реле для мощных нагрузок

---

### 3. System.Device.Adc

**NuGet пакет:** `nanoFramework.System.Device.Adc`  
**Версия:** обычно 1.1.x или выше  
**Назначение:** Работа с аналого-цифровыми преобразователями (ADC)

**Документация:** https://docs.nanoframework.net/api/System.Device.Adc.html

#### AdcController
```csharp
AdcController adcController = new AdcController();
```

**Описание:**  
Контроллер для управления ADC каналами микроконтроллера.

**Характеристики ESP32 ADC:**
- Разрешение: 12 бит (значения 0-4095)
- Два ADC блока: ADC1 и ADC2
- Входное напряжение: 0-3.3V (с аттенюацией до 0-3.6V)
- Встроенные фильтры для уменьшения шума

**Основные методы:**
- `OpenChannel(int channelNumber)`: открывает ADC канал для чтения

**Примечание:**  
ADC2 не работает когда WiFi активен на ESP32!

#### AdcChannel
```csharp
_cnl = adc.OpenChannel(chlPin);
int value = _cnl.ReadValue();
```

**Описание:**  
Представляет один канал ADC.

**Основные методы:**
- `ReadValue()`: читает текущее значение (0-4095 для 12-бит ADC)
- Возвращает целое число, пропорциональное входному напряжению

**Формула преобразования:**
```
Напряжение = (ADC_значение / 4095) × 3.3V
```

**Маппинг каналов ADC для ESP32:**

| Канал ADC | GPIO пин | Примечание |
|-----------|----------|------------|
| 0 | GPIO 36 | VP |
| 3 | GPIO 39 | VN |
| 4 | GPIO 32 | |
| 5 | GPIO 33 | |
| 6 | GPIO 34 | Только вход |
| 7 | GPIO 35 | Только вход |

**В программе используется:**
```csharp
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
```

**Особенности чтения ADC:**
- Нелинейность на краях диапазона (особенно близко к 0V и 3.3V)
- Рекомендуется использовать средний диапазон (0.3V - 3.0V)
- Встроенный шум ±50 единиц
- Может требоваться калибровка для точных измерений

**Схема подключения кнопок (резистивный делитель):**
```
VCC (3.3V)
    |
    R1 (разное сопротивление для каждой кнопки)
    |
    +---> ADC вход
    |
    R2
    |
   GND
```

Разные кнопки создают разные делители напряжения, что дает уникальные ADC значения:
- Кнопка 1: ~1950 (напряжение ~1.6V)
- Кнопка 2: ~2650 (напряжение ~2.1V)
- Кнопка 3: ~3300 (напряжение ~2.7V)

---

### 4. nanoFramework.Hardware.Esp32

**NuGet пакет:** `nanoFramework.Hardware.Esp32`  
**Версия:** 1.6.x или выше  
**Назначение:** Специфичные функции для ESP32, включая высокоточные таймеры

**Документация:** https://docs.nanoframework.net/api/nanoFramework.Hardware.Esp32.html

#### HighResTimer
```csharp
_heatingTimer = new HighResTimer();
_heatingTimer.OnHighResTimerExpired += _OnHeating;
_heatingTimer.StartOnePeriodic(microseconds);
_heatingTimer.Stop();
```

**Описание:**  
Высокоточный аппаратный таймер ESP32 с микросекундной точностью.

**Характеристики:**
- Разрешение: 1 микросекунда (μs)
- Базируется на аппаратных таймерах ESP32
- 4 доступных таймера (0-3)
- Максимальный период: ~71 минута (2^32 микросекунд)

**Основные методы:**

##### StartOnePeriodic(ulong periodMicroseconds)
```csharp
ulong us = Utils.ConvertFromTsToUs(TimeSpan.FromMinutes(4));
_heatingTimer.StartOnePeriodic(us);
```
- Запускает периодический таймер
- Автоматически перезапускается после каждого срабатывания
- Параметр: период в микросекундах

##### Stop()
```csharp
_heatingTimer.Stop();
```
- Останавливает таймер
- Сбрасывает внутренний счетчик
- Не вызывает событие expired

##### Dispose()
```csharp
_heatingTimer.Dispose();
```
- Освобождает аппаратные ресурсы таймера
- Должен вызываться при завершении работы
- После dispose таймер нельзя использовать повторно

**События:**

##### OnHighResTimerExpired
```csharp
_heatingTimer.OnHighResTimerExpired += (sender, e) =>
{
    // Код выполняется когда таймер истекает
    Console.WriteLine("Timer expired!");
};
```
- Вызывается когда таймер достигает установленного периода
- Выполняется в контексте прерывания (ISR)
- Должен быть максимально быстрым

**⚠️ Важные ограничения ISR (Interrupt Service Routine):**

В обработчике `OnHighResTimerExpired` **НЕЛЬЗЯ**:
- Вызывать `Thread.Sleep()` - вызовет зависание
- Выполнять длительные операции (> 100 μs)
- Использовать Console.WriteLine в продакшене (только для отладки)
- Выделять большие объемы памяти

**МОЖНО**:
- Устанавливать флаги
- Изменять переменные
- Запускать/останавливать другие таймеры
- Управлять GPIO (быстрые операции)

**Пример использования в программе:**

```csharp
public class HeatService
{
    private readonly HighResTimer _heatingTimer;
    private readonly HighResTimer _coolingTimer;
    private readonly HighResTimer _jobTimer;
    private readonly ulong _heatingUs;
    private readonly ulong _coolingUs;

    public HeatService(ulong heatingUs, ulong coolingUs)
    {
        _coolingUs = coolingUs;
        _heatingUs = heatingUs;
        
        _heatingTimer = new HighResTimer();
        _coolingTimer = new HighResTimer();
        _jobTimer = new HighResTimer();

        // Подписка на события таймеров
        _heatingTimer.OnHighResTimerExpired += _OnHeating;
        _coolingTimer.OnHighResTimerExpired += _OnCooling;
        _jobTimer.OnHighResTimerExpired += _OnStop;
    }

    public void ExecuteHeating(ulong delay)
    {
        _jobTimer.StartOnePeriodic(delay);        // Общее время программы
        _heatingTimer.StartOnePeriodic(_heatingUs); // Время нагрева
    }

    private void _OnHeating(HighResTimer sender, object e)
    {
        _heatingTimer.Stop();
        _coolingTimer.StartOnePeriodic(_coolingUs);
        // Уведомление через событие
    }
}
```

**Преобразование времени для HighResTimer:**

```csharp
public static ulong ConvertFromTsToUs(TimeSpan timeSpan)
{
    // 1 Tick = 100 nanoseconds
    // 1 microsecond = 1000 nanoseconds
    // microseconds = ticks / 10
    return (ulong)timeSpan.Ticks / 10;
}

// Примеры:
TimeSpan fourMinutes = TimeSpan.FromMinutes(4);
ulong microseconds = ConvertFromTsToUs(fourMinutes);
// Результат: 240,000,000 микросекунд (4 минуты)

TimeSpan oneHour = TimeSpan.FromHours(1);
ulong us = ConvertFromTsToUs(oneHour);
// Результат: 3,600,000,000 микросекунд
```

**Аппаратные таймеры ESP32:**

ESP32 имеет 4 аппаратных таймера:
- Timer Group 0: Timer 0, Timer 1
- Timer Group 1: Timer 0, Timer 1

**Характеристики:**
- 64-битный счетчик
- Программируемый делитель (prescaler)
- Автоматическая перезагрузка
- Работают независимо от CPU

**Точность:**
- Теоретическая: 1 μs
- Практическая: ±2-5 μs (из-за jitter прерываний)
- Достаточно для большинства применений

---

## Архитектура взаимодействия пакетов

```
┌─────────────────────────────────────────────────────────┐
│                      System                              │
│  (EventHandler, TimeSpan, Thread, Timer, Exception)     │
└─────────────────────────────────────────────────────────┘
                          ▲
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌─────────────────┐
│System.Device │  │System.Device │  │nanoFramework.   │
│    .Gpio     │  │    .Adc      │  │Hardware.Esp32   │
│              │  │              │  │                 │
│GpioController│  │AdcController │  │HighResTimer     │
│   GpioPin    │  │  AdcChannel  │  │                 │
│  PinValue    │  │              │  │                 │
└──────────────┘  └──────────────┘  └─────────────────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          ▼
                ┌──────────────────┐
                │  User Program    │
                │ (HeatService,    │
                │  AdcService,     │
                │  LedService)     │
                └──────────────────┘
```

## Особенности работы с nanoFramework

### 1. Ограничения памяти
- RAM: обычно 100-500 KB
- Flash: 1-4 MB
- Избегать выделения больших объектов
- Повторно использовать объекты

### 2. Сборка мусора (GC)
```csharp
// Плохо - создает много объектов
for (int i = 0; i < 1000; i++)
{
    string s = "Value: " + i.ToString();
}

// Хорошо - минимум аллокаций
for (int i = 0; i < 1000; i++)
{
    Console.WriteLine(i);
}
```

### 3. Отладка
- Использовать `Console.WriteLine()` для вывода
- Доступна через Serial port
- В продакшене удалять debug-вызовы

### 4. Обработка исключений
```csharp
try
{
    // Рискованная операция
    _cnl.ReadValue();
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
    // Обработка ошибки
}
```

### 5. Энергопотребление
- GPIO операции: ~5-40 mA на пин
- ADC: ~1-2 mA при активном чтении
- Таймеры: минимальное потребление (hardware)
- Можно использовать sleep modes (не в этой программе)

## Установка пакетов

### Через Visual Studio
```xml
<PackageReference Include="nanoFramework.System.Device.Gpio" Version="1.*" />
<PackageReference Include="nanoFramework.System.Device.Adc" Version="1.*" />
<PackageReference Include="nanoFramework.Hardware.Esp32" Version="1.*" />
```

### Через .NET CLI
```bash
dotnet add package nanoFramework.System.Device.Gpio
dotnet add package nanoFramework.System.Device.Adc
dotnet add package nanoFramework.Hardware.Esp32
```

## Полезные ссылки

1. **Официальная документация:** https://docs.nanoframework.net
2. **GitHub реп
