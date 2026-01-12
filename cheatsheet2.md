

# 📄 Cheatsheet: TCP/IP, JSON & Binary Streams (C#)

## ⚡ Szybki Start: Wymagane Przestrzenie Nazw

Na początku pliku zawsze upewnij się, że masz te usingi:

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Buffers.Binary; // KLUCZOWE dla Big Endian!
using Newtonsoft.Json;       // Wymagane przez instrukcję

```

---

## 1. Nawiązywanie Połączenia (Klient)

**Wymagania:** Obsługa IP lub DNS + Timeout 3 sekundy.

### Template: `ConnectAsync` z Timeoutem

Ten kod obsłuży zarówno adres IP ("127.0.0.1") jak i nazwę hosta ("localhost").

```csharp
public async Task<TcpClient> ConnectToServerAsync(string address, int port)
{
    TcpClient client = new TcpClient();
    
    // 1. Rozwiązywanie adresu (DNS lub IP)
    // Dns.GetHostAddressesAsync obsłuży i "localhost" i "192.168.0.1"
    IPAddress[] ips = await Dns.GetHostAddressesAsync(address);
    IPAddress targetIp = ips[0];

    // 2. Timeout 3 sekundy (CancellationTokenSource)
    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
    {
        try
        {
            // Przekazujemy token anulowania
            await client.ConnectAsync(targetIp, port, cts.Token);
            Console.WriteLine($"Połączono z {targetIp}:{port}");
            return client;
        }
        catch (OperationCanceledException)
        {
            // Timeout zadziałał
            client.Dispose(); // Ważne: posprzątaj po sobie
            Console.WriteLine("Błąd: Przekroczono limit czasu połączenia (3s).");
            return null; 
        }
        catch (Exception ex)
        {
            client.Dispose();
            Console.WriteLine($"Błąd połączenia: {ex.Message}");
            return null;
        }
    }
}

```

---

## 2. Pisanie Wiadomości (Serializacja + Binarny Nagłówek)

**Protokół:** [Nagłówek 4 bajty Big Endian] + [Treść JSON UTF-8].
**Limit:** Max 10kB (10240 bajtów).

### Template: `WriteMessageAsync`

```csharp
public async Task WriteMessageAsync(NetworkStream stream, object messageObj)
{
    // KROK 1: Serializacja do JSON (Newtonsoft)
    string json = JsonConvert.SerializeObject(messageObj);
    
    // KROK 2: Kodowanie do UTF-8
    byte[] messageBytes = Encoding.UTF8.GetBytes(json);
    int length = messageBytes.Length;

    // KROK 3: Walidacja długości (Limit 10KB)
    if (length > 10240)
    {
        throw new TooLongMessageException($"Wiadomość za długa: {length} bajtów (max 10240).");
    }

    // KROK 4: Tworzenie nagłówka (Big Endian Int32)
    byte[] lengthHeader = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(lengthHeader, length);

    // KROK 5: Wysłanie do strumienia (Najpierw długość, potem treść)
    // Wysyłamy wszystko naraz dla wydajności, albo w dwóch rzutach
    await stream.WriteAsync(lengthHeader, 0, 4);
    await stream.WriteAsync(messageBytes, 0, length);
    
    // Opcjonalnie flush, żeby wypchnąć dane natychmiast
    // await stream.FlushAsync(); 
}

```

---

## 3. Czytanie Wiadomości (Pętla doczytująca + Deserializacja)

**Ważne:** Metoda `Read` **nie gwarantuje** odczytania tylu bajtów, ile chcesz. Musisz użyć pętli!
**Wymagania:** Zwróć `null` jeśli koniec strumienia. Rzuć `InvalidMessageException` przy błędzie JSON.

### Helper: Pętla doczytująca (Crucial!)

Skopiuj tę metodę pomocniczą, uratuje Ci życie na kolokwium. Gwarantuje pobranie `count` bajtów.

```csharp
private async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count)
{
    byte[] buffer = new byte[count];
    int totalRead = 0;
    
    while (totalRead < count)
    {
        // Czytamy tylko tyle, ile brakuje (count - totalRead)
        int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
        
        if (read == 0) 
        {
            // Strumień zamknięty przez drugą stronę
            return null; 
        }
        totalRead += read;
    }
    return buffer;
}

```

### Template: `ReadMessageAsync`

```csharp
public async Task<T> ReadMessageAsync<T>(NetworkStream stream)
{
    // KROK 1: Odczyt nagłówka (4 bajty)
    byte[] lengthHeader = await ReadExactlyAsync(stream, 4);
    if (lengthHeader == null) return default(T); // Koniec połączenia

    // KROK 2: Konwersja nagłówka (Big Endian -> int)
    int length = BinaryPrimitives.ReadInt32BigEndian(lengthHeader);

    // KROK 3: Walidacja długości (przed alokacją bufora!)
    if (length > 10240)
    {
        // Opcjonalnie: wyczyść strumień lub zamknij połączenie
        throw new TooLongMessageException($"Otrzymano nagłówek z długością {length}. Max 10kB.");
    }

    // KROK 4: Odczyt treści właściwej (body)
    byte[] messageBytes = await ReadExactlyAsync(stream, length);
    if (messageBytes == null) throw new EndOfStreamException("Urwano połączenie w trakcie czytania treści.");

    // KROK 5: Deserializacja JSON
    try 
    {
        string json = Encoding.UTF8.GetString(messageBytes);
        return JsonConvert.DeserializeObject<T>(json);
    }
    catch (JsonException ex) // Błąd formatu JSON
    {
        throw new InvalidMessageException("Otrzymano niepoprawny JSON.", ex);
    }
}

```

---

## 4. Wyjątki (Wymagane przez zadanie)

Pamiętaj, aby zdefiniować klasy wyjątków, jeśli nie ma ich w kodzie startowym.

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

## 5. Serwer: Pętla główna (Loop)

Obsługa wielu klientów, `TcpListener` i `CancellationToken`.

```csharp
public async Task RunServerAsync(int port, CancellationToken token)
{
    TcpListener listener = new TcpListener(IPAddress.Any, port);
    listener.Start();
    Console.WriteLine($"Serwer nasłuchuje na porcie {port}...");

    try
    {
        // Pętla nasłuchująca nowych klientów
        while (!token.IsCancellationRequested)
        {
            // Oczekiwanie na klienta
            // Użycie tokena przy AcceptTcpClient jest trudne bezpośrednio w starszym .NET,
            // ale można sprawdzić token w pętli lub użyć obejścia z Task.WaitAny.
            if (listener.Pending()) 
            {
                 TcpClient client = await listener.AcceptTcpClientAsync();
                 // Uruchom obsługę klienta w tle (nie blokuj pętli!)
                 _ = HandleClientAsync(client, token);
            }
            else 
            {
                await Task.Delay(100); // Małe opóźnienie, żeby nie spalić CPU
            }
        }
    }
    finally
    {
        listener.Stop();
    }
}

```

---

## ⚠️ Najczęstsze Pułapki (Checklista)

1. **Big Endian:** Czy użyłeś `BinaryPrimitives`? Jeśli użyjesz `BitConverter.GetBytes()`, na procesorach Intel (Little Endian) wyślesz bajty w odwrotnej kolejności i serwer odczyta kosmiczną długość (np. zamiast 5 odczyta 83886080).
2. **Pętla przy Read:** Czy użyłeś pętli `while(total < expected)`? Pojedynczy `stream.Read` to za mało!
3. **UTF-8:** JSON musi być kodowany w UTF-8 (`Encoding.UTF8`).
4. **Zwalnianie zasobów:** Pamiętaj o `using` lub `client.Close()` / `client.Dispose()`.
5. **Parsowanie IP:** Jeśli użytkownik wpisze "localhost", `IPAddress.Parse("localhost")` wyrzuci błąd. Użyj `Dns.GetHostAddressesAsync`.

---

## Przydatne polecenia (Terminal)

* **Sprawdzenie IP (Windows):** `ipconfig`
* **Sprawdzenie IP (Linux/Mac):** `ip a` lub `ifconfig`
* **Test połączenia (Telnet):** `telnet <ip> <port>` (jeśli serwer działa, ekran zrobi się czarny lub zobaczysz kursor).
