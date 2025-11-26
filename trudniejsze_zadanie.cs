static void RunReports()
{
    var asm = Assembly.GetExecutingAssembly();

    // KROK 1: Filtrowanie typów
    var reportTypes = asm.GetTypes().Where(t =>
    {
        // A. Atrybut
        bool hasAttr = t.GetCustomAttribute<ReportGeneratorAttribute>() != null;
        
        // B. Interfejs Generyczny (Open Generic Check)
        bool isReport = t.GetInterfaces().Any(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IReport<>)
        );

        return hasAttr && isReport && !t.IsAbstract;
    });

    foreach (var type in reportTypes)
    {
        Console.WriteLine($"---> Znaleziono typ: {type.Name}");

        // KROK 2: Tworzenie instancji z parametrem w konstruktorze
        // Szukamy konstruktora, który przyjmuje jeden parametr typu string
        ConstructorInfo ctor = type.GetConstructor(new[] { typeof(string) });
        
        if (ctor == null)
        {
            Console.WriteLine("Błąd: Nie znaleziono odpowiedniego konstruktora.");
            continue;
        }

        // Wywołujemy konstruktor: new SalesReport("Egzamin_2025")
        object instance = ctor.Invoke(new object[] { "Egzamin_2025" });


        // KROK 3: Ustawienie pola prywatnego _maxRetries
        // BindingFlags są kluczowe, żeby zobaczyć "private"
        FieldInfo field = type.GetField("_maxRetries", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (field != null)
        {
            field.SetValue(instance, 3); // Zmieniamy 1 na 3
            Console.WriteLine("     [Sukces] Zmieniono pole prywatne _maxRetries na 3");
        }


        // KROK 4: Wywołanie metody Generate()
        MethodInfo method = type.GetMethod("Generate");
        
        if (method != null)
        {
            // Invoke zwraca object, mimo że metoda zwraca SalesData
            object result = method.Invoke(instance, null); // null, bo metoda nie ma parametrów
            Console.WriteLine($"     [Wynik] {result}");
        }
    }
}