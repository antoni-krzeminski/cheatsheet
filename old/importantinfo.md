# 📘 C# Reflection, Attributes & Assembly - Ultimate Guide

> **Cel:** Kompletne notatki do kolokwium/egzaminu obejmujące zagadnienia ze slajdu oraz materiałów wykładowych (Mini PW).
> **Zakres:** Atrybuty, Refleksja, Assembly, Zasoby (Resources), Dynamiczne ładowanie.

---

## 📑 Spis Treści

1. [Słownik pojęć i Tagi (Cmd+F)](#-słownik-i-tagi-wyszukiwania)
2. [1. Tworzenie i konfiguracja atrybutów](#1-tworzenie-i-konfiguracja-atrybutów)
3. [2. Skanowanie Assembly (Szukanie klas)](#2-skanowanie-assembly-szukanie-klas)
4. [3. Sprawdzanie dziedziczenia](#3-sprawdzanie-dziedziczenia-issubclassof)
5. [4. Właściwości i Pola (Odczyt/Zapis)](#4-właściwości-i-pola-setvalue--getvalue)
6. [5. Ukryta wiedza: Prywatne pola (BindingFlags)](#5-ukryta-wiedza-prywatne-pola-bindingflags)
7. [6. Metody: Wywoływanie dynamiczne (Invoke)](#6-metody-wywoływanie-dynamiczne-invoke)
8. [7. Interfejsy i Typy Generyczne (Hard Mode)](#7-interfejsy-i-typy-generyczne-hard-mode)
9. [8. Konstruktory i tworzenie obiektów](#8-konstruktory-i-tworzenie-obiektów)
10. [9. Zasoby: Czytanie plików z DLL](#9-zasoby-czytanie-plików-z-dll)
11. [🚀 ULTRA PRZYKŁAD (Wszystko w jednym)](#-ultra-przykład-wszystko-w-jednym)

---

## 🔍 Słownik i Tagi Wyszukiwania
*Użyj `Ctrl+F` lub `Cmd+F` i wpisz tag:*

* `#AttributeUsage` - flagi `AllowMultiple`, `Inherited`.
* `#GetTypes` - pobieranie typów z Assembly.
* `#BindingFlags` - klucz do prywatnych metod/pól.
* `#Invoke` - uruchamianie metody przez refleksję.
* `#MakeGenericType` - tworzenie `List<int>` dynamicznie.
* `#GetManifestResourceStream` - odczyt pliku tekstowego wbudowanego w exe.
* `#Activator` - tworzenie instancji.

---

## 1. Tworzenie i konfiguracja atrybutów
`#CreateAttribute` `#AttributeUsage` `#AllowMultiple`

Z wykładu warto pamiętać o `AttributeUsage`. Definiuje on, czy atrybut można nakładać wiele razy na ten sam element i czy dziedziczy się na klasy pochodne.

```csharp
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method, // Gdzie można użyć
    AllowMultiple = true,  // Czy można dać atrybut 2 razy na to samo?
    Inherited = false      // Czy klasa dziedzicząca też ma ten atrybut?
)]
public class AuthorAttribute : Attribute
{
    public string Name { get; }
    public AuthorAttribute(string name) => Name = name;
}
```

---

## 2. Skanowanie Assembly (Szukanie klas)
`#GetTypes` `#GetExecutingAssembly` `#GetEntryAssembly`

Podstawowa operacja. Często na egzaminie trzeba przeszukać nie tylko `ExecutingAssembly`, ale wszystkie załadowane.

```csharp
// 1. To, w którym aktualnie jest ten kod
var asm = Assembly.GetExecutingAssembly();

// 2. To, które uruchomiło proces (plik .exe)
var entryAsm = Assembly.GetEntryAssembly();

// Szukanie typów z atrybutem
var types = asm.GetTypes()
    .Where(t => t.GetCustomAttribute<AuthorAttribute>() != null);
```

---

## 3. Sprawdzanie dziedziczenia (IsSubclassOf)
`#Inheritance` `#IsAbstract` `#IsInterface`

```csharp
public abstract class PluginBase { }

var plugins = asm.GetTypes()
    .Where(t => 
        t.IsSubclassOf(typeof(PluginBase)) && // Dziedziczy po klasie
        !t.IsAbstract &&                      // Jest konkretną klasą
        t.IsClass                             // Nie jest interfejsem/structem
    );
```

---

## 4. Właściwości i Pola (SetValue / GetValue)
`#GetProperty` `#SetValue` `#GetValue`

Pamiętaj: `SetValue` wymaga **instancji** obiektu, chyba że właściwość jest `static` (wtedy podajesz `null`).

```csharp
object instance = Activator.CreateInstance(typeof(User));
PropertyInfo prop = typeof(User).GetProperty("Name");

// Zapis
prop.SetValue(instance, "Jan Kowalski");

// Odczyt
var value = prop.GetValue(instance); // zwraca object, trzeba rzutować
```

---

## 5. Ukryta wiedza: Prywatne pola (BindingFlags)
`#BindingFlags` `#Private` `#NonPublic`

To częsty "haczyk" z wykładów. Domyślnie `GetProperties()` czy `GetFields()` zwraca tylko publiczne rzeczy. Aby dostać się do prywatnych, musisz użyć `BindingFlags`.

```csharp
Type type = typeof(SecretService);

// Szukamy pola prywatnego (np. private string _password;)
FieldInfo secretField = type.GetField("_password", 
    BindingFlags.NonPublic | BindingFlags.Instance); // Magiczne flagi

string secret = (string)secretField.GetValue(instance);
```

---

## 6. Metody: Wywoływanie dynamiczne (Invoke)
`#GetMethod` `#Invoke` `#Parameters`

Nie tylko właściwości! Refleksja pozwala uruchamiać funkcje.

```csharp
Type type = typeof(Calculator);
MethodInfo method = type.GetMethod("Add"); // Zakładamy public int Add(int a, int b)

object instance = Activator.CreateInstance(type);

// Invoke przyjmuje: (instancja, tablica argumentów)
object result = method.Invoke(instance, new object[] { 10, 20 });

Console.WriteLine((int)result); // 30
```

---

## 7. Interfejsy i Typy Generyczne (Hard Mode)
`#GenericInterface` `#MakeGenericType` `#GetGenericTypeDefinition`

**Scenariusz A: Sprawdzenie czy implementuje interfejs `IList<>`**
```csharp
bool isList = type.GetInterfaces().Any(i => 
    i.IsGenericType && 
    i.GetGenericTypeDefinition() == typeof(IList<>)
);
```

**Scenariusz B: Tworzenie instancji `List<int>` dynamicznie**
Z wykładu: Masz typ `List<>` (otwarty) i chcesz stworzyć `List<int>` (zamknięty).

```csharp
Type openType = typeof(List<>);
Type genericType = openType.MakeGenericType(typeof(int)); // Tworzy List<int>

object intList = Activator.CreateInstance(genericType);
```

---

## 8. Konstruktory i tworzenie obiektów
`#ConstructorInfo` `#CreateInstance`

Czasami `Activator.CreateInstance(type)` nie wystarczy, bo konstruktor ma parametry.

```csharp
Type type = typeof(Person);
// Szukamy konstruktora przyjmującego (string, int)
ConstructorInfo ctor = type.GetConstructor(new[] { typeof(string), typeof(int) });

object person = ctor.Invoke(new object[] { "Alice", 30 });
```

---

## 9. Zasoby: Czytanie plików z DLL
`#GetManifestResourceStream` `#EmbeddedResources`

Z wykładu o Assembly: Jak odczytać plik tekstowy wkompilowany w projekt (Build Action: Embedded Resource).

```csharp
Assembly asm = Assembly.GetExecutingAssembly();
// Nazwa zasobu to zazwyczaj: NazwaProjektu.Katalogi.NazwaPliku
string resourceName = "MyProject.Data.config.txt"; 

using (Stream stream = asm.GetManifestResourceStream(resourceName))
using (StreamReader reader = new StreamReader(stream))
{
    string content = reader.ReadToEnd();
    Console.WriteLine(content);
}
```

---

## 🚀 ULTRA PRZYKŁAD (Wszystko w jednym)

Zadanie: Znajdź klasę `Processor`, stwórz ją, ustaw jej **prywatne** pole `_limit` na 100 i wywołaj metodę `Process`.

```csharp
public void RunExamTask()
{
    var asm = Assembly.GetExecutingAssembly();
    
    // 1. Szukamy typu
    var type = asm.GetTypes().FirstOrDefault(t => t.Name == "Processor");
    if(type == null) return;

    // 2. Tworzymy instancję
    object instance = Activator.CreateInstance(type);

    // 3. Ustawiamy PRYWATNE pole (BindingFlags!)
    var field = type.GetField("_limit", BindingFlags.NonPublic | BindingFlags.Instance);
    if (field != null)
    {
        field.SetValue(instance, 100);
    }

    // 4. Wywołujemy metodę z parametrem
    var method = type.GetMethod("Process");
    if (method != null)
    {
        method.Invoke(instance, new object[] { "start_now" });
    }
}
```