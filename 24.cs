using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Duck;

// Klasa reprezentująca stan całej sceny w grze (mapa, postacie, czas)
public class Scene
{
    // Właściwość gracza - kaczka, którą sterujemy (ma ustawione zachowanie PlayerControlled)
    public Duck Player { get; set; } = new("") { Behaviour = new Duck.PlayerControlled() };
    
    // Lista wszystkich pozostałych kaczek na scenie (np. NPC)
    public List<Duck> Ducks { get; set; } = [];
    
    // Czas gry - potrzebny do symulacji
    public DateTime Time { get; set; } = DateTime.Now;

    // Pusty konstruktor - jest niezbędny, aby deserializator JSON mógł stworzyć obiekt
    // zanim wypełni go danymi z pliku.
    public Scene()
    {
    }

    // Konstruktor wczytujący scenę z "Zasobów Wbudowanych" (Embedded Resources)
    // "path" to nazwa pliku, który został wkompilowany do pliku .exe (nie leży luzem na dysku)
    public Scene(string path)
    {
        // 1. Pobieramy strumień danych bezpośrednio z pliku .exe (Assembly)
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        
        // Zabezpieczenie - jeśli plik nie istnieje wewnątrz .exe, rzucamy błąd
        if (stream is null) throw new FileNotFoundException("Could not find scene file", path);
        
        // 2. Czytamy plik linijka po linijce
        using StreamReader streamReader = new StreamReader(stream);
        
        // Pętla czyta, dopóki są linie w pliku (wynik nie jest null)
        while (streamReader.ReadLine() is { } line)
        {
            // Parsowanie linii CSV (np. "Kaczka1; 10,5; 20,1; 90; 1.0")
            // Dzielimy po średniku lub przecinku, usuwamy spacje
            var tokens = line.Split([";", ","], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            
            // Przypisanie wartości z tablicy stringów do zmiennych
            var name = tokens[0];
            // Parsowanie pozycji X i Z (Y jest zawsze 0 dla płaskiej wody)
            var position = new Vector3(float.Parse(tokens[1]), 0, float.Parse(tokens[2]));
            // Konwersja stopni na radiany (OpenTK używa radianów)
            var rotation = float.DegreesToRadians(float.Parse(tokens[3]));
            var scale = float.Parse(tokens[4]);
            
            // Dodanie nowej kaczki do listy
            Ducks.Add(new Duck(name, position, rotation, scale));
        }
    }

    // Metoda pomocnicza zwracająca jedną kolekcję zawierającą Gracza ORAZ inne kaczki.
    // "yield return" pozwala zwracać elementy jeden po drugim bez tworzenia nowej listy w pamięci.
    public IEnumerable<Duck> GetAllDucks()
    {
        yield return Player; // Najpierw zwróć gracza
        foreach (var duck in Ducks)
        {
            yield return duck; // Potem zwróć resztę
        }
    }

    // Główna pętla logiki gry (wywoływana co klatkę)
    public void Update(float dt, KeyboardState keyboard, MouseState mouse)
    {
        // Aktualizacja czasu gry
        Time += TimeSpan.FromMinutes(dt);
        
        // Aktualizacja logiki gracza (ruch, sterowanie)
        Player.Update(dt, keyboard, mouse);
        
        // Aktualizacja logiki wszystkich innych kaczek
        foreach (var duck in Ducks)
        {
            duck.Update(dt, keyboard, mouse);
        }
    }

    // Konfiguracja serializatora JSON
    // Jest potrzebna, bo domyślny serializator nie wie, jak zapisać typy Vector3 z biblioteki OpenTK.
    private JsonSerializerOptions GetJsonSerializerOptions()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.Converters.Add(new Vector3Converter()); // Dodajemy własny konwerter dla Vector3
        options.Converters.Add(new Vector2Converter()); // Dodajemy własny konwerter dla Vector2
        options.WriteIndented = true; // Ładne formatowanie (z wcięciami)
        return options;
    }

    // Metoda zapisu stanu gry (Save Game)
    public void QuackSave()
    {
        // 1. Najpierw zamieniamy obiekt Scene na zwykły tekst (JSON)
        JsonSerializerOptions options = GetJsonSerializerOptions();
        string json = JsonSerializer.Serialize(this, options);
        
        // 2. Ustalamy ścieżkę do folderu "Moje Dokumenty/Duck/quack.save"
        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string save = Path.Combine(path, "Duck", "quack.save");
        
        // 3. Jeśli folder nie istnieje, tworzymy go
        string? directory = Path.GetDirectoryName(save);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // 4. "Kanapka ze strumieni" - Zapis skompresowany
        // Otwieramy plik do zapisu
        using FileStream fs = File.OpenWrite(save);
        
        // Nakładamy warstwę kompresji (GZip) - dane będą zmniejszone
        using GZipStream gs = new GZipStream(fs, CompressionLevel.SmallestSize);
        
        // Nakładamy warstwę do pisania tekstu
        using StreamWriter sw = new StreamWriter(gs);
        
        // Fizycznie zapisujemy JSON do pliku (przez kompresor)
        sw.Write(json);
    }

    // Metoda wczytywania gry (Load Game)
    public Scene? QuackLoad()
    {
        // Ustalamy ścieżkę do pliku zapisu
        string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string save = Path.Combine(path, "Duck", "quack.save");

        // Jeśli plik istnieje, zaczynamy proces wczytywania
        if (File.Exists(save))
        {
            // 1. Otwieramy plik z dysku
            using FileStream fs = File.OpenRead(save);
            
            // 2. Nakładamy warstwę dekompresji (musi być ta sama metoda co przy zapisie - GZip)
            using GZipStream gs = new GZipStream(fs, CompressionMode.Decompress);
            
            // 3. Czytamy rozpakowany strumień jako tekst
            using StreamReader sr = new StreamReader(gs);
            
            // Pobieramy cały JSON do zmiennej string
            string json = sr.ReadToEnd();
            
            // 4. Deserializujemy tekst z powrotem na obiekt klasy Scene
            JsonSerializerOptions options = GetJsonSerializerOptions();
            return JsonSerializer.Deserialize<Scene>(json, options);
        }
        
        // Jeśli plik nie istnieje, zwracamy null
        return null;
    }
}
