/*
==================================================================================
==                            ZADANIE LABORATORYJNE                             ==
==================================================================================

## Laboratorium: Aktywny Monitor Katalogu i Archiwizator

### 🎯 Cel ćwiczenia

Celem jest napisanie aplikacji konsolowej, która aktywnie monitoruje wskazany
katalog. Wykrycie nowego pliku tekstowego ma uruchomić zdarzenie, które z kolei
zainicjuje proces analizy pliku i jego archiwizacji. Zadanie kładzie silny
nacisk na architekturę sterowaną zdarzeniami (model wydawca-subskrybent).

### 📚 Wymagane koncepcje

* Zdarzenia (Events): Definiowanie, subskrybowanie (`+=`, `-=`) i wywoływanie.
* Delegaty i EventArgs: Tworzenie niestandardowych klas `EventArgs`.
* `FileSystemWatcher`: Użycie gotowej klasy do monitorowania zdarzeń.
* Strumienie (`StreamReader`): Odczytanie zawartości pliku.
* Archiwa (`ZipArchive`, `ZipFile`): Dodawanie plików do archiwum ZIP.
* Operacje na plikach/katalogach: `Path`, `Directory`, `File`.

---

### 📋 Treść zadania

Napisz aplikację składającą się z kilku współpracujących klas:

#### 1. Struktura folderów

Ręcznie utwórz w katalogu projektu (lub pozwól aplikacji tworzyć je
automatycznie) foldery:
* `/Source` - Katalog, który będzie monitorowany.
* `/Archive` - Katalog, w którym będzie przechowywane archiwum.

#### 2. Klasa `FileProcessedEventArgs` (Argumenty Zdarzenia)

* Utwórz klasę dziedziczącą po `EventArgs`.
* Musi ona przechowywać informacje o przetworzonym pliku:
    * `string FilePath` (pełna ścieżka do pliku)
    * `int WordCount` (liczba słów w pliku)
    * `int LineCount` (liczba linii w pliku)

#### 3. Klasa `DirectoryMonitor` (Wydawca/Publisher)

Ta klasa jest sercem aplikacji. Będzie "opakowywać" `FileSystemWatcher`.

* Zdarzenie: Zdefiniuj publiczne zdarzenie (event) o nazwie
    `FileCreatedAndProcessed`, używające delegata `EventHandler<FileProcessedEventArgs>`.
* Konstruktor: Powinien przyjmować ścieżkę do monitorowanego katalogu (`Source`).
* Logika wewnętrzna:
    * W klasie utwórz instancję `FileSystemWatcher`.
    * Skonfiguruj go tak, aby monitorował tylko pliki `*.txt` i reagował
        tylko na zdarzenie `Created`.
    * Włącz monitorowanie (`EnableRaisingEvents = true`).
* Metoda-Handler dla `FileSystemWatcher`:
    * Stwórz prywatną metodę, która będzie subskrybować zdarzenie `Created`
        od `FileSystemWatcher`.
    * Gdy zdarzenie to wystąpi:
        1.  Odczekaj chwilę (np. `Thread.Sleep(100)`) – plik może być jeszcze
            blokowany przez system.
        2.  Otwórz wykryty plik używając `StreamReader` (w bloku `using`).
        3.  Policz liczbę linii i słów w pliku.
        4.  Stwórz instancję `FileProcessedEventArgs` z zebranymi danymi.
        5.  Wywołaj własne zdarzenie `FileCreatedAndProcessed`, przekazując
            do niego nowo utworzone argumenty.

#### 4. Klasa `Archiver` (Subskrybent 1)

* Konstruktor: Powinien przyjmować ścieżkę do docelowego pliku archiwum
    (np. `/Archive/backup.zip`).
* Metoda publiczna `Subscribe(DirectoryMonitor monitor)`: Ta metoda powinna
    subskrybować zdarzenie `FileCreatedAndProcessed` od obiektu `monitor`.
* Metoda-Handler (Reakcja):
    * Stwórz prywatną metodę, która będzie reagować na zdarzenie.
    * Gdy zdarzenie wystąpi, metoda ma za zadanie:
        1.  Dodać plik (wskazany w `e.FilePath`) do archiwum ZIP. Użyj
            `ZipArchive` w trybie `Update`.
        2.  Użyj `Path.GetFileName` dla nazwy wpisu w ZIP.

#### 5. Klasa `ConsoleLogger` (Subskrybent 2)

* Druga, prostsza klasa, która również będzie subskrybentem.
* Metoda publiczna `Subscribe(DirectoryMonitor monitor)`: Podobnie jak
    w `Archiver`, subskrybuje to samo zdarzenie `FileCreatedAndProcessed`.
* Metoda-Handler (Reakcja):
    * Jej reakcją ma być jedynie wypisanie informacji na konsolę, np.
        `[Logger] Wykryto nowy plik: [nazwa_pliku], Linii: [liczba_linii], Słów: [liczba_słów]`.

#### 6. `Program.cs` (Orkiestracja)

* W metodzie `Main`:
    1.  Ustal ścieżki do folderów `Source` i `Archive` i utwórz je.
    2.  Utwórz instancję `DirectoryMonitor`, wskazując na folder `Source`.
    3.  Utwórz instancję `Archiver`, wskazując na plik `/Archive/backup.zip`.
    4.  Utwórz instancję `ConsoleLogger`.
    5.  Zasubskrybuj zdarzenia: `logger.Subscribe(monitor)` i `archiver.Subscribe(monitor)`.
    6.  Wypisz na konsolę informację, np. "Monitorowanie aktywne..."
    7.  Pozostaw aplikację działającą (np. przez `Console.ReadKey()`).

==================================================================================
*/

// Tutaj wklej swój kod rozwiązania...

using System;
using System.IO;
using System.IO.Compression;
using System.Threading; // Dla Thread.Sleep

namespace EventBasedArchiver
{
    // --- 1. Definicja Argumentów Zdarzenia ---

    /// <summary>
    /// Przechowuje dane o przetworzonym pliku,
    /// które zostaną wysłane do subskrybentów.
    /// </summary>
    public class FileProcessedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public int WordCount { get; }
        public int LineCount { get; }

        public FileProcessedEventArgs(string filePath, int wordCount, int lineCount)
        {
            FilePath = filePath;
            WordCount = wordCount;
            LineCount = lineCount;
        }
    }

    // --- 2. Klasa Wydawcy (Publisher) ---

    /// <summary>
    /// Monitoruje katalog i powiadamia subskrybentów o nowych,
    /// przetworzonych plikach.
    /// </summary>
    public class DirectoryMonitor
    {
        // --- KROK 1: DEFINICJA ZDARZENIA ---
        // Definiujemy "dzwoneczek", na który inni mogą subskrybować.
        public event EventHandler<FileProcessedEventArgs> FileCreatedAndProcessed;

        private readonly FileSystemWatcher _watcher;

        public DirectoryMonitor(string path)
        {
            _watcher = new FileSystemWatcher(path);
            _watcher.Filter = "*.txt";
            _watcher.Created += OnFileCreated; // Subskrybujemy wewnętrzne zdarzenie
        }

        public void Start()
        {
            Console.WriteLine("[Monitor] Uruchamiam monitorowanie...");
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        // Metoda-Handler dla zdarzenia z FileSystemWatcher
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Plik może być jeszcze używany przez system, dajmy mu chwilę
            Thread.Sleep(100); 

            try
            {
                // 1. Przetwarzanie pliku (odczyt strumieniem)
                int lineCount = 0;
                int wordCount = 0;

                using (StreamReader reader = new StreamReader(e.FullPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineCount++;
                        wordCount += line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    }
                }

                // 2. Przygotowanie danych do wysyłki
                var eventArgs = new FileProcessedEventArgs(e.FullPath, wordCount, lineCount);

                // --- KROK 2: WYWOŁANIE ZDARZENIA ---
                // "Naciskamy dzwoneczek", powiadamiając wszystkich subskrybentów.
                OnFileCreatedAndProcessed(eventArgs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] Błąd podczas przetwarzania pliku {e.Name}: {ex.Message}");
            }
        }

        // Metoda pomocnicza do bezpiecznego wywołania zdarzenia
        protected virtual void OnFileCreatedAndProcessed(FileProcessedEventArgs e)
        {
            // Sprawdzamy, czy ktokolwiek subskrybuje (czy lista nie jest pusta)
            FileCreatedAndProcessed?.Invoke(this, e);
        }
    }

    // --- 3. Klasa Subskrybenta 1 ---

    /// <summary>
    /// Subskrybent, którego zadaniem jest archiwizacja pliku.
    /// </summary>
    public class Archiver
    {
        private readonly string _archivePath;

        public Archiver(string archivePath)
        {
            _archivePath = archivePath;
        }

        // --- KROK 3: SUBSKRYPCJA ---
        public void Subscribe(DirectoryMonitor monitor)
        {
            // "Klikamy dzwoneczek" (operator +=)
            monitor.FileCreatedAndProcessed += OnFileReadyForArchive;
        }

        // Metoda-Handler (Reakcja na zdarzenie)
        private void OnFileReadyForArchive(object sender, FileProcessedEventArgs e)
        {
            try
            {
                // Używamy ZipArchive w trybie Update, aby móc dodawać do istniejącego ZIPa
                using (var archive = ZipFile.Open(_archivePath, ZipArchiveMode.Update))
                {
                    string entryName = Path.GetFileName(e.FilePath);
                    
                    // Usuwamy stary wpis, jeśli istnieje, aby go zaktualizować
                    var existingEntry = archive.GetEntry(entryName);
                    existingEntry?.Delete();
                    
                    // Dodajemy plik do archiwum
                    archive.CreateEntryFromFile(e.FilePath, entryName);
                }

                Console.WriteLine($"  -> [Archiver] Dodano/zaktualizowano plik {Path.GetFileName(e.FilePath)} w archiwum.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  -> [Archiver] Błąd podczas archiwizacji: {ex.Message}");
            }
        }
    }

    // --- 4. Klasa Subskrybenta 2 ---

    /// <summary>
    /// Subskrybent, którego zadaniem jest logowanie do konsoli.
    /// </summary>
    public class ConsoleLogger
    {
        // --- KROK 3: SUBSKRYPCJA ---
        public void Subscribe(DirectoryMonitor monitor)
        {
            // Ta klasa klika TEN SAM dzwoneczek
            monitor.FileCreatedAndProcessed += OnFileProcessed;
        }

        // Metoda-Handler (Reakcja na zdarzenie)
        private void OnFileProcessed(object sender, FileProcessedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  -> [Logger] Wykryto plik: {Path.GetFileName(e.FilePath)}, Linii: {e.LineCount}, Słów: {e.WordCount}");
            Console.ResetColor();
        }
    }

    // --- 5. Główny Program (Orkiestracja) ---

    class Program
    {
        static void Main(string[] args)
        {
            // 1. Konfiguracja środowiska
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceDir = Path.Combine(baseDir, "Source");
            string archiveDir = Path.Combine(baseDir, "Archive");
            string archiveFile = Path.Combine(archiveDir, "backup.zip");

            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(archiveDir);

            // 2. Tworzenie obiektów
            var monitor = new DirectoryMonitor(sourceDir);
            var archiver = new Archiver(archiveFile);
            var logger = new ConsoleLogger();

            // 3. Podpinanie subskrybentów
            archiver.Subscribe(monitor);
            logger.Subscribe(monitor);

            // 4. Uruchomienie
            monitor.Start();

            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("Monitoring aktywny.");
            Console.WriteLine($"Wrzuć dowolny plik .txt do folderu: \n{sourceDir}");
            Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
            Console.WriteLine("-----------------------------------------------------");

            // 5. Oczekiwanie na zakończenie
            Console.ReadKey();
            monitor.Stop();
            Console.WriteLine("[Program] Zamykanie aplikacji.");
        }
    }
}
