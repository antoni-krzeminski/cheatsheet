# 📘 C# Reflection & Attributes - Kompendium do Kolokwium

> **Cel:** Szybka powtórka i gotowe fragmenty kodu (snippety) do zadań z Refleksji i Atrybutów.  
> **Na podstawie:** Wykłady Mini PW (Assembly, Reflection, Attributes).

---

## 📑 Spis Treści

1. [Słownik pojęć i Tagi (Cmd+F)](#-słownik-i-tagi-wyszukiwania)
2. [1. Tworzenie własnych atrybutów](#1-tworzenie-własnych-atrybutów)
3. [2. Pobieranie typów oznaczonych atrybutem](#2-pobieranie-z-assembly-typów-oznaczonych-atrybutem)
4. [3. Sprawdzanie dziedziczenia](#3-pobieranie-typów-dziedziczących-po-klasie-abstrakcyjnej)
5. [4. Pobieranie właściwości z atrybutem](#4-pobieranie-właściwości-oznaczonych-atrybutem)
6. [5. Interfejsy generyczne (Trudne!)](#5-czy-typ-implementuje-generyczny-interfejs)
7. [6. Ustawianie wartości (SetValue)](#6-ustawianie-wartości-na-instancji-setvalue)
8. [🚀 KOMPLETNY PRZYKŁAD (Zadanie Egzaminacyjne)](#-kompletny-przykład-logiki-exam-ready)

---

## 🔍 Słownik i Tagi Wyszukiwania
*Użyj `Ctrl+F` lub `Cmd+F` i wpisz poniższe frazy, aby szybko znaleźć kod:*

* `#CreateAttribute` - jak zrobić nową klasę atrybutu.
* `#GetTypes` - pobieranie klas z dll/exe.
* `#FilterAttribute` - szukanie klas/metod z konkretnym atrybutem.
* `#Inheritance` - sprawdzanie `IsSubclassOf` (dziedziczenie).
* `#GetProperties` - wyciąganie `PropertyInfo` z typu.
* `#GenericInterface` - walka z `typeof(IList<>)` (otwarte typy).
* `#SetValue` - dynamiczna zmiana wartości w obiekcie.
* `#Activator` - tworzenie instancji obiektu z typu (`Type`).

---

## 1. Tworzenie własnych atrybutów
`#CreateAttribute` `#AttributeUsage`

Atrybut to po prostu klasa dziedzicząca po `System.Attribute`. Kluczowe jest dodanie atrybutu `[AttributeUsage]`, aby określić, gdzie można go używać (np. tylko na klasach lub tylko na właściwościach).

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] // Gdzie można użyć?
public class MyCustomAttribute : Attribute // Musi dziedziczyć po Attribute
{
    public string Description { get; }

    // Konstruktor przekazuje dane do metadanych
    public MyCustomAttribute(string description) 
    {
        Description = description;
    }
}
```

---

## 2. Pobieranie z Assembly typów oznaczonych atrybutem
`#GetTypes` `#FilterAttribute` `#GetCustomAttribute`

Aby znaleźć klasy z atrybutem, musisz:
1. Pobrać Assembly (np. `GetExecutingAssembly()`).
2. Pobrać tablicę wszystkich typów (`GetTypes()`).
3. Przefiltrować je LINQ-iem (`Where`).

```csharp
using System.Reflection;
using System.Linq;

Assembly assembly = Assembly.GetExecutingAssembly();

var typesWithAttribute = assembly.GetTypes()
    .Where(t => t.GetCustomAttribute<MyCustomAttribute>() != null)
    .ToList();
```

---

## 3. Pobieranie typów dziedziczących po klasie abstrakcyjnej
`#Inheritance` `#IsSubclassOf` `#IsAbstract`

Sprawdzamy relację rodzic-dziecko. Często na teście trzeba odrzucić samą klasę abstrakcyjną (używając `!t.IsAbstract`).

```csharp
public abstract class BaseClass { }

var childClasses = assembly.GetTypes()
    .Where(t => t.IsSubclassOf(typeof(BaseClass)) && !t.IsAbstract) // Tylko konkretne klasy
    .ToList();
```
> **Ważne:** `IsSubclassOf` sprawdza tylko dziedziczenie klas. Do interfejsów używa się innego sposobu (patrz pkt 5).

---

## 4. Pobieranie właściwości oznaczonych atrybutem
`#GetProperties` `#PropertyInfo`

Działamy na konkretnym obiekcie `Type`, a nie na całym Assembly.

```csharp
Type myType = typeof(SomeClass); // lub typ znaleziony w pkt 2

var markedProperties = myType.GetProperties()
    .Where(p => p.GetCustomAttribute<MyCustomAttribute>() != null);
    
foreach (PropertyInfo prop in markedProperties)
{
    Console.WriteLine($"Znalazłem property: {prop.Name}");
}
```

---

## 5. Czy typ implementuje generyczny interfejs?
`#GenericInterface` `#GetInterfaces` `#GetGenericTypeDefinition`

To najtrudniejszy punkt. `IsAssignableFrom` nie działa łatwo dla "otwartych typów generycznych" (np. `IRepository<>` bez podania typu w środku).

**Algorytm:**
1. Pobierz wszystkie interfejsy typu.
2. Sprawdź, czy interfejs jest generyczny (`IsGenericType`).
3. Sprawdź jego definicję (`GetGenericTypeDefinition`) i porównaj z poszukiwanym typem otwartym.

```csharp
// Szukamy np. IHandler<>
Type openGenericInterface = typeof(IHandler<>); 

bool isImplemented = myType.GetInterfaces().Any(i => 
    i.IsGenericType && 
    i.GetGenericTypeDefinition() == openGenericInterface
);
```

---

## 6. Ustawianie wartości na instancji (.SetValue)
`#SetValue` `#Activator` `#Instance`

Refleksja operuje na metadanych (`PropertyInfo`), ale żeby zmienić wartość, potrzebujesz żywego obiektu (instancji).

**Kroki:**
1. Mamy `PropertyInfo` (z pkt 4).
2. Musimy mieć instancję obiektu (stworzoną `new` lub `Activator.CreateInstance`).
3. Wywołujemy `SetValue(instancja, nowaWartosc)`.

```csharp
Type type = typeof(User);
object instance = Activator.CreateInstance(type); // Tworzymy obiekt dynamicznie
PropertyInfo prop = type.GetProperty("Age"); // Szukamy właściwości

// Odpowiednik: instance.Age = 25;
prop.SetValue(instance, 25); 
```

---

## 🚀 KOMPLETNY PRZYKŁAD (Logika Exam-Ready)

Poniżej funkcja, którą możesz dostosować na teście. Łączy szukanie klasy, tworzenie jej i modyfikację właściwości.

```csharp
public void RunReflectionTask()
{
    var assembly = Assembly.GetExecutingAssembly();

    // 1. Szukamy odpowiednich typów (Klasa + Atrybut + Interfejs)
    var targetTypes = assembly.GetTypes().Where(t => 
        t.GetCustomAttribute<MyPluginAttribute>() != null && // Pkt 2
        t.IsSubclassOf(typeof(BasePlugin)) &&                // Pkt 3
        !t.IsAbstract
    );

    foreach (var type in targetTypes)
    {
        // 2. Tworzymy instancję znalezionego typu
        object instance = Activator.CreateInstance(type);

        // 3. Szukamy właściwości z atrybutem DefaultValue
        var props = type.GetProperties()
            .Where(p => p.GetCustomAttribute<DefaultValueAttribute>() != null); // Pkt 4

        foreach (var prop in props)
        {
            // 4. Pobieramy wartość z atrybutu
            var attr = prop.GetCustomAttribute<DefaultValueAttribute>();
            var valueToSet = attr.Value;

            // 5. Ustawiamy wartość w instancji (Pkt 6)
            prop.SetValue(instance, valueToSet);
        }
    }
}
```

---
### 💡 Protipy na test:
* Pamiętaj o `using System.Reflection;` i `using System.Linq;`.
* Rozróżniaj `GetProperty` (jedna, po nazwie) od `GetProperties` (wszystkie).
* Gdy używasz `SetValue`, upewnij się, że typ wartości pasuje do typu właściwości (np. nie wpisuj `string` do `int`).