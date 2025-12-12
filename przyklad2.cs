using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

// --- MODELE ---
public class FragmentObrazu
{
    public int Id { get; set; }
    public int RozmiarPikseli { get; set; }
}

public class WynikAnalizy
{
    public int IdFragmentu { get; set; }
    public bool WykrytoAnomalie { get; set; }
    public long GlobalnyLicznikPikseli { get; set; }
}

// --- LOGIKA ---
public class StacjaNaziemna
{
    // Zmienna, do której będą pisać wszystkie wątki naraz.
    // Zwykłe += tutaj spowodowałoby błędy w danych (Race Condition).
    private long _calkowitaLiczbaPikseli = 0;

    // ETAP 1: Strumieniowe pobieranie (Async Stream)
    public async IAsyncEnumerable<FragmentObrazu> PobierzFragmentyAsync(
        [EnumeratorCancellation] CancellationToken token)
    {
        var random = new Random();

        for (int i = 1; i <= 50; i++)
        {
            // Zawsze sprawdzaj token przed operacją I/O
            token.ThrowIfCancellationRequested();

            // Symulacja wolnego łącza radiowego (100ms na pakiet)
            await Task.Delay(50, token); 

            yield return new FragmentObrazu
            {
                Id = i,
                RozmiarPikseli = random.Next(1000, 5000) // Losowy rozmiar danych
            };
        }
    }

    // ETAP 2: Analiza równoległa (Parallel + Interlocked)
    public async Task PrzetwarzajObrazyAsync(
        List<FragmentObrazu> fragmenty,
        IProgress<WynikAnalizy> progress,
        CancellationToken token)
    {
        // WAŻNE: Parallel jest blokujący. Musimy uciec do Task.Run, 
        // żeby nie zablokować wątku wywołującego (np. Main lub UI).
        await Task.Run(() =>
        {
            var options = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            };

            try
            {
                Parallel.ForEach(fragmenty, options, (fragment) =>
                {
                    // 1. Krytyczne sprawdzenie timeoutu wewnątrz pętli
                    options.CancellationToken.ThrowIfCancellationRequested();

                    // 2. Symulacja ciężkiej pracy CPU (analiza obrazu)
                    // Pamiętaj: W Parallel używamy Thread.Sleep, NIE await!
                    Thread.Sleep(100); 

                    // 3. Bezpieczne sumowanie (Atomic Operation)
                    // Zwykłe _calkowita += ... nie jest bezpieczne wątkowo!
                    long aktualnyStan = Interlocked.Add(ref _calkowitaLiczbaPikseli, fragment.RozmiarPikseli);

                    // 4. Raportowanie
                    // Symulujemy, że anomalia występuje w parzystych ID
                    bool anomalia = fragment.Id % 2 == 0;

                    progress?.Report(new WynikAnalizy
                    {
                        IdFragmentu = fragment.Id,
                        WykrytoAnomalie = anomalia,
                        GlobalnyLicznikPikseli = aktualnyStan
                    });
                });
            }
            catch (OperationCanceledException)
            {
                // Musimy rzucić wyjątek wyżej, żeby Main wiedział o przerwaniu
                throw;
            }
        }, token);
    }
}

// --- PROGRAM GŁÓWNY ---
class Program
{
    static async Task Main(string[] args)
    {
        var stacja = new StacjaNaziemna();

        // 1. Ustawienie TIMEOUTU na 3 sekundy
        // Satelita przelatuje szybko - musimy zdążyć.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = cts.Token;

        // 2. Konfiguracja Raportowania
        var progress = new Progress<WynikAnalizy>(w =>
        {
            string status = w.WykrytoAnomalie ? "[ANOMALIA]" : "[OK]";
            Console.WriteLine($"{status} ID: {w.IdFragmentu} | Razem pikseli: {w.GlobalnyLicznikPikseli}");
        });

        try
        {
            Console.WriteLine("--- FAZA 1: Odbieranie danych (Stream) ---");
            var bufor = new List<FragmentObrazu>();

            // Pobieramy dane dopóki są i dopóki czas pozwala
            await foreach (var fragment in stacja.PobierzFragmentyAsync(token))
            {
                bufor.Add(fragment);
                Console.Write("."); // Pasek postępu pobierania
            }

            Console.WriteLine($"\n\n--- FAZA 2: Analiza (Parallel) - Zostało mało czasu! ---");
            
            // To rzuci wyjątek, bo analiza 50 elementów * 100ms > pozostały czas
            await stacja.PrzetwarzajObrazyAsync(bufor, progress, token);

            Console.WriteLine("\nSUKCES: Wszystkie dane przetworzone przed utratą sygnału.");
        }
        catch (OperationCanceledException)
        {
            // TO JEST OCZEKIWANY REZULTAT
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n\n!!! UTRATA SYGNAŁU (TIMEOUT) !!!");
            Console.WriteLine("Satelita zniknął za horyzontem. Przetwarzanie przerwane.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd krytyczny: {ex.Message}");
        }
    }
}