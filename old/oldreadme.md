# 📘 C# Cheatsheet – System.IO, Strumienie, Archiwa i Eventy

---

## 📁 1. System.IO: Operacje na Katalogach (`Directory`)

Statyczna klasa do operacji na folderach.  
Wymaga:  
```csharp
using System.IO;
```

```csharp
// Sprawdzenie, czy katalog istnieje
if (Directory.Exists(@"C:\Temp\MojeZdjecia")) { ... }

// Stworzenie katalogu (bezpieczne – nie rzuca błędu, jeśli istnieje)
Directory.CreateDirectory(@"C:\Posortowane\2023\05");

// Usunięcie katalogu (true = rekursywnie)
Directory.Delete(@"C:\Temp\DoUsuniecia", true);

// Pobranie wszystkich plików (wydajniej niż GetFiles)
foreach (string sciezkaPliku in Directory.EnumerateFiles(@"C:\Zrodlo", "*.jpg"))
{
    Console.WriteLine(sciezkaPliku);
}

// Pobranie plików rekurencyjnie (ze wszystkich podfolderów)
var opcje = SearchOption.AllDirectories;
foreach (string sciezkaPliku in Directory.EnumerateFiles(@"C:\Zrodlo", "*.jpg", opcje))
{
    // ...
}

// Pobranie podkatalogów
foreach (string sciezkaKatalogu in Directory.EnumerateDirectories(@"C:\Zrodlo"))
{
    // ...
}
```

---

## 📄 2. System.IO: Operacje na Plikach (`File`)

Statyczna klasa do operacji na plikach.  
Wymaga:  
```csharp
using System.IO;
```

```csharp
// Sprawdzenie, czy plik istnieje
if (File.Exists(@"C:\Temp\plik.txt")) { ... }

// Kopiowanie pliku (true = nadpisz, jeśli istnieje)
File.Copy(@"C:\Temp\plik.txt", @"C:\Cel\plik_kopia.txt", true);

// Przenoszenie (również zmiana nazwy)
File.Move(@"C:\Temp\plik.txt", @"C:\Cel\nowy_plik.txt");

// Usunięcie
File.Delete(@"C:\Cel\nowy_plik.txt");

// Odczytanie całego tekstu (dla małych plików)
string zawartosc = File.ReadAllText(@"C:\config.json");

// Zapisanie całego tekstu (nadpisuje plik)
File.WriteAllText(@"C:\log.txt", "Ważna informacja");

// Dopisywanie do istniejącego pliku
File.AppendAllText(@"C:\log.txt", "Kolejna linijka\n");

// Pobranie metadanych
DateTime dataUtworzenia = File.GetCreationTime(@"C:\zdjecie.jpg");
DateTime dataModyfikacji = File.GetLastWriteTime(@"C:\zdjecie.jpg");
```

---

## 🗺️ 3. System.IO: Operacje na Ścieżkach (`Path`)

Nigdy nie łącz ścieżek operatorem `+`!

```csharp
// Poprawne łączenie ścieżek
string sciezkaDocelowa = Path.Combine(@"C:\Posortowane\2023", "05", "15");
// => "C:\Posortowane\2023\05\15"

// Pobranie nazwy pliku
string nazwa = Path.GetFileName(@"C:\Temp\wakacje.jpg"); // wakacje.jpg

// Nazwa bez rozszerzenia
string nazwaBezRoz = Path.GetFileNameWithoutExtension(@"C:\Temp\wakacje.jpg"); // wakacje

// Rozszerzenie
string rozszerzenie = Path.GetExtension(@"C:\Temp\wakacje.jpg"); // .jpg

// Katalog nadrzędny
string katalog = Path.GetDirectoryName(@"C:\Temp\wakacje.jpg"); // C:\Temp

// Folder tymczasowy systemu
string tempFolder = Path.GetTempPath(); // np. C:\Users\Wiktor\AppData\Local\Temp\
```

---

## 🌊 4. Strumienie (`Stream` i pochodne)

Do odczytu/zapisu dużych plików.  
Zawsze używaj `using`!

```csharp
// --- FileStream ---
// Zapis bajtów
using (FileStream fs = new FileStream(@"C:\plik.bin", FileMode.Create, FileAccess.Write))
{
    byte[] bufor = { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
    fs.Write(bufor, 0, bufor.Length);
}

// Odczyt bajtów
using (FileStream fs = new FileStream(@"C:\plik.bin", FileMode.Open, FileAccess.Read))
{
    byte[] bufor = new byte[1024];
    int przeczytaneBity = fs.Read(bufor, 0, bufor.Length);
}
```

```csharp
// --- StreamReader / StreamWriter ---
// Zapis tekstu
using (StreamWriter sw = new StreamWriter(@"C:\log.txt", append: true, Encoding.UTF8))
{
    sw.WriteLine("Log start");
    sw.WriteLine($"Czas: {DateTime.Now}");
}

// Odczyt tekstu
using (StreamReader sr = new StreamReader(@"C:\log.txt", Encoding.UTF8))
{
    string linijka;
    while ((linijka = sr.ReadLine()) != null)
    {
        Console.WriteLine(linijka);
    }
}
```

---

## 📦 5. Archiwa ZIP (`ZipFile`, `ZipArchive`)

Wymaga:  
```csharp
using System.IO.Compression;
```

### 🔹 Sposób 1: Prosty (`ZipFile`)

```csharp
string plikZip = @"C:\archiwum.zip";
string folderDoWypakowania = @"C:\Temp\Rozpakowane";
string folderDoSpakowania = @"C:\MojePliki";

// Wypakowanie całego archiwum
ZipFile.ExtractToDirectory(plikZip, folderDoWypakowania);

// Spakowanie folderu
ZipFile.CreateFromDirectory(folderDoSpakowania, plikZip);
```

### 🔹 Sposób 2: Zaawansowany (`ZipArchive`)

```csharp
using (ZipArchive archiwum = ZipFile.OpenRead(plikZip))
{
    foreach (ZipArchiveEntry wpis in archiwum.Entries)
    {
        Console.WriteLine($"Plik w archiwum: {wpis.FullName}");

        // Wypakowanie konkretnego pliku
        if (wpis.Name == "szukany_plik.txt")
        {
            string sciezkaDocelowa = Path.Combine(folderDoWypakowania, wpis.Name);
            wpis.ExtractToFile(sciezkaDocelowa, true);
        }

        // Odczyt bez wypakowywania
        using (Stream s = wpis.Open())
        using (StreamReader sr = new StreamReader(s))
        {
            string zawartosc = sr.ReadToEnd();
        }
    }
}
```

---

## ⚡ 6. Eventy (Zdarzenia)

Mechanizm powiadamiania o zmianach – wzorzec **Obserwator**.

### 🔹 A. Koncepcja

```csharp
// --- Krok 1: Definicja EventArgs ---
public class MojeEventArgs : EventArgs
{
    public string Wiadomosc { get; set; }
}

// --- Krok 2: Wydawca (emituje event) ---
public class Wydawca
{
    public delegate void MojEventHandler(object sender, MojeEventArgs e);
    public event MojEventHandler CosSieStalo;

    public void ZrobCos()
    {
        Console.WriteLine("Robota zrobiona, powiadamiam subskrybentów...");
        CosSieStalo?.Invoke(this, new MojeEventArgs { Wiadomosc = "Zadanie ukończone" });
    }
}

// --- Krok 3: Subskrybent (reaguje) ---
public class Subskrybent
{
    public void Podlacz(Wydawca w)
    {
        w.CosSieStalo += ObslugaZdarzenia;
    }

    private void ObslugaZdarzenia(object sender, MojeEventArgs e)
    {
        Console.WriteLine($"Otrzymałem event od {sender} z wiadomością: {e.Wiadomosc}");
    }
}

// --- Użycie ---
Wydawca w = new Wydawca();
Subskrybent s = new Subskrybent();
s.Podlacz(w);
w.ZrobCos();
```

---

### 🔹 B. Praktyczny przykład: `FileSystemWatcher`

```csharp
public class MonitorFolderu
{
    private FileSystemWatcher watcher;

    public void Start(string sciezkaDoMonitorowania)
    {
        watcher = new FileSystemWatcher(sciezkaDoMonitorowania);

        watcher.NotifyFilter = NotifyFilters.FileName 
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite;

        watcher.Filter = "*.jpg"; // lub "" dla wszystkich
        watcher.IncludeSubdirectories = true;

        watcher.Created += OnCreated;
        watcher.Renamed += OnRenamed;
        watcher.Deleted += OnDeleted;

        watcher.EnableRaisingEvents = true; 

        Console.WriteLine($"Nasłuchuję zmian w: {sciezkaDoMonitorowania}...");
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"NOWY PLIK: {e.FullPath}");
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine($"ZMIANA NAZWY: {e.OldFullPath} -> {e.FullPath}");
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"USUNIĘTO: {e.FullPath}");
    }
}

// --- Użycie ---
// MonitorFolderu monitor = new MonitorFolderu();
// monitor.Start(@"C:\MojeZdjecia");
// Console.ReadLine(); // zapobiega zakończeniu programu
```

---

💡 **Tip:**  
Do większych projektów używaj `async/await` w handlerach eventów, aby uniknąć blokowania wątków I/O.

---

> © 2025 C# Cheatsheet – System.IO, Streams, ZIP, Events  
> Przydatne do nauki i powtórek 👨‍💻
