using System;
using System.Threading; // Potrzebne tylko do symulacji upływu czasu (Thread.Sleep)

namespace PrzykladZdarzen
{
    // --- KROK 1: DEFINICJA (po stronie Nadawcy) ---

    /// <summary>
    /// Klasa, która będzie wysyłać powiadomienia (zdarzenia).
    /// To jest nasz "Nadawca" (Publisher).
    /// </summary>
    public class Podgrzewacz
    {
        // 1a. Definiujemy "dzwoneczek" (event).
        // Mówimy, że będziemy wysyłać powiadomienia typu "WodaZagotowanaEventArgs".
        public event EventHandler<WodaZagotowanaEventArgs> WodaSieZagotowala;

        /// <summary>
        /// Główna metoda robocza. Symuluje podgrzewanie wody.
        /// </summary>
        public void ZacznijGotowac()
        {
            Console.WriteLine("[Podgrzewacz] Rozpoczynam gotowanie...");
            for (int temp = 0; temp <= 100; temp += 20)
            {
                Console.WriteLine($"[Podgrzewacz] Aktualna temperatura: {temp}°C");
                Thread.Sleep(500); // Udajemy, że to chwilę trwa

                if (temp >= 100)
                {
                    // 1b. Osiągnęliśmy punkt krytyczny! Czas "nacisnąć dzwoneczek".
                    // Tworzymy obiekt z informacją o zdarzeniu.
                    WodaZagotowanaEventArgs args = new WodaZagotowanaEventArgs(temp);
                    
                    // Wywołujemy metodę, która odpali zdarzenie.
                    OnWodaSieZagotowala(args);
                }
            }
        }

        /// <summary>
        /// Metoda pomocnicza, która FAKTYCZNIE odpala zdarzenie.
        /// To jest "naciśnięcie dzwoneczka".
        /// </summary>
        protected virtual void OnWodaSieZagotowala(WodaZagotowanaEventArgs e)
        {
            // 1c. Sprawdzamy, czy KTOKOLWIEK subskrybuje (czy lista nie jest pusta).
            // Jeśli tak (nie jest null), wywołujemy (Invoke) wszystkie podpięte metody.
            WodaSieZagotowala?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Klasa z danymi, które chcemy przekazać przy okazji zdarzenia.
    /// W naszym przypadku - z jaką temperaturą się zagotowało.
    /// </summary>
    public class WodaZagotowanaEventArgs : EventArgs
    {
        public int OsiagnietaTemperatura { get; }

        public WodaZagotowanaEventArgs(int temperatura)
        {
            OsiagnietaTemperatura = temperatura;
        }
    }


    // --- KROK 2 i 3: SUBSKRYPCJA I REAKCJA (po stronie Subskrybenta) ---

    /// <summary>
    /// Główna klasa programu. To jest nasz "Subskrybent".
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // Tworzymy obiekty
            Podgrzewacz czajnik = new Podgrzewacz();

            // KROK 2: SUBSKRYPCJA
            // Mówimy: "Hej czajnik, jak już wywołasz zdarzenie 'WodaSieZagotowala',
            // to ja chcę, żebyś uruchomił moją metodę 'Czajnik_ZareagujNaPstrykniecie'".
            // To jest "kliknięcie dzwoneczka".
            czajnik.WodaSieZagotowala += Czajnik_ZareagujNaPstrykniecie;

            // Możemy zasubskrybować więcej niż raz!
            czajnik.WodaSieZagotowala += (sender, e) => {
                Console.WriteLine("[SMS] Wiadomość do mamy: woda gotowa.");
            };


            // Teraz dopiero uruchamiamy proces, który DOPIERO doprowadzi do zdarzenia.
            czajnik.ZacznijGotowac();

            Console.WriteLine("[Program] Zakończyłem pracę. Czekam na zamknięcie.");
            Console.ReadKey();
        }

        // KROK 3: REAKCJA
        /// <summary>
        /// To jest nasza metoda-reakcja (Event Handler).
        /// Zostanie automatycznie wywołana, gdy zdarzenie wystąpi.
        /// </summary>
        private static void Czajnik_ZareagujNaPstrykniecie(object sender, WodaZagotowanaEventArgs e)
        {
            // 'sender' to obiekt, który wysłał zdarzenie (nasz 'czajnik')
            // 'e' to dane, które wysłał (obiekt 'WodaZagotowanaEventArgs')

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n!!! BIP BIP BIP! Czajnik 'pstryknął' !!!");
            Console.WriteLine($"Woda osiągnęła {e.OsiagnietaTemperatura}°C.");
            Console.WriteLine("Można robić herbatę!\n");
            Console.ResetColor();
        }
    }
}
