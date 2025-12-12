using System.Reflection;

namespace TypeCrafter;

public class ParseException : Exception
{
    public ParseException() { }

    public ParseException(string message) 
        : base(message) { }

    public ParseException(string message, Exception innerException) 
        : base(message, innerException) { }
}

public static class TypeCrafter
{
    public static T CraftInstance<T>()
{
    Type type = typeof(T);

    // KROK 1: Sprawdź konstruktor bezparametrowy
    var constructor = type.GetConstructor(Type.EmptyTypes);
    if (constructor == null)
    {
        throw new InvalidOperationException($"Typ {type.Name} nie ma pustego konstruktora!");
    }

    // Stwórz instancję (pusty obiekt)
    var instance = (T)constructor.Invoke(null);

    // KROK 2: Przejdź przez właściwości
    // BindingFlags.Public | BindingFlags.Instance oznacza "daj mi publiczne, niestatyczne pola"
    foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
    {
        // Sprawdzamy czy możemy pisać do tej właściwości (czy ma 'set')
        if (!property.CanWrite) continue;

        Console.WriteLine($"Podaj wartość dla {property.Name} ({property.PropertyType.Name}):");
        string input = Console.ReadLine();

        // KROK 3A: String
        if (property.PropertyType == typeof(string))
        {
            property.SetValue(instance, input);
        }
        // KROK 3B: Typy proste (int, double, guid...) - szukamy TryParse
        else
        {
            // Szukamy metody TryParse.
            // MakeByRefType() jest kluczowe, bo szukamy parametru 'out T result'
            var tryParseMethod = property.PropertyType.GetMethod("TryParse",
                BindingFlags.Public | BindingFlags.Static,
                new[] { typeof(string), typeof(IFormatProvider), property.PropertyType.MakeByRefType() });

            if (tryParseMethod != null)
            {
                // Przygotowujemy parametry: [string, provider, wynik]
                // null na drugim miejscu to IFormatProvider
                // null na trzecim miejscu to miejsce, gdzie trafi wynik (parametr out)
                object[] args = new object[] { input, null, null };

                // Wywołujemy metodę. Invoke zwraca to, co metoda (bool), a wynik ląduje w args[2]
                bool success = (bool)tryParseMethod.Invoke(null, args);

                if (success)
                {
                    property.SetValue(instance, args[2]); // args[2] to nasza sparsowana liczba/data
                }
                else
                {
                    throw new ParseException($"Nie udało się zamienić '{input}' na {property.PropertyType.Name}");
                }
            }
// KROK 3C: Obiekt złożony (rekurencja)
            else
            {
                // PRZYKŁAD SYTUACJI:
                // Jesteśmy w trakcie tworzenia obiektu 'Invoice' (Faktura).
                // Pętla trafiła na właściwość: public Customer Buyer { get; set; }
                // property.PropertyType to więc typ 'Customer'.

                Console.WriteLine($"Właściwość '{property.Name}' to złożony obiekt typu {property.PropertyType.Name}. Tworzę go...");

                // 1. Pobieramy "przepis" na metodę CraftInstance.
                // Musimy użyć refleksji, żeby zdobyć dostęp do metody, w której właśnie jesteśmy!
                // Dlaczego? Bo musimy ją wywołać ponownie, ale dla innego typu.
                // craftMethod to teraz definicja: "public static T CraftInstance<T>()"
                var craftMethod = typeof(TypeCrafter).GetMethod(nameof(CraftInstance));

                // 2. Konkretyzujemy metodę (Wypełniamy <T>).
                // Mamy ogólną metodę CraftInstance<T>, ale potrzebujemy CraftInstance<Customer>.
                // MakeGenericMethod bierze typ właściwości (np. Customer) i tworzy nową wersję metody specjalnie dla niego.
                // genericCraftMethod to teraz definicja: "public static Customer CraftInstance<Customer>()"
                var genericCraftMethod = craftMethod.MakeGenericMethod(property.PropertyType);

                // 3. Uruchamiamy tę nową metodę (Rekurencja).
                // Invoke(null, null) oznacza:
                // - pierwszy null: metoda jest statyczna, nie potrzebuje obiektu "rodzica".
                // - drugi null: metoda nie przyjmuje argumentów ().
                // W tym momencie program "wchodzi" do środka nowej metody CraftInstance,
                // pyta użytkownika o pola Klienta (Imię, ID...) i wraca dopiero, jak stworzy całego Klienta.
                object nestedObject = genericCraftMethod.Invoke(null, null);
                // nestedObject to teraz gotowy obiekt typu Customer (np. Jan Kowalski, ID: 5).

                // 4. Przypisujemy gotowego "pod-obiekt" do głównego obiektu.
                // Invoice.Buyer = JanKowalski;
                property.SetValue(instance, nestedObject);
            }
        }
    }

    return instance;
}
}