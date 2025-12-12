using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices; // Potrzebne do [EnumeratorCancellation]

// 1. OBIEKT ZŁOŻONY (Model danych do raportowania)
public class RaportAnalizy
{
    public int Id { get; set; }
    public string PoziomZagrozenia { get; set; } // np. INFO, ERROR
    public string Tresc { get; set; }
}

public class SystemLogow
{
    // 2. ASYNCHRONICZNE SEKWENCJE (Symulacja wolnego pobierania)
    public async IAsyncEnumerable<string> PobierzLogiAsync([EnumeratorCancellation] CancellationToken token)
    {
        for (int i = 1; i <= 20; i++)
        {
            // Sprawdzenie anulowania
            token.ThrowIfCancellationRequested();

            // Symulacja opóźnienia sieci (300ms)
            await Task.Delay(300, token);

            yield return $"Log_systemowy_nr_{i}_data_{DateTime.Now.Ticks}";
        }
    }

    // 3. PARALLEL + TASK + IPROGRESS (Główne przetwarzanie)
    public async Task PrzetwarzajLogiAsync(
        List<string> suroweLogi, 
        IProgress<RaportAnalizy> progress, 
        CancellationToken token)
    {
        // Pakujemy Parallel w Task.Run, żeby nie zablokować wątku wywołującego
        await Task.Run(() =>
        {
            var options = new ParallelOptions 
            { 
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            };

            try
            {
                Parallel.ForEach(suroweLogi, options, (log, state, index) =>
                {
                    // a) Sprawdzamy token (bardzo ważne w pętlach!)
                    options.CancellationToken.ThrowIfCancellationRequested();

                    // b) Symulacja ciężkiej pracy CPU (analiza tekstu) - Thread.Sleep!
                    Thread.Sleep(600); 

                    // c) Logika biznesowa (fikcyjna analiza)
                    // Jeśli długość napisu jest parzysta -> CRITICAL, nieparzysta -> INFO
                    string poziom = (log.Length % 2 == 0) ? "CRITICAL" : "INFO";

                    // d) Tworzenie obiektu raportu
                    var raport = new RaportAnalizy
                    {
                        Id = (int)index,
                        PoziomZagrozenia = poziom,
                        Tresc = $"Przeanalizowano: {log}"
                    };

                    // e) Raportowanie postępu
                    progress?.Report(raport);
                });
            }
            catch (OperationCanceledException)
            {
                // Musimy rzucić wyjątek dalej, żeby Main wiedział o przerwaniu
                throw;
            }
        }, token);
    }
}

// 4. PROGRAM GŁÓWNY
class Program
{
    static async Task Main(string[] args)
    {
        var system = new SystemLogow();
        
        // --- KONFIGURACJA TIMEOUTU (4 sekundy na wszystko) ---
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        CancellationToken token = cts.Token;

        // --- KONFIGURACJA ODBIORU POSTĘPU ---
        var progressHandler = new Progress<RaportAnalizy>(raport =>
        {
            // Zmiana koloru w zależności od statusu (bajer wizualny)
            if (raport.PoziomZagrozenia == "CRITICAL") Console.ForegroundColor = ConsoleColor.Red;
            else Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine($"[{raport.PoziomZagrozenia}] ID:{raport.Id} -> {raport.Tresc}");
            Console.ResetColor();
        });

        try
        {
            Console.WriteLine("1. Rozpoczynam pobieranie logów (Stream)...");
            var listaLogow = new List<string>();

            // Pobieramy dane ze strumienia
            await foreach (var log in system.PobierzLogiAsync(token))
            {
                listaLogow.Add(log);
                Console.WriteLine($"   -> Pobrano: {log}");
            }

            Console.WriteLine($"\n2. Pobrano {listaLogow.Count} logów. Start analizy równoległej...");
            
            // Uruchamiamy przetwarzanie
            await system.PrzetwarzajLogiAsync(listaLogow, progressHandler, token);

            Console.WriteLine("\nSUKCES: Wszystkie logi przeanalizowane!");
        }
        catch (OperationCanceledException)
        {
            // --- TO JEST MOMENT, KTÓREGO SZUKA WYKŁADOWCA ---
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("\n!!! WYJĄTEK: CZAS PRZEKROCZONY (TIMEOUT) !!!");
            Console.ResetColor();
            Console.WriteLine("Zadanie trwało zbyt długo i zostało anulowane przez Token.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wystąpił inny błąd: {ex.Message}");
        }

        Console.WriteLine("Koniec programu.");
        Console.ReadKey();
    }
}