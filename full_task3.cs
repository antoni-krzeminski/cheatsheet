/*
==================================================================================
==                      ZADANIE LABORATORYJNE: SORTER PLIKÓW                      ==
==================================================================================

Cel: Stworzenie aplikacji konsolowej .NET, która monitoruje wskazany katalog 
("źródłowy") i automatycznie sortuje pojawiające się w nim pliki graficzne oraz 
archiwa. Sortowanie odbywa się na podstawie daty ostatniej modyfikacji pliku. 
Aplikacja tworzy strukturę folderów ROK/MIESIAC i obsługuje archiwa .zip.

==================================================================================
*/

using System;
using System.Collections.Generic; // Dla HashSet
using System.IO;                  // Dla File, Directory, Path, FileInfo, FileSystemWatcher
using System.IO.Compression;      // Dla ZipFile, ZipArchive
using System.Threading;           // Dla Thread.Sleep

namespace FileSorterApp
{
    /// <summary>
    /// Główna klasa orkiestrująca proces sortowania.
    /// Łączy w sobie logikę wszystkich 4 etapów.
    /// </summary>
    public class SorterEngine
    {
        // --- Pola konfiguracyjne ---

        private readonly string _sourcePath;      // Katalog monitorowany
        private readonly string _targetPath;      // Katalog docelowy (Posortowane)
        private readonly string _archiveMovePath; // Katalog na przetworzone pliki ZIP

        // Używamy HashSet dla BŁYSKAWICZNEGO sprawdzania rozszerzeń (szybsze niż Lista)
        private readonly HashSet<string> _imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif"
        };
        
        private const string ZipExtension = ".zip";

        // --- Konstruktor ---

        public SorterEngine(string sourcePath, string targetPath)
        {
            _sourcePath = sourcePath;
            _targetPath = targetPath;

            // Definiujemy ścieżkę dla przetworzonych archiwów
            _archiveMovePath = Path.Combine(_targetPath, "Archiwa");
            
            // Upewniamy się, że katalogi istnieją od samego początku
            Directory.CreateDirectory(_sourcePath);
            Directory.CreateDirectory(_targetPath);
            Directory.CreateDirectory(_archiveMovePath);
        }

        // --- Logika GŁÓWNA (Etapy 1, 2, 3) ---

        /// <summary>
        /// Metoda realizująca Etapy 1-3: Jednorazowe skanowanie
        /// i przetwarzanie wszystkich plików już istniejących w katalogu źródłowym.
        /// </summary>
        public void ProcessExistingFiles()
        {
            LogInfo($"--- Skanowanie jednorazowe folderu: {_sourcePath} ---");
            try
            {
                // Używamy EnumerateFiles, aby nie ładować całej listy do pamięci
                foreach (string filePath in Directory.EnumerateFiles(_sourcePath))
                {
                    // Używamy tej samej logiki, co dla Watchera
                    ProcessFile(filePath);
                }
            }
            catch (Exception ex)
            {
                LogError($"Krytyczny błąd podczas skanowania jednorazowego: {ex.Message}", ex);
            }
            LogInfo("--- Skanowanie jednorazowe zakończone ---");
        }

        /// <summary>
        /// Główna metoda przetwarzająca pojedynczy plik.
        /// Wywoływana zarówno przez skan jednorazowy, jak i przez Watchera.
        /// </summary>
        private void ProcessFile(string filePath)
        {
            // Sprawdzenie, czy plik jeszcze istnieje (mógł być np. szybko usunięty)
            if (!File.Exists(filePath))
            {
                LogSkip(filePath, "Plik zniknął przed przetworzeniem.");
                return;
            }

            string extension = Path.GetExtension(filePath).ToLower();

            if (_imageExtensions.Contains(extension))
            {
                // ETAP 2: Przetwarzanie pliku graficznego
                ProcessImageFile(filePath);
            }
            else if (extension == ZipExtension)
            {
                // ETAP 3: Przetwarzanie archiwum ZIP
                ProcessZipFile(filePath);
            }
            else
            {
                // Logowanie pominiętych plików
                LogSkip(Path.GetFileName(filePath), "Nieobsługiwane rozszerzenie");
            }
        }

        /// <summary>
        /// Logika dla Etapu 2: Przenoszenie plików graficznych.
        /// </summary>
        private void ProcessImageFile(string filePath)
        {
            try
            {
                // Odczyt daty modyfikacji
                DateTime modTime = File.GetLastWriteTime(filePath);
                
                // Wygenerowanie ścieżki docelowej i utworzenie katalogów
                string destinationPath = GetDestinationPath(filePath, modTime);

                // Logika przenoszenia z obsługą konfliktów
                MoveFileWithConflictCheck(filePath, destinationPath);
            }
            catch (Exception ex)
            {
                LogError($"Nie można przenieść pliku {Path.GetFileName(filePath)}", ex);
            }
        }

        /// <summary>
        /// Logika dla Etapu 3: Przetwarzanie plików .zip.
        /// </summary>
        private void ProcessZipFile(string zipPath)
        {
            try
            {
                // Używamy 'using', aby mieć pewność, że plik zostanie zamknięty
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    LogInfo($"Przetwarzam archiwum: {Path.GetFileName(zipPath)}...");

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Sprawdzamy, czy wpis w archiwum jest obrazkiem
                        string entryExtension = Path.GetExtension(entry.FullName).ToLower();
                        if (_imageExtensions.Contains(entryExtension))
                        {
                            // Używamy daty modyfikacji pliku WEWNĄTRZ archiwum
                            DateTime modTime = entry.LastWriteTime.DateTime;

                            // Tworzymy ścieżkę docelową dla pliku Z archiwum
                            string extractPath = GetDestinationPath(entry.FullName, modTime);

                            // Wypakowanie z obsługą konfliktów
                            ExtractEntryWithConflictCheck(entry, extractPath);
                        }
                    }
                } // Archiwum jest zamykane automatycznie tutaj

                // Po pomyślnym przetworzeniu, przenosimy plik .zip
                string movedZipPath = Path.Combine(_archiveMovePath, Path.GetFileName(zipPath));
                MoveFileWithConflictCheck(zipPath, movedZipPath);
            }
            catch (InvalidDataException)
            {
                LogError($"Plik {Path.GetFileName(zipPath)} nie jest poprawnym archiwum ZIP.", null);
            }
            catch (Exception ex)
            {
                LogError($"Nie można przetworzyć archiwum {Path.GetFileName(zipPath)}", ex);
            }
        }

        // --- Metody Pomocnicze (Współdzielone przez Etapy) ---

        /// <summary>
        /// Tworzy ścieżkę docelową ROK/MIESIAC na podstawie daty modyfikacji
        /// i dba o to, by katalog istniał.
        /// </summary>
        private string GetDestinationPath(string originalFilePath, DateTime modTime)
        {
            string year = modTime.Year.ToString();
            string month = modTime.Month.ToString("00"); // "00" zapewnia format "05" zamiast "5"
            string fileName = Path.GetFileName(originalFilePath);

            // Tworzymy ścieżkę: .../Posortowane/2023/05
            string targetDir = Path.Combine(_targetPath, year, month);

            // ETAP 1: Tworzenie katalogów
            // Directory.CreateDirectory jest "inteligentne" - 
            // stworzy folder tylko jeśli nie istnieje. Nie trzeba sprawdzać.
            Directory.CreateDirectory(targetDir);

            // Zwraca pełną ścieżkę docelową dla pliku
            return Path.Combine(targetDir, fileName);
        }

        /// <summary>
        /// Wspólna logika przenoszenia plików z obsługą błędów i konfliktów (Etap 2).
        /// </summary>
        private void MoveFileWithConflictCheck(string sourcePath, string destPath)
        {
            try
            {
                if (File.Exists(destPath))
                {
                    // Obsługa konfliktów: Plik już istnieje.
                    // Zmieniamy nazwę, dodając znacznik czasu, aby uniknąć nadpisania.
                    string newFileName = $"{Path.GetFileNameWithoutExtension(destPath)}_{DateTime.Now:HHmmss}{Path.GetExtension(destPath)}";
                    destPath = Path.Combine(Path.GetDirectoryName(destPath), newFileName);
                    LogInfo($"Konflikt! Zmieniam nazwę na: {newFileName}");
                }

                File.Move(sourcePath, destPath);
                LogMove(Path.GetFileName(sourcePath), destPath);
            }
            catch (Exception ex)
            {
                LogError($"Błąd przenoszenia {Path.GetFileName(sourcePath)}", ex);
            }
        }

        /// <summary>
        /// Wspólna logika wypakowywania plików z ZIP z obsługą błędów i konfliktów (Etap 3).
        /// </summary>
        private void ExtractEntryWithConflictCheck(ZipArchiveEntry entry, string extractPath)
        {
            try
            {
                if (File.Exists(extractPath))
                {
                    // Obsługa konfliktów (taka sama jak wyżej)
                    string newFileName = $"{Path.GetFileNameWithoutExtension(extractPath)}_{DateTime.Now:HHmmss}{Path.GetExtension(extractPath)}";
                    extractPath = Path.Combine(Path.GetDirectoryName(extractPath), newFileName);
                }

                entry.ExtractToFile(extractPath);
                LogInfo($"   Wypakowano: {entry.FullName} -> {extractPath}");
            }
            catch (Exception ex)
            {
                LogError($"   Błąd wypakowania {entry.FullName}", ex);
            }
        }

        // --- Logika dla Etapu 4: FileSystemWatcher ---

        /// <summary>
        /// Uruchamia monitorowanie katalogu źródłowego.
        /// </summary>
        public void StartMonitoring()
        {
            FileSystemWatcher watcher = new FileSystemWatcher(_sourcePath);
            
            // Interesują nas tylko nowe pliki.
            watcher.NotifyFilter = NotifyFilters.FileName;
            
            // Subskrypcja zdarzenia (podpięcie "głośnika" pod "dzwonek")
            watcher.Created += OnFileCreated;
            
            // Filtrujemy, aby nie łapać np. plików .tmp
            watcher.Filter = "*.*";

            // Uruchomienie monitorowania
            watcher.EnableRaisingEvents = true;

            LogInfo("--- Monitorowanie aktywne. Czekam na nowe pliki... ---");
        }

        /// <summary>
        /// Metoda-Handler (Reakcja), która jest wywoływana, gdy Watcher wykryje nowy plik.
        /// </summary>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            LogInfo($"Wykryto nowy plik: {e.Name}");

            // KRYTYCZNE: Musimy zaczekać chwilę.
            // System Windows często blokuje plik przez ułamek sekundy 
            // w trakcie jego kopiowania. Bez tego
            // ProcessFile zgłosi błąd "plik jest używany przez inny proces".
            Thread.Sleep(500); // 0.5 sekundy opóźnienia dla bezpieczeństwa

            // KRYTYCZNE: Cały handler musi być w try-catch.
            // Jeśli tutaj wystąpi nieobsłużony błąd, cały Watcher przestanie działać.
            try
            {
                // Po prostu wywołujemy naszą główną logikę przetwarzania
                ProcessFile(e.FullPath);
            }
            catch (Exception ex)
            {
                LogError($"Krytyczny błąd w handlerze Watchera (plik: {e.Name})", ex);
            }
        }


        // --- ETAP 2: Logowanie (metody pomocnicze) ---

        private void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }

        private void LogMove(string fileName, string destination)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[PRZENIESIONO] {fileName} -> {destination}");
            Console.ResetColor();
        }

        private void LogSkip(string fileName, string reason)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[POMIJAM] {fileName} ({reason})");
            Console.ResetColor();
        }

        private void LogError(string message, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[BŁĄD] {message}");
            if (ex != null)
            {
                Console.WriteLine($"       Szczegóły: {ex.Message}");
            }
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Główna klasa programu - punkt startowy aplikacji.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Sorter Plików 1.0";
            
            // --- Konfiguracja Ścieżek ---
            // Możesz zmienić te ścieżki
            // Upewnij się, że masz uprawnienia do zapisu w C:\
            // lub użyj ścieżek względnych, np. "Source" i "Sorted"
            string sourcePath = @"C:\SortSource";
            string targetPath = @"C:\SortTarget";

            // 1. Inicjalizacja silnika sortującego
            SorterEngine sorter = new SorterEngine(sourcePath, targetPath);

            // 2. Realizacja Etapów 1-3: Przetworzenie plików, które już tam są.
            sorter.ProcessExistingFiles();

            // 3. Realizacja Etapu 4: Uruchomienie monitorowania na nowe pliki.
            sorter.StartMonitoring();
            
            // 4. Utrzymanie aplikacji przy życiu
            Console.WriteLine("\nNaciśnij [Enter], aby zakończyć program...");
            Console.ReadLine();
        }
    }
}
