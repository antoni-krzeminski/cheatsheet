using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ReflectionExamTask
{
    // --- 1. DEFINICJE (PKT 1: Tworzenie własnych atrybutów) ---

    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public class DefaultValueAttribute : Attribute
    {
        public int Value { get; }
        public DefaultValueAttribute(int value)
        {
            Value = value;
        }
    }

    // --- STRUKTURA KLAS I INTERFEJSÓW ---

    public abstract class BasePlugin { }

    // Generyczny interfejs do sprawdzenia w PKT 5
    public interface IHandler<T> { void Handle(T input); }

    // --- KONKRETNE KLASY (TESTOWE) ---

    // Ta klasa POWINNA zostać znaleziona i przetworzona
    [Plugin] 
    public class DataPlugin : BasePlugin, IHandler<string>
    {
        // Tę właściwość będziemy ustawiać przez refleksję
        [DefaultValue(5000)] 
        public int Timeout { get; set; }

        public void Handle(string input) => Console.WriteLine("Processing data...");
    }

    // Ta klasa NIE powinna zostać znaleziona (brak atrybutu [Plugin])
    public class HiddenPlugin : BasePlugin, IHandler<string>
    {
        [DefaultValue(100)]
        public int Timeout { get; set; }
        public void Handle(string input) { }
    }

    // Ta klasa NIE powinna zostać znaleziona (nie dziedziczy po BasePlugin)
    [Plugin]
    public class StandaloneHandler : IHandler<string>
    {
        public void Handle(string input) { }
    }

    // --- PROGRAM GŁÓWNY ---

    class Program
    {
        static void Main(string[] args)
        {
            // Pobieramy aktualne assembly
            Assembly assembly = Assembly.GetExecutingAssembly();

            // --- FILTROWANIE TYPÓW (PKT 2, 3, 5) ---
            var targetTypes = assembly.GetTypes().Where(t =>
            {
                // PKT 2: Pobieranie typów oznaczonych atrybutem
                bool hasAttribute = t.GetCustomAttribute<PluginAttribute>() != null;

                // PKT 3: Pobieranie typów dziedziczących po klasie abstrakcyjnej
                bool isSubclass = t.IsSubclassOf(typeof(BasePlugin));

                // PKT 5: Czy typ implementuje generyczny interfejs?
                // Uwaga: IHandler<string> to zamknięty typ, a my szukamy definicji IHandler<>
                bool implementsInterface = t.GetInterfaces().Any(i =>
                    i.IsGenericType && 
                    i.GetGenericTypeDefinition() == typeof(IHandler<>)
                );

                return hasAttribute && isSubclass && implementsInterface && !t.IsAbstract;
            });

            Console.WriteLine("Znalezione pluginy:");

            foreach (Type type in targetTypes)
            {
                Console.WriteLine($"-> Przetwarzam klasę: {type.Name}");

                // Tworzymy instancję (potrzebna do SetValue)
                object instance = Activator.CreateInstance(type);

                // --- PRACA Z WŁAŚCIWOŚCIAMI (PKT 4, 6) ---
                
                // PKT 4: Pobieranie właściwości oznaczonych atrybutem
                var propsToConfigure = type.GetProperties()
                    .Where(p => p.GetCustomAttribute<DefaultValueAttribute>() != null);

                foreach (var prop in propsToConfigure)
                {
                    // Odczytujemy wartość z atrybutu
                    var attr = prop.GetCustomAttribute<DefaultValueAttribute>();
                    int valueToSet = attr.Value;

                    Console.WriteLine($"   Znaleziono właściwość '{prop.Name}' z domyślną wartością: {valueToSet}");

                    // PKT 6: Ustawienie wartości właściwości na instancji typu
                    try
                    {
                        prop.SetValue(instance, valueToSet);
                        
                        // Weryfikacja (czytamy, czy się ustawiło)
                        var currentValue = prop.GetValue(instance);
                        Console.WriteLine($"   [SUKCES] Wartość w obiekcie wynosi teraz: {currentValue}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   [BŁĄD] Nie udało się ustawić wartości: {ex.Message}");
                    }
                }
            }

            Console.ReadKey();
        }
    }
}