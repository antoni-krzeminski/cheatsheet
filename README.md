
# 📘 Skrypt Teoretyczny: Zaawansowane Operacje Wejścia/Wyjścia w C#

## Wstęp

Laboratorium 12 skupia się na komunikacji i wydajnym przetwarzaniu danych poza standardową pamięcią RAM aplikacji. Zamiast operować tylko na zmiennych w pamięci, będziesz wymieniać dane między procesami (IPC), między komputerami (TCP) oraz mapować gigantyczne pliki bezpośrednio do przestrzeni adresowej procesu.

---

## Część 1: Komunikacja Sieciowa (TCP/IP)

### 1.1. Podstawy protokołu TCP w .NET

Protokół TCP (Transmission Control Protocol) to protokół strumieniowy. Gwarantuje on dostarczenie danych w kolejności, ale **nie gwarantuje zachowania granic wiadomości**. Oznacza to, że jeśli wyślesz dwie wiadomości po 100 bajtów, odbiorca może otrzymać jedną paczkę 200 bajtów, albo dziesięć paczek po 20 bajtów.

Dlatego w zadaniu  wymagane jest zdefiniowanie własnego "protokołu" (tzw. framing), który w tym przypadku wygląda tak:
`[DŁUGOŚĆ (4 bajty)]` + `[TREŚĆ (JSON)]`

### 1.2. Kluczowe Klasy

* **`TcpListener`**: Klasa serwera. Nasłuchuje na wskazanym porcie na przychodzące połączenia.
* **`TcpClient`**: Klasa klienta (lub reprezentacja klienta po stronie serwera). Umożliwia nawiązanie połączenia.
* **`NetworkStream`**: Strumień danych. To tutaj piszesz (`Write`) i czytasz (`Read`) bajty.

### 1.3. Endianness (Kolejność bajtów)

Komputery (x86/x64) zazwyczaj pracują w trybie **Little Endian** (najmniej znaczący bajt pierwszy). Protokoły sieciowe (tzw. Network Byte Order) zazwyczaj wymagają **Big Endian**.

W zadaniu musisz przesłać nagłówek długości jako `int` w Big Endian.

**Przykład konwersji (C#):**

```csharp
using System.Buffers.Binary;

int dlugosc = 125;
byte[] naglowek = new byte[4];

// Zapisz int jako Big Endian do tablicy bajtów
BinaryPrimitives.WriteInt32BigEndian(naglowek, dlugosc);

// Odczyt (gdy odbierasz dane)
int odebranaDlugosc = BinaryPrimitives.ReadInt32BigEndian(odebranyBufor);

```

### 1.4. Serializacja JSON

W zadaniu treść wiadomości to JSON zakodowany w UTF-8. Należy użyć biblioteki `Newtonsoft.Json`.

**Schemat wysyłania wiadomości (Pseudokod dla `MessageWriter`):**

1. Zserializuj obiekt do stringa (JSON).
2. Zamień string na tablicę bajtów (UTF-8).
3. Sprawdź, czy rozmiar nie przekracza 10kB – jeśli tak, rzuć `TooLongMessageException`.


4. Przygotuj nagłówek (4 bajty, Big Endian) z długością tablicy bajtów.
5. Wyślij do strumienia: najpierw nagłówek, potem treść.

**Schemat odbierania wiadomości (Pseudokod dla `MessageReader`):**

1. Czytaj ze strumienia dokładnie 4 bajty (pamiętaj: `Stream.Read` może zwrócić mniej niż poprosiłeś, użyj pętli `ReadExactly` lub podobnej logiki).
2. Zinterpretuj te 4 bajty jako `int` (długość).
3. Jeśli długość > 10kB -> Błąd.


4. Czytaj ze strumienia dokładnie tyle bajtów, ile wynosi długość.
5. Zamień bajty na string (UTF-8), a string na obiekt (Deserializacja).

---

## Część 2: Łącza Nazwane (Named Pipes)

### 2.1. Czym są Pipes?

Named Pipes (łącza nazwane) to mechanizm IPC (Inter-Process Communication). Pozwalają na bardzo szybką wymianę danych między procesami działającymi **na tym samym komputerze**. Działają podobnie do plików lub socketów, ale są zoptymalizowane przez system operacyjny (dane często nie trafiają nawet na dysk, siedzą w RAM).

W zadaniu tworzysz bazę klucz-wartość (Key-Value Store).

### 2.2. Kluczowe Klasy

* **`NamedPipeServerStream`**: Tworzona przez serwer. Czeka na połączenie (`WaitForConnectionAsync`).
* **`NamedPipeClientStream`**: Tworzona przez klienta. Łączy się z serwerem (`Connect`).

### 2.3. Protokół Komunikacji

Tutaj protokół jest prostszy niż w TCP – tekstowy, oddzielony znakami nowej linii.

* Komendy: `SET key value`, `GET key`, `DELETE key`.


* Ważne: Wiadomości nie mogą zawierać znaku nowej linii w treści.



**Przykład implementacji (Klient):**

```csharp
using System.IO.Pipes;

[cite_start]// Łączenie z timeoutem [cite: 136]
using var client = new NamedPipeClientStream(".", "NazwaRury", PipeDirection.InOut);
try {
    await client.ConnectAsync(3000); // 3 sekundy
} catch (TimeoutException) {
    // Obsługa błędu
}

// Pisanie i czytanie (można użyć StreamWriter/StreamReader dla wygody)
using var writer = new StreamWriter(client) { AutoFlush = true };
using var reader = new StreamReader(client);

await writer.WriteLineAsync("GET mojKlucz");
string odpowiedz = await reader.ReadLineAsync();

```

### 2.4. Cancellation Token

W zadaniu wielokrotnie pojawia się wymóg obsługi `CancellationToken`. To standardowy w .NET sposób na przerywanie operacji asynchronicznych (np. gdy zamykamy serwer).

* Przekazuj token do każdej metody asynchronicznej (np. `ReadAsync(buffer, token)`).

---

## Część 3: Mapowanie Plików (Memory Mapped Files)

### 3.1. Problem

Masz plik CSV, który jest większy niż dostępna pamięć RAM (np. 10 GB). Nie możesz zrobić `File.ReadAllLines()`, bo wyrzuci `OutOfMemoryException`.
Tradycyjne `FileStream` i czytanie linia po linii jest bezpieczne, ale może być wolne przy losowym dostępie (skakanie po pliku).

### 3.2. Rozwiązanie: Memory Mapped Files (MMF)

MMF pozwala mapować plik z dysku bezpośrednio do wirtualnej przestrzeni adresowej procesu. Dla Twojego programu wygląda to tak, jakby cały plik był w tablicy w pamięci, a system operacyjny zajmuje się doczytywaniem fragmentów (stronicowaniem) z dysku w tle. Jest to ekstremalnie wydajne.

### 3.3. Zadanie: BigCSVReader

Musisz zaimplementować dwie wersje czytnika:

1. **`StreamBigCsvReader`**: Używa zwykłego `FileStream` + `Seek`.
2. **`MmfBigCsvReader`**: Używa `MemoryMappedFile`.

Kluczowy jest tu plik `.offsets`. Ponieważ linie w CSV mają różną długość, nie wiesz, gdzie zaczyna się 100-tna linia bez przeczytania 99 poprzednich. Dlatego w konstruktorze tworzony jest indeks (plik `.offsets`), który przechowuje pozycję startową każdego wiersza jako `long` (8 bajtów).

### 3.4. Implementacja MMF

Będziesz używać klas:

* `MemoryMappedFile.CreateFromFile(...)` – otwiera plik.
* `MemoryMappedViewAccessor` – "okno", przez które zaglądasz do pliku.

**Przykład odczytu fragmentu za pomocą MMF:**

```csharp
using System.IO.MemoryMappedFiles;

// Otwarcie pliku
using var mmf = MemoryMappedFile.CreateFromFile("plik.csv", FileMode.Open);

// Utworzenie widoku (można mapować tylko fragment, tu mapujemy całość lub fragment)
using var accessor = mmf.CreateViewAccessor(offset, length);

// Odczyt bajtów
byte[] buffer = new byte[length];
accessor.ReadArray(0, buffer, 0, buffer.Length);

[cite_start]// Konwersja na string (pamiętaj o kodowaniu UTF-8 [cite: 191])
string linia = Encoding.UTF8.GetString(buffer);

```

---

## 🚀 Praktyczny Checklist do Laboratorium

### Zadanie 1: Chat (TCP)

1. **MessageDTO:** Klasa do przesyłania danych.
2. **MessageWriter:**
* Sprawdź długość (max 10kB).
* Zapisz nagłówek (4 bajty Big Endian).
* Zapisz JSON.


3. **MessageReader:**
* Odczytaj nagłówek -> ustal długość.
* Odczytaj resztę -> deserializuj.
* Obsłuż wyjątki (`InvalidMessageException`, `TooLongMessageException`).


4. **Serwer:**
* Metoda `ForwardMessagesAsync`: Odbierz od klienta A -> wypisz na konsolę -> wyślij do klienta B.





### Zadanie 2: Baza Key-Value (Pipes)

1. **Serwer:**
* `NamedPipeServerStream`.
* Pętla nasłuchująca komend (`StreamReader.ReadLine`).
* Obsługa: SET, GET, DELETE.


2. **Klient:**
* `NamedPipeClientStream` z timeoutem 3s.
* Wysyłanie komend i odbieranie odpowiedzi ("OK", "NOT_FOUND", "ERROR").



### Zadanie 3: CSV (MMF)

1. **StreamReader:** Implementacja przy użyciu `FileStream.Seek(offset)` i odczytu bajtów.
2. **MmfReader:** Implementacja przy użyciu `MemoryMappedFile` i `ViewAccessor`.
3. Korzystaj z pliku `.offsets` (dostarczonego w kodzie startowym), aby wiedzieć, gdzie `Seek`-ować.

Czy chciałbyś, abym przygotował teraz szkielet kodu dla konkretnej klasy, np. `MessageReader` lub `MmfBigCsvReader`?
