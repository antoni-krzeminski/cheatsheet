]
```markdown
# 🎓 C# Networking Practice: Zadania z Rozwiązaniami

Ten dokument zawiera zestaw zadań ćwiczeniowych z zakresu programowania sieciowego TCP/IP w C#.
Każde zadanie skupia się na innym aspekcie: obsługa DNS, serializacja JSON oraz czysta serializacja binarna.

## 🛠️ Wymagane Namespace'y
Do wszystkich poniższych rozwiązań wymagane są te biblioteki:

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Buffers.Binary; // Ważne dla Big Endian
using System.Collections.Generic;
using Newtonsoft.Json;       // NuGet: Newtonsoft.Json

```

---

## 📝 Zadanie 1: Inteligentny Łącznik (DNS & IP)

**Treść zadania:**
Napisz metodę `ConnectAsync`, która przyjmuje adres serwera (jako `string`) oraz port.

1. Metoda musi obsługiwać zarówno surowe IP (np. "127.0.0.1") jak i nazwy domenowe (np. "localhost", "https://www.google.com/search?q=google.com").
2. Połączenie musi zostać przerwane (timeout), jeśli nie uda się nawiązać go w ciągu **3 sekund**.
3. W przypadku błędu zwróć `null` i wypisz komunikat.

### ✅ Rozwiązanie

```csharp
public async Task<TcpClient> ConnectWithTimeoutAsync(string host, int port)
{
    TcpClient client = new TcpClient();

    // CancellationTokenSource z timeoutem 3 sekundy
    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
    {
        try
        {
            Console.WriteLine($"🔍 Rozwiązywanie adresu: {host}...");
            
            // Dns.GetHostAddressesAsync automatycznie obsługuje IP i nazwy DNS
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
            IPAddress targetIp = addresses[0];

            Console.WriteLine($"🚀 Łączenie z {targetIp}:{port}...");
            
            // Przekazujemy cts.Token, aby przerwać w razie upływu czasu
            await client.ConnectAsync(targetIp, port, cts.Token);
            
            Console.WriteLine("✅ Połączono!");
            return client;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("❌ Błąd: Timeout połączenia (3s).");
            client.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd połączenia: {ex.Message}");
            client.Dispose();
            return null;
        }
    }
}

```

---

## 📝 Zadanie 2: Wysyłanie Raportu (JSON Serialization)

**Treść zadania:**
Masz klasę `WeatherReport`:

```csharp
public class WeatherReport
{
    public string City { get; set; }
    public double Temperature { get; set; }
    public DateTime Date { get; set; }
}

```

Napisz metodę `SendReportAsync`, która:

1. Zserializuje obiekt do formatu **JSON**.
2. Zakoduje JSON do bajtów **UTF-8**.
3. Wyśle do strumienia ramkę w formacie: `[4 bajty długości Big Endian] + [Treść JSON]`.
4. Rzuci wyjątek, jeśli wiadomość przekracza 5KB.

### ✅ Rozwiązanie

```csharp
public async Task SendReportAsync(NetworkStream stream, WeatherReport report)
{
    // 1. Serializacja do JSON
    string json = JsonConvert.SerializeObject(report);
    
    // 2. Kodowanie do bajtów
    byte[] payload = Encoding.UTF8.GetBytes(json);
    int length = payload.Length;

    // Walidacja rozmiaru
    if (length > 5120) throw new Exception("Wiadomość za długa (>5KB)!");

    // 3. Przygotowanie nagłówka (4 bajty, Big Endian)
    byte[] header = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(header, length);

    // 4. Wysłanie (Nagłówek + Dane)
    await stream.WriteAsync(header, 0, 4);
    await stream.WriteAsync(payload, 0, length);
    
    // Dobra praktyka: Flush
    await stream.FlushAsync(); 
    Console.WriteLine($"📤 Wysłano raport JSON ({length} bajtów).");
}

```

---

## 📝 Zadanie 3: Odbieranie Wiadomości (Pętla doczytująca + Deserializacja)

**Treść zadania:**
Napisz generyczną metodę `ReadMessageAsync<T>`, która odczyta wiadomość wysłaną w formacie z Zadania 2.
**Wymagania krytyczne:**

1. Musisz użyć pętli do doczytania dokładnej liczby bajtów (TCP może dzielić pakiety!).
2. Musisz odczytać najpierw 4 bajty długości, a potem treść.
3. Zdeserializuj treść z JSON do obiektu typu `T`.

### ✅ Rozwiązanie

**Helper (Kluczowy element):**

```csharp
private async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count)
{
    byte[] buffer = new byte[count];
    int totalRead = 0;
    while (totalRead < count)
    {
        int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
        if (read == 0) return null; // Zerwane połączenie
        totalRead += read;
    }
    return buffer;
}

```

**Metoda Główna:**

```csharp
public async Task<T> ReadMessageAsync<T>(NetworkStream stream)
{
    // 1. Odczyt nagłówka (4 bajty)
    byte[] header = await ReadExactlyAsync(stream, 4);
    if (header == null) return default(T); // Koniec strumienia

    // 2. Konwersja Big Endian -> int
    int length = BinaryPrimitives.ReadInt32BigEndian(header);

    // 3. Odczyt treści (dokładnie tyle bajtów, ile wskazał nagłówek)
    byte[] payload = await ReadExactlyAsync(stream, length);
    if (payload == null) throw new EndOfStreamException("Urwano dane w połowie.");

    // 4. Deserializacja JSON
    string json = Encoding.UTF8.GetString(payload);
    Console.WriteLine($"📥 Odebrano JSON: {json}");
    
    return JsonConvert.DeserializeObject<T>(json);
}

```

---

## 📝 Zadanie 4: Protokół Binarny (Optymalizacja)

**Treść zadania:**
System wymaga maksymalnej wydajności. Zamiast JSON, musisz wysłać dane gracza binarnie (bez nazw pól, same wartości).
Klasa:

```csharp
public class PlayerStats
{
    public int PlayerId { get; set; }
    public bool IsOnline { get; set; }
    public float Health { get; set; }
}

```

Napisz metodę `SendBinaryAsync`, która użyje `BinaryWriter` do zapisania pól w kolejności: ID -> IsOnline -> Health. Całość poprzedź standardowym nagłówkiem długości (4 bajty Big Endian).

### ✅ Rozwiązanie

```csharp
public async Task SendBinaryAsync(NetworkStream stream, PlayerStats stats)
{
    // Używamy MemoryStream jako bufora, aby poznać długość całej paczki
    using (var ms = new MemoryStream())
    using (var writer = new BinaryWriter(ms))
    {
        // 1. Zapisywanie pól (kolejność jest święta!)
        writer.Write(stats.PlayerId);   // int (4 bajty)
        writer.Write(stats.IsOnline);   // bool (1 bajt)
        writer.Write(stats.Health);     // float (4 bajty)

        // Pobierz gotową tablicę bajtów
        byte[] payload = ms.ToArray();
        int length = payload.Length;

        // 2. Nagłówek długości (Big Endian)
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);

        // 3. Wysłanie do sieci
        await stream.WriteAsync(header, 0, 4);      // Nagłówek
        await stream.WriteAsync(payload, 0, length); // Dane
        await stream.FlushAsync();
        
        Console.WriteLine($"📤 Wysłano dane binarne ({length} bajtów).");
    }
}

```

### 💡 Jak to odebrać? (Dla kompletności)

```csharp
public async Task<PlayerStats> ReadBinaryAsync(NetworkStream stream)
{
    // Nagłówek...
    byte[] header = await ReadExactlyAsync(stream, 4);
    if (header == null) return null;
    int length = BinaryPrimitives.ReadInt32BigEndian(header);

    // Treść...
    byte[] payload = await ReadExactlyAsync(stream, length);
    
    // Odczyt z pamięci (BinaryReader)
    using (var ms = new MemoryStream(payload))
    using (var reader = new BinaryReader(ms))
    {
        var stats = new PlayerStats();
        // KOLEJNOŚĆ MUSI BYĆ TAKA SAMA JAK PRZY ZAPISIE!
        stats.PlayerId = reader.ReadInt32();
        stats.IsOnline = reader.ReadBoolean();
        stats.Health = reader.ReadSingle(); // ReadSingle to float
        return stats;
    }
}

```

---

## 🚀 Uruchomienie Testowe (Main)

Przykładowy kod, który spina to w całość (możesz wkleić do `Program.cs`):

```csharp
public static async Task Main()
{
    // Uruchom najpierw nasłuch (np. netcat lub własny serwer) na porcie 5000
    // albo połącz się z localhostem jeśli masz serwer w tle.
    
    TcpClient client = await ConnectWithTimeoutAsync("localhost", 5000);
    if (client != null)
    {
        NetworkStream stream = client.GetStream();

        // Test 1: JSON
        var report = new WeatherReport { City = "Warsaw", Temperature = 23.5, Date = DateTime.Now };
        await SendReportAsync(stream, report);

        // Test 2: Binary
        var player = new PlayerStats { PlayerId = 99, IsOnline = true, Health = 100.0f };
        await SendBinaryAsync(stream, player);

        client.Close();
    }
}

```

```

Czy chciałbyś, abym wygenerował dla Ciebie teraz pusty szablon projektu (np. strukturę plików), czy to Ci wystarczy?

```
