# Часть 3: nanoFramework - Концепция и Принципы Работы

## Что такое nanoFramework?

**nanoFramework** — это бесплатная open-source платформа для разработки встраиваемых систем на языке C# (.NET). Это облегченная реализация .NET Framework, специально оптимизированная для работы на микроконтроллерах с ограниченными ресурсами.

### Философия nanoFramework

```
Традиционная разработка для MCU:
C/C++ → Низкоуровневое программирование → Сложная отладка → Длительная разработка

nanoFramework подход:
C# → Высокоуровневое программирование → Быстрая разработка → Простая отладка
```

### Ключевые преимущества

1. **Знакомый синтаксис:** Разработчики .NET могут сразу приступить к работе
2. **Управление памятью:** Автоматическая сборка мусора (Garbage Collection)
3. **Безопасность типов:** Компилятор предотвращает многие ошибки
4. **ООП:** Полная поддержка объектно-ориентированного программирования
5. **Отладка:** Visual Studio debugging с breakpoints и watch variables

## Архитектура nanoFramework

```
┌─────────────────────────────────────────────┐
│         Приложение на C# (.NET)             │
│  (Ваш код: Program.cs, Services, etc.)      │
├─────────────────────────────────────────────┤
│         nanoFramework Class Libraries       │
│  (System.Device.Gpio, System.Device.Adc)    │
├─────────────────────────────────────────────┤
│         Common Language Runtime (CLR)       │
│  - Исполнение IL кода                       │
│  - Управление памятью (GC)                  │
│  - Обработка исключений                     │
├─────────────────────────────────────────────┤
│         Hardware Abstraction Layer (HAL)    │
│  - ESP32 специфичные драйверы               │
│  - Интерфейсы периферии                     │
├─────────────────────────────────────────────┤
│              ESP32 Hardware                 │
│  (CPU, GPIO, ADC, Timers, Memory)          │
└─────────────────────────────────────────────┘
```

## Работа с периферией ESP32

### 1. GPIO (General Purpose Input/Output)

#### Концепция
GPIO — это универсальные контакты ввода-вывода микроконтроллера. В nanoFramework работа с GPIO абстрагирована через класс `GpioController`.

#### Пример из проекта:
```csharp
GpioController gpioController = new GpioController();
CustomOutputGpio relayPin = new CustomOutputGpio(gpioController, 16);
relayPin.WritePin(PinValue.High);  // Установить HIGH (3.3V)
relayPin.WritePin(PinValue.Low);   // Установить LOW (0V)
```

#### Как это работает внутри:
```
1. GpioController инициализирует контроллер GPIO ESP32
2. OpenPin(16, PinMode.Output) настраивает GPIO16 как выход
3. WritePin() вызывает HAL функцию для установки напряжения
4. ESP32 устанавливает соответствующий регистр GPIO
```

#### Технические детали ESP32 GPIO:
- **Напряжение:** 3.3V (не 5V tolerant!)
- **Максимальный ток на pin:** 40mA (рекомендуется ≤20mA)
- **Встроенные pull-up/pull-down:** 45kΩ
- **Частота переключения:** До нескольких MHz

### 2. ADC (Analog-to-Digital Converter)

#### Концепция
ADC преобразует аналоговое напряжение (0-3.3V) в цифровое значение (0-4095 для 12-bit).

#### Пример из проекта:
```csharp
AdcController adcController = new AdcController();
AdcChannel channel = adcController.OpenChannel(4);
int value = channel.ReadValue();  // Возвращает 0-4095
```

#### Физический процесс:
```
Кнопка нажата → Делитель напряжения → Напряжение на ADC pin
                                              ↓
                         ESP32 ADC преобразует в число
                                              ↓
                              Возвращает значение 0-4095
```

#### Расчет напряжения:
```csharp
// Для 12-bit ADC:
float voltage = (adcValue / 4095.0f) * 3.3f;

// Примеры из проекта:
// adcValue = 2000 → ~1.61V
// adcValue = 2700 → ~2.17V
// adcValue = 3350 → ~2.69V
```

#### Особенности ESP32 ADC:
- **Разрядность:** 12-bit (0-4095)
- **Напряжение:** 0-3.3V (с ослаблением до 11dB можно измерять до 3.9V)
- **Точность:** ±2% (нелинейность)
- **Каналы:** ADC1 (8 каналов), ADC2 (10 каналов)
- **Важно:** ADC2 не работает когда включен WiFi!

### 3. Таймеры высокого разрешения

#### HighResTimer в nanoFramework
Использует аппаратные таймеры ESP32 для точных временных интервалов.

```csharp
HighResTimer timer = new HighResTimer();
timer.OnHighResTimerExpired += (sender, args) => {
    // Этот код выполнится через заданное время
    Console.WriteLine("Timer expired!");
};

// Запуск таймера на 1 секунду (1,000,000 микросекунд)
timer.StartOnePeriodic(1_000_000);
```

#### Как работают аппаратные таймеры ESP32:

```
┌──────────────────────────────────────────┐
│  CPU устанавливает значение в регистр    │
│  таймера: TIMER_LOAD_VALUE               │
├──────────────────────────────────────────┤
│  Таймер считает с частотой 80MHz         │
│  (каждый тик = 12.5 наносекунд)          │
├──────────────────────────────────────────┤
│  Когда счетчик достигает 0:              │
│  - Генерируется прерывание (interrupt)   │
│  - Вызывается обработчик события         │
└──────────────────────────────────────────┘
```

#### Точность таймеров:
- **Разрешение:** Микросекунды (1μs = 0.000001 секунды)
- **Максимальное время:** ~71 минут (2^32 микросекунд)
- **Джиттер:** <1μs при высоком приоритете прерывания

#### Преимущества аппаратных таймеров:
1. **Независимость:** Работают параллельно с основным кодом
2. **Точность:** Не зависят от загрузки CPU
3. **Энергоэффективность:** CPU может спать между событиями
4. **Множественность:** ESP32 имеет 4 аппаратных таймера

## Управление памятью

### Stack vs Heap в nanoFramework

```
FLASH (Программная память):
├── Compiled IL Code
├── nanoFramework Runtime
└── Константы и статические данные

RAM (Оперативная память):
├── Stack (Стек)
│   ├── Локальные переменные
│   ├── Параметры функций
│   └── Адреса возврата
│
└── Heap (Куча)
    ├── Объекты классов (new)
    ├── Массивы
    └── Строки
```

### Garbage Collection (Сборка мусора)

#### Когда запускается GC:
1. **Heap заполнен:** Недостаточно места для нового объекта
2. **Периодически:** По таймеру (если настроено)
3. **Явно:** `nanoFramework.Runtime.Native.GC.Run(true)`

#### Процесс GC:
```
1. [Mark Phase] Пометка всех достижимых объектов
   - Начинаем с корневых ссылок (static, stack)
   - Рекурсивно помечаем все связанные объекты

2. [Sweep Phase] Удаление непомеченных объектов
   - Все непомеченные объекты = мусор
   - Освобождаем занятую ими память

3. [Compact Phase] Уплотнение памяти (опционально)
   - Перемещаем объекты чтобы устранить фрагментацию
```

### Оптимизация памяти в проекте

#### Хорошие практики:
```csharp
// ✅ ХОРОШО: Переиспользование объектов
private Timer _timer;  // Создаем один раз
_timer = new Timer(_OnScanButtons, null, 0, 250);

// ❌ ПЛОХО: Создание объектов в цикле
while(true) {
    Timer t = new Timer(...);  // Новый объект каждую итерацию!
    Thread.Sleep(1000);
}

// ✅ ХОРОШО: Использование примитивов
int value = _cnl.ReadValue();

// ❌ ПЛОХО: Излишний boxing
object value = _cnl.ReadValue();  // int → object (boxing)

// ✅ ХОРОШО: Массивы фиксированного размера
private readonly AnalogButton[][] _buttonsSet = new AnalogButton[2][];

// ❌ ПЛОХО: Динамические коллекции (если не нужны)
private List<AnalogButton> buttons = new List<AnalogButton>();
```

## Threading и многозадачность

### Модель потоков в nanoFramework

```
Main Thread:
├── Program.Main() запускается
├── Инициализация компонентов
├── Thread.Sleep(Timeout.Infinite)  ← Засыпает навсегда
└── [Блокирован]

Timer Threads (в пуле потоков):
├── AdcService._timer каждые 250ms
├── LED blink timers каждые 500ms
└── HeatService timers (динамические)

Interrupt Service Routines (ISR):
└── Hardware timer interrupts
```

### Синхронизация в проекте

#### Event-based архитектура:
```csharp
// Publisher (издатель)
public EventHandler HeatingActionEventHandler;

// Где-то в коде:
HeatingActionEventHandler?.Invoke(this, new HeatingActionEventArgs(status));

// Subscriber (подписчик)
heatService.HeatingActionEventHandler += (sender, args) => {
    // Обработка события
    HeatingStatus status = ((HeatingActionEventArgs)args).Status;
    // ...
};
```

#### Преимущества event-driven подхода:
1. **Слабая связанность:** Компоненты не знают друг о друге напрямую
2. **Расширяемость:** Легко добавить новых подписчиков
3. **Асинхронность:** События обрабатываются в разных потоках

### Потенциальные проблемы многопоточности

#### Race Condition пример:
```csharp
// ❌ ПРОБЛЕМА: Два потока изменяют lastDelayValue
// Thread 1: AdcService timer
lastDelayValue = timingService.Program1Us;

// Thread 2: Другое событие кнопки (одновременно)
lastDelayValue = timingService.Program2Us;

// Решение в текущем проекте:
// ✅ Кнопки сканируются последовательно (один поток)
// ✅ События обрабатываются синхронно
```

#### Когда нужна синхронизация:
```csharp
// Если бы было несколько потоков:
private readonly object _lock = new object();

void UpdateValue(ulong value) {
    lock(_lock) {
        lastDelayValue = value;
    }
}
```

## Обработка прерываний (Interrupts)

### Концепция прерываний

```
Normal Program Flow:
[Instruction 1] → [Instruction 2] → [Instruction 3] → ...
                        ↓
                  [INTERRUPT!]
                        ↓
                  [ISR executed]
                        ↓
                  [Resume at Instruction 3]
```

### GPIO Interrupts (в проекте не используются)

```csharp
// Пример: Реагирование на нажатие кнопки через interrupt
GpioPin button = gpioController.OpenPin(21, PinMode.InputPullUp);
button.ValueChanged += (sender, args) => {
    if (args.Edge == PinEventTypes.Falling) {
        Console.WriteLine("Button pressed!");
    }
};
```

### Почему в проекте используется polling вместо interrupts?

#### Polling подход (текущий):
```csharp
// Каждые 250ms читаем ADC
_timer = new Timer(_OnScanButtons, null, 0, 250);
```

**Преимущества:**
- ✅ Простота реализации
- ✅ Нет проблем с дребезгом контактов
- ✅ Предсказуемая нагрузка на CPU
- ✅ Легко отлаживать

**Недостатки:**
- ❌ Задержка до 250ms в обнаружении нажатия
- ❌ Постоянное потребление энергии

#### Interrupt подход (альтернатива):
**Преимущества:**
- ✅ Мгновенная реакция
- ✅ Энергоэффективность (CPU спит)

**Недостатки:**
- ❌ Требует debouncing логики
- ❌ Сложнее в отладке
- ❌ Возможные race conditions

## Энергопотребление и оптимизация

### Режимы энергопотребления ESP32

```
┌──────────────────────────────────────────────────────┐
│ Active Mode (все включено)                           │
│ Потребление: ~160-260mA                              │
│ - CPU работает на полной частоте (240MHz)            │
│ - Все периферия активна                              │
│ - WiFi/Bluetooth активны (если используются)         │
├──────────────────────────────────────────────────────┤
│ Modem Sleep (CPU активен, WiFi выключен)             │
│ Потребление: ~20-70mA                                │
│ - Используется в текущем проекте                     │
├──────────────────────────────────────────────────────┤
│ Light Sleep (CPU останавливается)                    │
│ Потребление: ~0.8mA                                  │
│ - Можно использовать с таймерами                     │
├──────────────────────────────────────────────────────┤
│ Deep Sleep (почти все выключено)                     │
│ Потребление: ~10μA                                   │
│ - Только RTC активен                                 │
│ - Требует перезагрузки для пробуждения               │
└──────────────────────────────────────────────────────┘
```

### Оптимизация в текущем проекте

```csharp
// Основной поток спит бесконечно
Thread.Sleep(Timeout.Infinite);

// Вся работа выполняется таймерами:
// - AdcService: 250ms интервал (минимальная активность)
// - LED timers: 500ms (только когда нужно мигать)
// - Heat timers: минуты/часы (очень редко)
```

## Отладка и мониторинг

### Console.WriteLine в embedded системах

```csharp
Console.WriteLine("Status: " + status.ToString());
```

#### Куда выводится текст:
```
ESP32 UART → USB-to-Serial → COM Port → Visual Studio Output
```

#### Настройка в Visual Studio:
1. View → Other Windows → Device Explorer
2. Подключение к COM порту
3. Просмотр вывода в реальном времени

### Отладка с breakpoints

```csharp
// Можно ставить breakpoints прямо в VS:
private void _OnHeating(HighResTimer sender, object e)
{
    _heatingTimer.Stop();  // ← Breakpoint здесь
    _coolingTimer.StartOnePeriodic(_coolingUs);
    // Inspect variables, step through code
}
```

### Debug практики для данного проекта

```csharp
// ✅ Логирование состояний
Console.WriteLine("Status: " + status.ToString());

// ✅ Логирование ADC значений
if (v > 100) Console.WriteLine(v.ToString());

// ✅ Логирование нажатий кнопок
Console.WriteLine("The button " + btn.Type.ToString() + " was pressed");

// ✅ Критические события
Console.WriteLine("On reset");
```

## Ограничения nanoFramework

### Что ЕСТЬ:
- ✅ Базовые типы данных (int, float, string, etc.)
- ✅ Классы, интерфейсы, наследование
- ✅ Events и delegates
- ✅ LINQ (ограниченный)
- ✅ Threading (базовый)
- ✅ GPIO, ADC, PWM, I2C, SPI, UART

### Чего НЕТ:
- ❌ Полный .NET Framework (только подмножество)
- ❌ Async/await (в большинстве случаев)
- ❌ Reflection (ограниченный)
- ❌ Entity Framework, ASP.NET
- ❌ Многие NuGet пакеты
- ❌ File System (зависит от платформы)

## Сравнение с альтернативами

### nanoFramework vs Arduino (C++)

```
Arduino:
void loop() {
    if (digitalRead(BUTTON_PIN) == LOW) {
        digitalWrite(LED_PIN, HIGH);
    }
}

nanoFramework:
button.ValueChanged += (s, e) => {
    led.Write(PinValue.High);
};
```

| Характеристика | Arduino | nanoFramework |
|----------------|---------|---------------|
| Язык | C/C++ | C# |
| Управление памятью | Ручное | Автоматическое (GC) |
| ООП | Ограниченное | Полное |
| Отладка | Сложная | Visual Studio |
| Производительность | Выше | Ниже |
| Скорость разработки | Медленнее | Быстрее |

### nanoFramework vs MicroPython

| Характеристика | MicroPython | nanoFramework |
|----------------|-------------|---------------|
| Язык | Python | C# |
| Производительность | Низкая | Средняя |
| Память | Больше | Меньше |
| Типизация | Динамическая | Статическая |
| IDE | Любой редактор | Visual Studio |
| Экосистема | Большая | Растущая |

## Практические рекомендации

### 1. Структура проекта

```
✅ ХОРОШАЯ структура (как в проекте):
- Разделение на логические классы
- Сервисы с единственной ответственностью
- Event-based коммуникация
- Абстракции (ITiming, ITimingService)

❌ ПЛОХАЯ структура:
- Весь код в Main()
- Глобальные переменные
- Tight coupling между компонентами
```

### 2. Управление ресурсами

```csharp
// ✅ Создавайте ресурсы один раз
private readonly HighResTimer _timer = new HighResTimer();

// ✅ Освобождайте ресурсы
public void Dispose() {
    _coolingTimer.Dispose();
    _heatingTimer.Dispose();
}

// ❌ Не забывайте про утечки
// Если создаете Timer, но не вызываете Dispose()
```

### 3. Обработка ошибок

```csharp
// ✅ Добавьте try-catch для критических операций
try {
    AdcChannel channel = adcController.OpenChannel(4);
} catch (Exception ex) {
    Console.WriteLine("Failed to open ADC: " + ex.Message);
    // Fallback или restart
}
```

### 4. Тестирование

```csharp
// ✅ Используйте DevelopStage для быстрого тестирования
ITiming.BuildOn(DevelopStage.Develop);  // 30s вместо 8 часов!

// ✅ Добавьте диагностический вывод
Console.WriteLine("ADC Value: " + value);

// ✅ Тестируйте граничные условия
// - Что если две кнопки нажаты одновременно?
// - Что если кнопка нажата во время heating?
```

## Заключение

nanoFramework предоставляет мощный и удобный способ разработки embedded систем для разработчиков .NET. Проект демонстрирует:

1. **Правильную архитектуру:** Разделение ответственности, event-driven дизайн
2. **Эффективное использование ресурсов:** Таймеры, минимальное потребление памяти
3. **Профессиональный подход:** Абстракции, конфигурируемость (Dev/Prod режимы)
4. **Практичность:** Реальная задача управления нагревом с обратной связью через LEDs

Понимание принципов работы nanoFramework позволяет создавать надежные, производительные и легко поддерживаемые embedded решения на знакомом языке C#.
