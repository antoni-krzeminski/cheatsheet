/*
==================================================================================
==                            ZADANIE LABORATORYJNE                             ==
==================================================================================

## Laboratorium: Automatyczny Sorter Plików

### 🎯 Cel ćwiczenia

Celem jest napisanie aplikacji konsolowej, która monitoruje katalog "Inbox".
Gdy pojawi się w nim nowy plik, aplikacja musi automatycznie przenieść go
do odpowiedniego podkatalogu w folderze "Sorted", bazując na jego rozszerzeniu.
Aplikacja ma również dynamicznie tworzyć te podkatalogi, jeśli jeszcze nie
istnieją.

### 📚 Wymagane koncepcje

* Monitorowanie katalogu (`FileSystemWatcher`)
* Operacje na plikach (`File.Move`, `File.Exists`)
* Operacje na katalogach (`Directory.CreateDirectory`)
* Praca ze ścieżkami i rozszerzeniami (`Path.GetExtension`, `Path.GetFileName`, `Path.Combine`)
* Czytanie plików (`StreamReader` / `File.ReadAllLines`) - do wczytania reguł
* Obsługa zdarzeń (`Watcher.Created += ...`)

---

### 📋 Treść zadania

Napisz aplikację konsolową, która będzie działać jako demon sortujący.

#### 1. Struktura folderów

Aplikacja po uruchomieniu powinna sama zadbać o stworzenie następującej
struktury w folderze, z którego jest uruchamiana:

* `/Inbox` - Katalog, do którego użytkownik będzie wrzucał pliki.
* `/Sorted` - Katalog, w którym aplikacja będzie tworzyć podkatalogi
    i umieszczać posortowane pliki.
* `/Config` - Katalog zawierający plik z regułami sortowania.

#### 2. Plik Konfiguracyjny (`rules.txt`)

W folderze `/Config` aplikacja ma stworzyć (jeśli nie istnieje) plik `rules.txt`
z przykładową zawartością:

    Images=.jpg,.png,.gif
    Documents=.pdf,.docx,.txt
    Music=.mp3,.wav

#### 3. Klasa `FileSorter`

Stwórz główną klasę logiki `FileSorter`.

* **Pola:**
    * `FileSystemWatcher _watcher`
    * `string _inboxPath`
    * `string _sortedPath`
    * `Dictionary<string, string> _rules` - Klucz to rozszerzenie (np. ".jpg"),
        Wartość to nazwa folderu (np. "Images").

* **Konstruktor `FileSorter(string inboxPath, string sortedPath, string configPath)`:**
    * Powinien inicjalizować ścieżki.
    * Powinien wywołać metodę `LoadRules(configPath)`, która wczyta plik
        `rules.txt` ("Czytanie") i wypełni słownik `_rules`.
        * *Wskazówka:* Użyj `File.ReadAllLines`, a następnie dla każdej linii
            użyj `Split('=')` i `Split(',')`. Pamiętaj o dodaniu kropki do
            rozszerzeń (np. `.jpg`).

* **Metoda `Start()`:**
    * Inicjalizuje `FileSystemWatcher`, ustawia jego `Path` na `_inboxPath`.
    * Subskrybuje metodę (np. `OnFileCreated`) do zdarzenia `Watcher.Created`.
    * Włącza monitorowanie (`EnableRaisingEvents = true`).

* **Metoda-Handler `OnFileCreated(object sender, FileSystemEventArgs e)`:**
    * To jest serce aplikacji. Gdy `Watcher` wykryje nowy plik:
        1.  Odczekaj chwilę (np. `Thread.Sleep(100)`) na zwolnienie pliku.
        2.  Pobierz rozszerzenie pliku: `string ext = Path.GetExtension(e.FullPath)`.
        3.  Sprawdź, czy dla tego rozszerzenia istnieje reguła w słowniku `_rules`.
        4.  Ustal folder docelowy:
            * Jeśli reguła istnieje, `string destFolder = _rules[ext]` (np. "Images").
            * Jeśli nie, `string destFolder = "Other"`.
        5.  Stwórz pełną ścieżkę do katalogu docelowego:
            `string destDirectoryPath = Path.Combine(_sortedPath, destFolder)`.
        6.  **Utwórz katalog:** Użyj `Directory.CreateDirectory(destDirectoryPath)`.
            Ta metoda jest "inteligentna" - stworzy folder tylko jeśli nie istnieje.
        7.  Stwórz pełną ścieżkę docelową dla pliku:
            `string destFilePath = Path.Combine(destDirectoryPath, e.Name)`.
        8.  **Przenieś plik:** Użyj `File.Move(e.FullPath, destFilePath)`.
        9.  Wypisz na konsolę log, co zostało zrobione.

#### 4. `Program.cs` (Orkiestracja)

* W metodzie `Main`:
    1.  Zdefiniuj i stwórz wszystkie wymagane katalogi (`Inbox`, `Sorted`, `Config`).
    2.  Stwórz przykładowy plik `rules.txt`, jeśli nie istnieje.
    3.  Utwórz instancję `FileSorter`, przekazując mu odpowiednie ścieżki.
    4.  Wywołaj `sorter.Start()`.
    5.  Wypisz komunikat dla użytkownika i czekaj na klawisz (`Console.ReadKey()`).

==================================================================================
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FileSorterApp
{
    /// <summary>
    /// Główna klasa logiki. Monitoruje katalog i przenosi pliki
    /// na podstawie wczytanych reguł.
    /// </summary>
    public class FileSorter
    {
        private readonly FileSystemWatcher _watcher;
        private readonly string _inboxPath;
        private readonly string _sortedPath;
        
        // Słownik przechowuje reguły: Klucz = rozszerzenie (np. ".jpg"), Wartość = folder (np. "Images")
        private readonly Dictionary<string, string> _rules = new Dictionary<string, string>();

        /// <summary>
        /// Konstruktor inicjalizuje sorter.
        /// </summary>
        /// <param name="inboxPath">Ścieżka do monitorowanego folderu.</param>
        /// <param name="sortedPath">Ścieżka do folderu z posortowanymi plikami.</param>
        /// <param name="configPath">Ścieżka do pliku rules.txt.</param>
        public FileSorter(string inboxPath, string sortedPath, string configPath)
        {
            _inboxPath = inboxPath;
            _sortedPath = sortedPath;

            // 1. Wczytanie reguł z pliku (użycie "Czytania")
            LoadRules(configPath);

            // 2. Konfiguracja Watchera
            _watcher = new FileSystemWatcher(_inboxPath);
            _watcher.Created += OnFileCreated; // Subskrypcja zdarzenia
        }

        /// <summary>
        /// Wczytuje reguły sortowania z pliku konfiguracyjnego.
        /// </summary>
        private void LoadRules(string configPath)
        {
            Console.WriteLine("[Sorter] Wczytuję reguły...");
            try
            {
                // Używamy File.ReadAllLines (prostsza alternatywa dla StreamReader)
                string[] lines = File.ReadAllLines(configPath);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue; // Ignoruj puste linie i komentarze

                    string[] parts = line.Split('=');
                    if (parts.Length != 2) continue; // Zły format

                    string folderName = parts[0].Trim();
                    string[] extensions = parts[1].Split(',');

                    foreach (string ext in extensions)
                    {
                        string cleanExt = ext.Trim().ToLower();
                        if (!cleanExt.StartsWith("."))
                        {
                            cleanExt = "." + cleanExt;
                        }
                        
                        if (!_rules.ContainsKey(cleanExt))
                        {
                            _rules.Add(cleanExt, folderName);
                            Console.WriteLine($"  -> Reguła: {cleanExt} -> {folderName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Sorter] BŁĄD: Nie można wczytać pliku reguł ({ex.Message}). Domyślnie wszystko trafi do 'Other'.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Uruchamia monitorowanie katalogu.
        /// </summary>
        public void Start()
        {
            Console.WriteLine($"\n[Sorter] Uruchamiam monitorowanie folderu: {_inboxPath}");
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Zatrzymuje monitorowanie katalogu.
        /// </summary>
        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        /// <summary>
        /// Metoda-Handler (reakcja) wywoływana przez zdarzenie Watcher.Created
        /// </summary>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Czasem system blokuje plik przez ułamek sekundy po jego utworzeniu
            // Dajemy mu chwilę na "oddech".
            Thread.Sleep(100); 
            
            try
            {
                // 1. Pobranie rozszerzenia
                string ext = Path.GetExtension(e.FullPath).ToLower();
                
                // 2. Ustalenie folderu docelowego
                string destFolder;
                if (_rules.ContainsKey(ext))
                {
                    destFolder = _rules[ext]; // Reguła znaleziona
                }
                else
                {
                    destFolder = "Other"; // Reguła domyślna
                }

                // 3. Stworzenie ścieżki do katalogu docelowego
                string destDirectoryPath = Path.Combine(_sortedPath, destFolder);

                // 4. Tworzenie katalogu (jeśli nie istnieje)
                Directory.CreateDirectory(destDirectoryPath);

                // 5. Stworzenie pełnej ścieżki docelowej dla pliku
                string destFilePath = Path.Combine(destDirectoryPath, e.Name);

                // 6. Przeniesienie pliku (użycie "Kopiowania" / "Operacji na plikach")
                File.Move(e.FullPath, destFilePath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Sorter] Przeniesiono: {e.Name} -> {destFolder}");
                Console.ResetColor();
            }
            catch(IOException ioEx)
            {
                // Ten błąd często się zdarza, gdy plik jest wciąż używany
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Sorter] Plik {e.Name} jest wciąż używany. Spróbuję ponownie...");
                // W prawdziwej aplikacji użylibyśmy pętli ponawiania (retry logic)
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Sorter] Błąd podczas przenoszenia {e.Name}: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Główna klasa programu, odpowiedzialna za konfigurację i uruchomienie.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- Automatyczny Sorter Plików ---");

            // 1. Definiowanie i tworzenie katalogów
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string inboxDir = Path.Combine(baseDir, "Inbox");
            string sortedDir = Path.Combine(baseDir, "Sorted");
            string configDir = Path.Combine(baseDir, "Config");
            
            string configFile = Path.Combine(configDir, "rules.txt");

            Console.WriteLine("[Program] Tworzenie wymaganych katalogów...");
            Directory.CreateDirectory(inboxDir);
            Directory.CreateDirectory(sortedDir);
            Directory.CreateDirectory(configDir);

            // 2. Tworzenie przykładowego pliku konfiguracyjnego
            if (!File.Exists(configFile))
            {
                Console.WriteLine("[Program] Tworzenie przykładowego pliku rules.txt...");
                string[] defaultRules =
                {
                    "# Format: NazwaFolderu=rozszerzenia (oddzielone przecinkami)",
                    "Images=.jpg,.jpeg,.png,.gif,.bmp",
                    "Documents=.pdf,.docx,.doc,.txt,.xls,.xlsx",
                    "Music=.mp3,.wav,.flac",
                    "Archives=.zip,.rar,.7z"
                };
                File.WriteAllLines(configFile, defaultRules);
            }

            // 3. Tworzenie i uruchamianie sortera
            FileSorter sorter = new FileSorter(inboxDir, sortedDir, configFile);
            sorter.Start();

            // 4. Oczekiwanie
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("Monitorowanie aktywne.");
            Console.WriteLine($"Wrzuć pliki do folderu: \n{inboxDir}");
            Console.WriteLine("\nNaciśnij dowolny klawisz, aby zakończyć...");
            Console.WriteLine("-----------------------------------------------------");

            Console.ReadKey();
            sorter.Stop();
            Console.WriteLine("[Program] Zamykanie aplikacji.");
        }
    }
}
