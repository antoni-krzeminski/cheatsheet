

```markdown
# 🎮 Cheatsheet: P3 Network Game Client (TCP/JSON)

Ten dokument zawiera gotowe rozwiązania na 4 etapy zadania laboratoryjnego.
**Scenariusz:** Klient łączy się z serwerem gry, wymieniając komunikaty w formacie:
[cite_start]`[NAGŁÓWEK: 4 bajty długości (BigEndian)]` + `[PAYLOAD: JSON (UTF-8)]`[cite: 45, 46].

## 📦 0. Niezbędne Importy
Wklej to na samej górze pliku `Program.cs` lub klasy obsługującej sieć.

```csharp
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Buffers.Binary; // Kluczowe dla BigEndian
using Newtonsoft.Json;       [cite_start]// Kluczowe dla JSON [cite: 57]

```

---

## 🛠️ 1. Serializacja (JSON & Binary Helpers)

Te metody służą do zamiany obiektów gry (np. ruch gracza) na bajty i odwrotnie.

**Wymagania:** Kodowanie UTF-8, BigEndian dla liczb.

```csharp
public static class GameSerializer
{
    // Zamienia dowolny obiekt gry na gotową do wysłania tablicę bajtów (Payload)
    public static byte[] SerializeToBytes(object data)
    {
        string json = JsonConvert.SerializeObject(data);
        return Encoding.UTF8.GetBytes(json);
    }

    // Zamienia otrzymane bajty z powrotem na obiekt gry
    public static T DeserializeFromBytes<T>(byte[] payload)
    {
        string json = Encoding.UTF8.GetString(payload);
        return JsonConvert.DeserializeObject<T>(json);
    }

    // Tworzy 4-bajtowy nagłówek długości (wymóg protokołu)
    public static byte[] CreateHeader(int length)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, length);
        return header;
    }
    
    // Odczytuje długość z 4 bajtów
    public static int ReadHeader(byte[] header)
    {
        return BinaryPrimitives.ReadInt32BigEndian(header);
    }
}

```

---

## 🌐 2. Połączenie (IP lub DNS)

Uniwersalna metoda łącząca z serwerem gry. Obsługuje adresy typu `localhost` (DNS) oraz `127.0.0.1` (IP).
Zawiera timeout, aby program nie wisiał w nieskończoność.

```csharp
public async Task<TcpClient> ConnectToGameServerAsync(string host, int port)
{
    Console.WriteLine($"[GameClient] Łączenie z {host}:{port}...");
    TcpClient client = new TcpClient();

    try 
    {
        // Rozwiązanie DNS (działa też dla czystego IP)
        // Jeśli podasz "localhost", system znajdzie odpowiednie IP (IPv4/IPv6)
        IPHostEntry entry = await Dns.GetHostEntryAsync(host);
        IPAddress ipAddress = entry.AddressList[0];

        // Próba połączenia z timeoutem (np. 3 sekundy)
        var connectTask = client.ConnectAsync(ipAddress, port);
        var timeoutTask = Task.Delay(3000);

        if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
        {
            throw new Exception("Timeout: Nie udało się połączyć z serwerem gry.");
        }
        
        await connectTask; // Rzuci wyjątek, jeśli serwer odrzuci połączenie
        Console.WriteLine("[GameClient] Połączono!");
        return client;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Błąd Połączenia] {ex.Message}");
        client.Dispose();
        return null;
    }
}

```

---

## 📨 4. Pisanie Wiadomości Binarnej (Wysyłanie)

Wysyłanie składa się z dwóch kroków: wysłania długości (4 bajty) i wysłania danych.
To jest "Pisanie binarne" w kontekście Twojego zadania – piszesz bajty do strumienia.

**Ważne:** Sprawdzanie limitu 10KB (10240 bajtów).

```csharp
public async Task SendGameActionAsync(NetworkStream stream, object action)
{
    // 1. Serializacja (przygotowanie payloadu)
    byte[] payload = GameSerializer.SerializeToBytes(action);
    int length = payload.Length;

    // 2. Walidacja (wymóg z labów)
    if (length > 10240) 
        throw new Exception("TooLongMessageException: Wiadomość > 10KB");

    // 3. Przygotowanie nagłówka (4 bajty BigEndian)
    byte[] header = GameSerializer.CreateHeader(length);

    // 4. Wysłanie BINARNE do strumienia
    // Najpierw nagłówek, potem treść
    await stream.WriteAsync(header, 0, header.Length);
    await stream.WriteAsync(payload, 0, payload.Length);
    
    // Wypchnięcie danych (ważne przy socketach!)
    await stream.FlushAsync(); 
    
    Console.WriteLine($"[Wysłano] {length} bajtów.");
}

```

---

## 📥 3. Czytanie Wiadomości JSON (Odbiór)

To jest serce komunikacji. Musisz najpierw odebrać 4 bajty, sprawdzić ile danych ma nadejść, a potem pobrać resztę.
**Klucz:** Pętla `while` w metodzie `ReadExactly`. Bez tego, przy lagach sieci, program się wywali.

```csharp
// Helper: Czyta z sieci DOKŁADNIE 'count' bajtów
private async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count)
{
    byte[] buffer = new byte[count];
    int totalRead = 0;
    
    while (totalRead < count)
    {
        int read = await stream.ReadAsync(buffer, totalRead, count - totalRead);
        if (read == 0) return null; // Serwer zamknął połączenie
        totalRead += read;
    }
    return buffer;
}

// Główna metoda odbierająca stan gry
public async Task<T> ReceiveGameStateAsync<T>(NetworkStream stream)
{
    // KROK A: Pobierz nagłówek (4 bajty)
    byte[] header = await ReadExactlyAsync(stream, 4);
    if (header == null) return default; // Koniec połączenia

    // KROK B: Odczytaj długość wiadomości
    int length = GameSerializer.ReadHeader(header);

    // Walidacja przychodzących danych
    if (length > 10240) 
        throw new Exception("TooLongMessageException: Otrzymano za duży pakiet");

    // KROK C: Pobierz właściwą treść (JSON w bajtach)
    byte[] payload = await ReadExactlyAsync(stream, length);
    if (payload == null) throw new Exception("Połączenie zerwane w trakcie pobierania danych");

    // KROK D: Deserializacja JSON
    return GameSerializer.DeserializeFromBytes<T>(payload);
}

```

---

## 🚀 Przykładowe użycie (Game Loop)

```csharp
// Przykładowe DTO (Data Transfer Object)
public class PlayerMove { public string Direction { get; set; } }
public class GameState { public int Score { get; set; } public string Message { get; set; } }

// W metodzie Main lub Run:
var client = await ConnectToGameServerAsync("localhost", 9000);
if (client != null)
{
    using (NetworkStream stream = client.GetStream())
    {
        // 1. Wyślij ruch (Etap 4 i 1)
        var myMove = new PlayerMove { Direction = "UP" };
        await SendGameActionAsync(stream, myMove);

        // 2. Odbierz stan gry (Etap 3)
        GameState state = await ReceiveGameStateAsync<GameState>(stream);
        Console.WriteLine($"Serwer mówi: {state.Message}, Punkty: {state.Score}");
    }
}

```

```

```
