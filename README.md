
# MEGA SKRYPT: PROGRAMOWANIE WSPÓŁBIEŻNE I ASYNCHRONICZNE (C#)
# Baza wiedzy na Laboratoria Mini PW

Ten dokument to kompletne kompendium. Każdy dział zawiera:
1. Teorię (jak to działa).
2. Przykład praktyczny (jak to zakodować).
3. Wyjaśnienie kluczowych mechanizmów.

---

## SPIS TREŚCI
1. [Wstęp: Task vs Thread vs Parallel](#1-wstęp)
2. [CancellationToken i Timeout (Mechanizm Anulowania)](#2-cancellationtoken)
3. [IProgress i Raportowanie Stanu (Obiekt Złożony)](#3-iprogress)
4. [Równoległość: Parallel.ForEach i Interlocked](#4-parallel)
5. [Asynchroniczne Strumienie: IAsyncEnumerable](#5-async-streams)
6. [ULTIMATE TASK: Zadanie Zaliczeniowe (Wszystko w jednym)](#6-ultimate)

---

## 1. WSTĘP: Task vs Thread vs Parallel

Zanim zaczniesz pisać kod, musisz rozróżnić dwa typy zadań, o których mówił wykładowca.

### A. I/O Bound (Zadania Wejścia/Wyjścia)
Czekanie na coś z zewnątrz: pobieranie pliku, zapytanie do bazy, czekanie na timer.
* **Co robimy:** Używamy `async` i `await`.
* **Czego NIE robimy:** Nie używamy `Parallel`, nie tworzymy nowych wątków ręcznie.
* **Kluczowa metoda:** `await Task.Delay(1000)` (zamiast `Thread.Sleep`).

### B. CPU Bound (Zadania Obliczeniowe)
Ciężka praca procesora: przetwarzanie obrazów, szyfrowanie, skomplikowane obliczenia matematyczne.
* **Co robimy:** Używamy `Parallel.ForEach` lub `Task.Run`.
* **Dlaczego:** Musimy "zepchnąć" pracę z wątku głównego (UI), żeby aplikacja nie zamarzła.

---

## 2. CANCELLATIONTOKEN (MECHANIZM ANULOWANIA)

To jest najważniejszy punkt na zaliczenie. Wykładowca wymaga obsługi **TIMEOUTU**.
System nie zabija wątku brutalnie. System prosi wątek: *"Hej, czas minął, proszę, posprzątaj i wyjdź"*.

### Jak to działa?
1.  **CancellationTokenSource (CTS):** To jest "pilot". Możesz na nim ustawić czas (`CancelAfter`) lub kliknąć guzik (`Cancel()`).
2.  **CancellationToken (Token):** To jest "sygnał" wysyłany do metody.
3.  **ThrowIfCancellationRequested():** To instrukcja wewnątrz metody: *"Sprawdź pilota. Jak wciśnięto STOP, rzuć błąd i przerwij pracę"*.

### PRZYKŁAD KODU: Metoda z Timeoutem

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

public async Task WykonajZadanieZTimeoutem()
{
    // 1. Tworzymy źródło tokena z czasem życia 3 sekundy
    // Po 3 sekundach token automatycznie zmieni stan na "Anulowany"
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    CancellationToken token = cts.Token;

    try
    {
        Console.WriteLine("Start pracy...");
        // Przekazujemy token głębiej
        await DlugaOperacjaAsync(token);
        Console.WriteLine("Koniec pracy.");
    }
    catch (OperationCanceledException)
    {
        // 2. TUTAJ TRAFIAMY W PRZYPADKU TIMEOUTU
        Console.WriteLine("BŁĄD: Przekroczono czas operacji! (Timeout)");
    }
}

public async Task DlugaOperacjaAsync(CancellationToken token)
{
    for (int i = 0; i < 10; i++)
    {
        // 3. Krytyczne sprawdzenie - czy żyjemy?
        // Jeśli minęły 3 sekundy, ta linijka wyrzuci OperationCanceledException
        token.ThrowIfCancellationRequested();

        Console.WriteLine($"Praca... {i * 10}%");

        // Przekazujemy token też do metod wbudowanych
        await Task.Delay(1000, token);
    }
}
```

---

## 3. IPROGRESS I RAPORTOWANIE STANU

Wykładowca: *"Tu będzie jakiś obiekt złożony i w nim będziemy progressować"*.
Nie wolno aktualizować UI (np. Labela czy ProgressBara) bezpośrednio z wątku roboczego (`Parallel` lub `Task.Run`). Dostaniesz błąd wątków.

### Rozwiązanie: Wzorzec Progress<T>
Klasa `Progress<T>` automatycznie "łapie" kontekst synchronizacji (SynchronizationContext) wątku głównego.

### KROK 1: Definicja Obiektu Złożonego (Model)
Nie wysyłamy samego `int`. Tworzymy klasę.

```csharp
public class StanAplikacji
{
    public int Procent { get; set; }
    public string Wiadomosc { get; set; }
    public int PrzetworzoneElementy { get; set; }
}
```

### PRZYKŁAD KODU: Raportowanie

```csharp
// --- KOD W UI (MAIN) ---
public async Task UruchomRaportowanie()
{
    // Tworzymy "odbiornik" wiadomości. To wykonuje się na wątku UI.
    var progress = new Progress<StanAplikacji>(raport =>
    {
        // Bezpieczna aktualizacja interfejsu
        Console.WriteLine($"[UI] Postęp: {raport.Procent}% | Info: {raport.Wiadomosc}");
    });

    await WykonajPrace(progress);
}

// --- KOD ROBOCZY (BACKEND) ---
// Przyjmujemy interfejs IProgress, nie konkretną klasę Progress!
public async Task WykonajPrace(IProgress<StanAplikacji> progress)
{
    await Task.Run(() =>
    {
        var stan = new StanAplikacji();

        for (int i = 0; i <= 100; i += 20)
        {
            Thread.Sleep(500); // Symulacja pracy

            // Aktualizacja obiektu
            stan.Procent = i;
            stan.Wiadomosc = i < 100 ? "Przetwarzanie..." : "Zakończono";
            stan.PrzetworzoneElementy = i / 10;

            // Wysłanie raportu (zawsze sprawdzaj null!)
            progress?.Report(stan);
        }
    });
}
```

---

## 4. RÓWNOLEGŁOŚĆ: PARALLEL.FOR I INTERLOCKED

Służy do wykorzystania wszystkich rdzeni procesora.
**Ważne:** `Parallel.ForEach` jest **blokujący**. To znaczy, że kod zatrzyma się na tej linii, dopóki wszystkie wątki nie skończą. Dlatego w aplikacjach okienkowych zawsze pakujemy go w `Task.Run`.

### Kluczowe elementy:
1.  **ParallelOptions:** Tutaj wkładamy `CancellationToken`.
2.  **Brak `await`:** W środku lambdy Parallel używamy kodu synchronicznego (`Thread.Sleep`), a nie asynchronicznego.
3.  **Interlocked:** Jeśli wiele wątków chce zmienić tę samą zmienną (np. licznik), musisz użyć `Interlocked.Increment`, inaczej wyniki będą błędne (Race Condition).

### PRZYKŁAD KODU: Parallel Processing

```csharp
public async Task PrzetwarzanieRownolegle(List<int> dane, CancellationToken token)
{
    // Ucieczka z wątku UI
    await Task.Run(() =>
    {
        var opcje = new ParallelOptions
        {
            CancellationToken = token,          // Obsługa anulowania
            MaxDegreeOfParallelism = 4          // (Opcjonalne) Limit wątków
        };

        int globalnyLicznik = 0; // Wspólna zmienna dla wszystkich wątków

        try
        {
            // Pętla Parallel.ForEach
            Parallel.ForEach(dane, opcje, (liczba) =>
            {
                // 1. Sprawdzamy token wewnątrz pętli!
                opcje.CancellationToken.ThrowIfCancellationRequested();

                // 2. Symulacja ciężkiej pracy (CPU)
                Thread.Sleep(500); 

                // 3. Bezpieczne zwiększanie licznika
                // Zwykłe "globalnyLicznik++" byłoby błędem!
                Interlocked.Increment(ref globalnyLicznik);

                Console.WriteLine($"Wątek {Task.CurrentId} przetworzył liczbę {liczba}");
            });
        }
        catch (OperationCanceledException)
        {
            // Musimy rzucić wyjątek dalej, żeby zatrzymać Task.Run
            throw;
        }
    }, token);
}
```

---

## 5. ASYNCHRONICZNE STRUMIENIE: IAsyncEnumerable

Wykładowca: *"Asynchroniczne sekwencje - 1 etap"*.
To nowoczesny sposób na zwracanie kolekcji danych, które "spływają" powoli (np. są pobierane strona po stronie). Zamiast czekać na całą listę, dostajesz elementy jeden po drugim.

### Słowa kluczowe:
* `yield return`: Zwraca element i "zawiesza" metodę do następnego razu.
* `[EnumeratorCancellation]`: Atrybut konieczny, by `CancellationToken` działał w pętli `foreach`.

### PRZYKŁAD KODU: Strumień

```csharp
using System.Runtime.CompilerServices;

// PRODUCENT DANYCH
public async IAsyncEnumerable<int> GenerujDaneAsync(
    [EnumeratorCancellation] CancellationToken token)
{
    for (int i = 0; i < 10; i++)
    {
        // Sprawdź czy nie anulowano przed czekaniem
        token.ThrowIfCancellationRequested();

        // Symulacja pobierania danych (I/O)
        await Task.Delay(300, token);

        yield return i; // "Wypluj" liczbę i czekaj dalej
    }
}

// KONSUMENT DANYCH
public async Task OdbierzDane(CancellationToken token)
{
    // WAŻNE: await przed foreach!
    await foreach (var liczba in GenerujDaneAsync(token))
    {
        Console.WriteLine($"Odebrano: {liczba}");
    }
}
```

---

## 6. ULTIMATE: ZADANIE ZALICZENIOWE (WSZYSTKO RAZEM)

To jest scenariusz "Hardkorowy", który łączy wszystkie powyższe punkty. Jeśli to zrozumiesz, zdasz.

**Scenariusz:**
1.  Pobierz listę 20 IDków strumieniowo (`IAsyncEnumerable`).
2.  Przetwórz je równolegle (`Parallel`), licząc ich potęgi.
3.  Raportuj postęp obiektem złożonym (`IProgress`).
4.  Całość ma Timeout 4 sekundy. Jeśli przekroczy -> Błąd.

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

// 1. MODEL DANYCH (OBIEKT ZŁOŻONY)
public class RaportZadania
{
    public int Id { get; set; }
    public double Wynik { get; set; }
    public int ProcentUkonczenia { get; set; }
    public string Status { get; set; }
}

public class LaboratoriumLogic
{
    // 2. ETAP I: POBIERANIE (ASYNC STREAMS)
    public async IAsyncEnumerable<int> PobierzIdentyfikatoryAsync(
        [EnumeratorCancellation] CancellationToken token)
    {
        for (int i = 1; i <= 20; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(100, token); // Szybkie I/O
            yield return i;
        }
    }

    // 3. ETAP II: PRZETWARZANIE (PARALLEL)
    public async Task PrzetwarzajRownolegleAsync(
        List<int> dane,
        IProgress<RaportZadania> progress,
        CancellationToken token)
    {
        await Task.Run(() =>
        {
            var options = new ParallelOptions 
            { 
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount 
            };

            int licznikPostepu = 0;

            try
            {
                Parallel.ForEach(dane, options, (id) =>
                {
                    // A. Sprawdź anulowanie
                    options.CancellationToken.ThrowIfCancellationRequested();

                    // B. Symulacja CPU (Ciężka praca)
                    Thread.Sleep(300); 

                    // C. Logika
                    double wynik = Math.Pow(id, 2);

                    // D. Aktualizacja licznika (Interlocked!)
                    int zrobione = Interlocked.Increment(ref licznikPostepu);
                    
                    // E. Raportowanie
                    var raport = new RaportZadania
                    {
                        Id = id,
                        Wynik = wynik,
                        ProcentUkonczenia = (zrobione * 100) / dane.Count,
                        Status = "Przetworzono"
                    };
                    progress?.Report(raport);
                });
            }
            catch (OperationCanceledException)
            {
                throw; // Przekaż anulowanie wyżej
            }
        }, token);
    }
}

// 4. PROGRAM GŁÓWNY (MAIN / BUTTON CLICK)
public static async Task Main()
{
    var lab = new LaboratoriumLogic();

    // --- KONFIGURACJA TIMEOUTU (4 sekundy) ---
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
    CancellationToken token = cts.Token;

    // --- KONFIGURACJA PROGRESSU ---
    var progressHandler = new Progress<RaportZadania>(r =>
    {
        Console.Write($"\r[{r.ProcentUkonczenia}%] ID:{r.Id} => {r.Wynik}   ");
    });

    try
    {
        Console.WriteLine("Start pobierania...");
        var bufor = new List<int>();

        await foreach(var id in lab.PobierzIdentyfikatoryAsync(token))
        {
            bufor.Add(id);
        }
        Console.WriteLine($"\nPobrano {bufor.Count} elementów. Start obliczeń...");

        // Start Parallel
        await lab.PrzetwarzajRownolegleAsync(bufor, progressHandler, token);

        Console.WriteLine("\nSUKCES!");
    }
    catch (OperationCanceledException)
    {
        // --- OCZEKIWANY REZULTAT PRZY TIMEOUT ---
        Console.WriteLine("\n\n!!! TIMEOUT: Operacja trwała zbyt długo !!!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nBłąd: {ex.Message}");
    }
}
