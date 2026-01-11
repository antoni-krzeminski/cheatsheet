

# 📖 Skrypt: Zaawansowane I/O i Sieci w C# (.NET)

## 1. Komunikacja Sieciowa (TCP/IP)

### Teoria w pigułce

TCP to protokół strumieniowy. To najważniejsza rzecz, którą musisz pamiętać.

* **Strumień (Stream):** Dane płyną jak woda w rurze. Nie ma pojęcia "paczki". Jeśli wyślesz "ABC" i "DEF", odbiorca może dostać "ABCDEF", "A", "BCDEF" albo "ABCDE", "F".
* **Framing (Ramkowanie):** Aby wiedzieć, gdzie kończy się jedna wiadomość, a zaczyna druga, musisz użyć własnego protokołu. Najczęstszy standard na labach to:


* **Endianness:** Sieć zazwyczaj wymaga **Big Endian** (najbardziej znaczący bajt pierwszy), a Twój procesor to prawdopodobnie **Little Endian**. Musisz konwertować liczby.

### 🛠️ Szablon: Uniwersalna obsługa wiadomości (TCP)

To jest kod, który ratuje życie, gdy trzeba wysłać/odebrać dane i nie martwić się o to, że TCP utnie kawałek wiadomości.

#### A. Wysyłanie (Writer)

Wysyłamy 4 bajty długości, a potem treść (np. JSON lub tekst).

```csharp
using System.Net.Sockets;
using System.Buffers.Binary; // Ważne do Endianness
using System.Text;
using Newtonsoft.Json; // Jeśli używasz JSON

public static void SendMessage<T>(NetworkStream stream, T data)
{
    // 1. Serializacja (zamiana obiektu na bajty)
    string json = JsonConvert.SerializeObject(data);
    byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
    
    // 2. Przygotowanie nagłówka (Długość treści)
    byte[] headerBytes = new byte[4];
    [cite_start]// Zapisz int jako BigEndian (standard sieciowy) [cite: 45]
    BinaryPrimitives.WriteInt32BigEndian(headerBytes, bodyBytes.Length);

    // 3. Wysłanie
    // Najpierw długość, potem ciało
    stream.Write(headerBytes, 0, headerBytes.Length); 
    stream.Write(bodyBytes, 0, bodyBytes.Length);
}

```

#### B. Odbieranie (Reader) - TO JEST NAJWAŻNIEJSZE

Metoda `Read` w strumieniu **nie gwarantuje** odczytania tylu bajtów, ile chcesz. Musisz pętlić, aż zbierzesz wszystko.

```csharp
public static T ReceiveMessage<T>(NetworkStream stream)
{
    // 1. Odczyt nagłówka (4 bajty)
    byte[] headerBytes = new byte[4];
    if (!ReadExactly(stream, headerBytes, 4)) return default; // Zerwane połączenie

    // 2. Parsowanie długości
    int bodyLength = BinaryPrimitives.ReadInt32BigEndian(headerBytes);

    // Opcjonalnie: Zabezpieczenie przed gigantycznymi wiadomościami
    if (bodyLength > 10 * 1024) throw new Exception("Za duża wiadomość!");

    // 3. Odczyt właściwej treści
    byte[] bodyBytes = new byte[bodyLength];
    if (!ReadExactly(stream, bodyBytes, bodyLength)) return default;

    // 4. Deserializacja
    string json = Encoding.UTF8.GetString(bodyBytes);
    return JsonConvert.DeserializeObject<T>(json);
}

// Funkcja pomocnicza - czyta AŻ uzbiera 'count' bajtów
private static bool ReadExactly(NetworkStream stream, byte[] buffer, int count)
{
    int offset = 0;
    while (offset < count)
    {
        int read = stream.Read(buffer, offset, count - offset);
        if (read == 0) return false; // Koniec strumienia (rozłączenie)
        offset += read;
    }
    return true;
}

```

### 🛠️ Szablon: Klient i Serwer (Inicjalizacja)

**Serwer (TcpListener):**

```csharp
TcpListener listener = new TcpListener(IPAddress.Any, 5000);
listener.Start();
Console.WriteLine("Serwer czeka...");

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    // Obsługa klienta w tle (żeby nie blokować reszty)
    _ = HandleClientAsync(client); 
}

```

**Klient (TcpClient):**

```csharp
using TcpClient client = new TcpClient();
// Timeout na łączenie (częsty wymóg)
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
try {
    await client.ConnectAsync("127.0.0.1", 5000, cts.Token);
} catch {
    Console.WriteLine("Nie udało się połączyć.");
}

```

---

## 2. Łącza Nazwane (Named Pipes)

### Teoria w pigułce

Służą do komunikacji procesów na **tym samym komputerze**. Są szybsze niż TCP i działają bardziej jak pliki.

* Ścieżka do pipe'a (w systemie Windows) to zawsze: `\\.\pipe\NazwaTwojejRury`. W kodzie C# podajesz tylko `NazwaTwojejRury`.
* Częsty model: Serwer tworzy rurę, Klient się do niej podpina.
* Komunikacja jest zazwyczaj tekstowa (StreamReader/StreamWriter).

### 🛠️ Szablon: Serwer i Klient Pipe

**Serwer (NamedPipeServerStream):**

```csharp
using System.IO.Pipes;

// Serwer musi podać nazwę rury
using var server = new NamedPipeServerStream("MojaRuraTestowa", PipeDirection.InOut);

Console.WriteLine("Czekam na połączenie...");
await server.WaitForConnectionAsync(); // Blokuje aż klient się podłączy

// Czytanie i pisanie jak w pliku tekstowym
using var reader = new StreamReader(server);
using var writer = new StreamWriter(server) { AutoFlush = true }; // WAŻNE: AutoFlush!

string message = await reader.ReadLineAsync(); // Czytaj linię
await writer.WriteLineAsync("Otrzymałem: " + message); // Odpisz

```

**Klient (NamedPipeClientStream):**

```csharp
using System.IO.Pipes;

// Klient podaje kropkę "." jako nazwę serwera (ten sam komputer)
using var client = new NamedPipeClientStream(".", "MojaRuraTestowa", PipeDirection.InOut);

try {
    await client.ConnectAsync(2000); // Timeout 2s
} catch (TimeoutException) {
    Console.WriteLine("Serwer nie odpowiada.");
    return;
}

using var writer = new StreamWriter(client) { AutoFlush = true };
using var reader = new StreamReader(client);

await writer.WriteLineAsync("Hej serwer!");
string response = await reader.ReadLineAsync();

```

---

## 3. Mapowanie Plików (Memory Mapped Files)

### Teoria w pigułce

Używane, gdy plik jest za duży na RAM (np. 5GB) lub gdy wiele procesów chce współdzielić pamięć.

* Mapujesz plik z dysku do wirtualnej pamięci operacyjnej.
* Nie używasz `Read`, tylko przesuwasz się wskaźnikiem (offsetem).
* **Accessor:** To twoje "okienko" na plik. Możesz stworzyć Accessor (widok) na cały plik lub tylko na mały fragment (np. od bajtu 1000 do 2000).

### 🛠️ Szablon: Czytanie dużego pliku

Załóżmy, że musisz przeczytać fragment pliku od pozycji `offset` o długości `length`.

```csharp
using System.IO.MemoryMappedFiles;
using System.Text;

public string ReadFragment(string path, long offset, int length)
{
    // 1. Otwórz plik z dysku
    using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open);

    // 2. Stwórz "widok" (okno) na konkretny fragment
    // offset = gdzie zacząć, length = ile bajtów mapować
    using var accessor = mmf.CreateViewAccessor(offset, length, MemoryMappedFileAccess.Read);

    // 3. Przygotuj bufor w RAMie
    byte[] buffer = new byte[length];

    // 4. Skopiuj dane z "okna" do bufora
    // 0 = pozycja w widoku (początek naszego okna)
    accessor.ReadArray(0, buffer, 0, length);

    // 5. Zinterpretuj dane (np. jako tekst)
    return Encoding.UTF8.GetString(buffer);
}

```

### Przydatne operacje na MMF

* **Czytanie liczb (structów):** Jeśli plik jest binarny (nie tekstowy), `ViewAccessor` jest super szybki.
```csharp
int liczba = accessor.ReadInt32(pozycja);
double ułamek = accessor.ReadDouble(pozycja + 4);

```



---

## 4. Cheat Sheet: Komendy i Przydatne Klasy

### Przydatne klasy z .NET

| Klasa | Namespace | Zastosowanie |
| --- | --- | --- |
| `BinaryPrimitives` | `System.Buffers.Binary` | Kluczowe do zamiany BigEndian <-> LittleEndian (`ReadInt32BigEndian`). |
| `CancellationTokenSource` | `System.Threading` | Do robienia timeoutów i przerywania zadań. |
| `Encoding.UTF8` | `System.Text` | `GetBytes()` (string->byte[]) i `GetString()` (byte[]->string). |
| `StreamWriter` | `System.IO` | Pamiętaj o `AutoFlush = true` przy `Pipe` i `NetworkStream`! |

### Przydatne polecenia konsolowe (Terminal)

* 
`ipconfig` (Windows) / `ip a` (Linux/Mac) – sprawdzenie IP.


* `netstat -an | findstr 5000` – sprawdź, czy coś nasłuchuje na porcie 5000 (Windows).
* `dotnet run -- argumenty` – uruchomienie programu z argumentami (np. IP i port).

### Jak radzić sobie z wyjątkami (Common Patterns)

1. **Timeout:** Zawsze używaj `CancellationTokenSource` z `TimeSpan`.
2. **Koniec strumienia:** Jeśli `stream.Read` zwróci `0` lub `reader.ReadLine` zwróci `null` -> druga strona zamknęła połączenie.
3. **Za duży plik/wiadomość:** Zawsze sprawdzaj `length` przed alokacją tablicy (`new byte[length]`), żeby ktoś nie wysłał Ci 2GB i nie wysadził pamięci.

To jest zestaw narzędzi, z którym powinieneś poradzić sobie z większością zadań na labach z "Programowania sieciowego i współbieżnego". Powodzenia!
