# 📄 Ultimate Cheatsheet: TCP/IP, JSON & Binary (C#)

## 0. Wymagane Przestrzenie Nazw

Skopiuj to na samą górę.
**Ważne:** Upewnij się, że masz paczkę NuGet: `Newtonsoft.Json`.

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.IO;
using System.Buffers.Binary; // Wymagane dla Big Endian!
using System.Collections.Generic;
using Newtonsoft.Json;       // Wymagane dla JSON

```

---

## 1. Wyjątki (Wklej to gdzieś na dole klasy)

Dobre praktyki wymagają własnych wyjątków.

```csharp
public class TooLongMessageException : Exception
{
    public TooLongMessageException(string message) : base(message) { }
}

public class InvalidMessageException : Exception
{
    public InvalidMessageException(string message) : base(message) { }
    public InvalidMessageException(string message, Exception inner) : base(message, inner) { }
}

```

---

## 2. Klient: Nawiązywanie Połączenia (IP lub DNS)

**Poprawka:** `try-catch` obejmuje teraz też rozwiązywanie DNS, więc jak wpiszesz głupi adres, program się nie wywali.

```csharp
public async Task<TcpClient> ConnectToServerAsync(string address, int port)
{
    TcpClient client = new TcpClient();

    // Timeout 3 sekundy na połączenie
    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
    {
        try
        {
            // 1. Rozwiązywanie adresu (obsługuje "localhost" i "192.168.x.x")
            IPAddress[] ips = await Dns.GetHostAddressesAsync(address);
            IPAddress targetIp = ips[0];

            // 2. Łączenie z tokenem anulowania
            await client.ConnectAsync(targetIp, port, cts.Token);
            Console.WriteLine($"Połączono z {targetIp}:{port}");
            return client;
        }
        catch (Exception ex)
        {
            client.Dispose(); // Sprzątamy po nieudanej próbie
            Console.WriteLine($"Błąd połączenia: {ex.Message}");
            return null; 
        }
    }
}

```

---

## 3. Helper: Pętla doczytująca (CRITICAL!)

Bez tej metody nie zdasz. TCP może pociąć wiadomość na kawałki. Ta metoda skleja je z powrotem.

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

---

## 4. SCENARIUSZ A: Wiadomości JSON (Tekstowe)

Użyj tego, gdy zadanie mówi: *"Prześlij obiekt klasy X jako JSON"*.

### Pisanie (JSON)

```csharp
public async Task WriteJsonMessageAsync(NetworkStream stream, object messageObj)
{
    // 1. Serializacja
    string json = JsonConvert.SerializeObject(messageObj);
    byte[] payload = Encoding.UTF8.GetBytes(json);
    int length = payload.Length;

    if (length > 10240) throw new TooLongMessageException("Za długa wiadomość!");

    // 2. Nagłówek długości (Big Endian)
    byte[] header = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(header, length);

    // 3. Wysłanie
    await stream.WriteAsync(header, 0, 4);
    await stream.WriteAsync(payload, 0, length);
    await stream.FlushAsync(); // Wypchnięcie danych
}

```

### Czytanie (JSON)

```csharp
public async Task<T> ReadJsonMessageAsync<T>(NetworkStream stream)
{
    // 1. Nagłówek
    byte[] header = await ReadExactlyAsync(stream, 4);
    if (header == null) return default(T); // Koniec strumienia

    int length = BinaryPrimitives.ReadInt32BigEndian(header);
    if (length > 10240) throw new TooLongMessageException($"Nagłówek wskazuje {length} bajtów.");

    // 2. Treść
    byte[] payload = await ReadExactlyAsync(stream, length);
    if (payload == null) throw new EndOfStreamException("Urwano połączenie.");

    // 3. Deserializacja
    try 
    {
        string json = Encoding.UTF8.GetString(payload);
        return JsonConvert.DeserializeObject<T>(json);
    }
    catch (JsonException ex)
    {
        throw new InvalidMessageException("Błędny format JSON", ex);
    }
}

```

---

## 5. SCENARIUSZ B: Wiadomości Binarne (Raw Bytes)

Użyj tego, gdy zadanie mówi: *"Prześlij int, potem bool, a potem string binarnie"* (bez formatowania JSON).

### Przykład klasy danych

```csharp
public class DaneBinarne {
    public int Liczba { get; set; }
    public bool Flaga { get; set; }
    public string Tekst { get; set; }
}

```

### Pisanie (BinaryWriter)

```csharp
public async Task WriteBinaryMessageAsync(NetworkStream stream, DaneBinarne dane)
{
    // Używamy MemoryStream, aby obliczyć długość całej paczki przed wysłaniem nagłówka
    using (var ms = new MemoryStream())
    using (var writer = new BinaryWriter(ms, Encoding.UTF8))
    {
        // Kolejność zapisu jest KLUCZOWA!
        writer.Write(dane.Liczba); // 4 bajty
        writer.Write(dane.Flaga);  // 1 bajt
        writer.Write(dane.Tekst);  // Długość stringa + bajty stringa

        byte[] payload = ms.ToArray();
        int length = payload.Length;

        // Nagłówek długości dla protokołu TCP
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);

        // Wysyłamy nagłówek + dane binarne
        await stream.WriteAsync(header, 0, 4);
        await stream.WriteAsync(payload, 0, length);
        await stream.FlushAsync();
    }
}

```

### Czytanie (BinaryReader)

```csharp
public async Task<DaneBinarne> ReadBinaryMessageAsync(NetworkStream stream)
{
    // 1. Odczyt nagłówka długości
    byte[] header = await ReadExactlyAsync(stream, 4);
    if (header == null) return null;

    int length = BinaryPrimitives.ReadInt32BigEndian(header);

    // 2. Odczyt surowych danych do bufora
    byte[] payload = await ReadExactlyAsync(stream, length);
    if (payload == null) throw new EndOfStreamException();

    // 3. Deserializacja z pamięci
    using (var ms = new MemoryStream(payload))
    using (var reader = new BinaryReader(ms, Encoding.UTF8))
    {
        var wynik = new DaneBinarne();
        // Kolejność ODCZYTU musi być identyczna jak ZAPISU!
        wynik.Liczba = reader.ReadInt32();
        wynik.Flaga = reader.ReadBoolean();
        wynik.Tekst = reader.ReadString();
        return wynik;
    }
}

```

---

## 6. Serwer: Główna pętla

Szablon obsługujący wielu klientów asynchronicznie.

```csharp
public async Task RunServerAsync(int port, CancellationToken token)
{
    TcpListener listener = new TcpListener(IPAddress.Any, port);
    listener.Start();
    Console.WriteLine($"[Serwer] Start na porcie {port}");

    try
    {
        while (!token.IsCancellationRequested)
        {
            // Oczekiwanie na klienta
            if (listener.Pending())
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                // Ważne: _ = Handle... uruchamia zadanie w tle (fire and forget)
                _ = HandleClientAsync(client);
            }
            else
            {
                await Task.Delay(50); // Odciążenie CPU
            }
        }
    }
    finally
    {
        listener.Stop();
    }
}

private async Task HandleClientAsync(TcpClient client)
{
    using (client)
    using (NetworkStream stream = client.GetStream())
    {
        Console.WriteLine("Klient podłączony.");
        try
        {
            // TU UŻYWASZ ReadJsonMessageAsync LUB ReadBinaryMessageAsync
            // np.:
            // var msg = await ReadJsonMessageAsync<MojaKlasa>(stream);
            // Console.WriteLine(msg.Pole);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd klienta: {ex.Message}");
        }
    }
}

```
