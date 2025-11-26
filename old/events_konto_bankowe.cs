using System;

namespace BankAppExample
{
    // --- 1. Pliki Danych (EventArgs) ---
    // Definicje klas, które będą przenosić dane o zdarzeniach.

    /// <summary>
    /// Paczka danych wysyłana przy udanej transakcji (wpłacie lub wypłacie).
    /// </summary>
    public class TransakcjaEventArgs : EventArgs
    {
        public decimal Kwota { get; }
        public decimal AktualneSaldo { get; }

        public TransakcjaEventArgs(decimal kwota, decimal aktualneSaldo)
        {
            Kwota = kwota;
            AktualneSaldo = aktualneSaldo;
        }
    }

    /// <summary>
    /// Paczka danych wysyłana przy nieudanej próbie wypłaty.
    /// </summary>
    public class OdmowaEventArgs : EventArgs
    {
        public decimal KwotaDoWyplaty { get; }
        public decimal ObecneSaldo { get; }

        public OdmowaEventArgs(decimal kwotaDoWyplaty, decimal obecneSaldo)
        {
            KwotaDoWyplaty = kwotaDoWyplaty;
            ObecneSaldo = obecneSaldo;
        }
    }

    // --- 2. Nadawca Zdarzeń (Publisher) ---

    /// <summary>
    /// Klasa 'KontoBankowe' jest NADAWCĄ (Publisherem).
    /// Posiada logikę biznesową i wywołuje trzy różne zdarzenia.
    /// </summary>
    public class KontoBankowe
    {
        private decimal _saldo;

        // --- Definicja "Dzwoneczków" (Zdarzeń) ---
        public event EventHandler<TransakcjaEventArgs> SrodkiWplynely;
        public event EventHandler<TransakcjaEventArgs> SrodkiWyplacone;
        public event EventHandler<OdmowaEventArgs> OdmowaWyplaty;

        
        public KontoBankowe(decimal saldoPoczatkowe)
        {
            _saldo = saldoPoczatkowe;
        }

        public decimal Saldo => _saldo; // Publiczna właściwość tylko do odczytu

        
        public void Wplac(decimal kwota)
        {
            if (kwota <= 0)
            {
                Console.WriteLine("[KONTO] Kwota wpłaty musi być dodatnia.");
                return;
            }

            _saldo += kwota;
            Console.WriteLine($"[KONTO] Wpłacono {kwota:C}. Nowe saldo: {_saldo:C}");

            // --- WYWOŁANIE ZDARZENIA 1 ---
            OnSrodkiWplynely(new TransakcjaEventArgs(kwota, _saldo));
        }

        public void Wyplac(decimal kwota)
        {
            if (kwota <= 0)
            {
                Console.WriteLine("[KONTO] Kwota wypłaty musi być dodatnia.");
                return;
            }

            if (_saldo >= kwota)
            {
                _saldo -= kwota;
                Console.WriteLine($"[KONTO] Wypłacono {kwota:C}. Nowe saldo: {_saldo:C}");

                // --- WYWOŁANIE ZDARZENIA 2 ---
                OnSrodkiWyplacone(new TransakcjaEventArgs(kwota, _saldo));
            }
            else
            {
                Console.WriteLine($"[KONTO] ODMOWA. Brak środków. Chciano wypłacić {kwota:C}, dostępne jest {_saldo:C}");

                // --- WYWOŁANIE ZDARZENIA 3 ---
                OnOdmowaWyplaty(new OdmowaEventArgs(kwota, _saldo));
            }
        }

        // Metody pomocnicze do bezpiecznego wywoływania zdarzeń
        protected virtual void OnSrodkiWplynely(TransakcjaEventArgs e)
        {
            SrodkiWplynely?.Invoke(this, e);
        }

        protected virtual void OnSrodkiWyplacone(TransakcjaEventArgs e)
        {
            SrodkiWyplacone?.Invoke(this, e);
        }

        protected virtual void OnOdmowaWyplaty(OdmowaEventArgs e)
        {
            OdmowaWyplaty?.Invoke(this, e);
        }
    }

    // --- 3. Subskrybenci (Subscribers) ---

    /// <summary>
    /// SUBSKRYBENT 1: Rejestrator Transakcji
    /// Interesują go tylko udane transakcje (wpłaty i wypłaty).
    /// </summary>
    public class RejestratorTransakcji
    {
        private KontoBankowe _obserwowaneKonto;

        // Przy tworzeniu, przekazujemy mu konto, które ma obserwować
        public RejestratorTransakcji(KontoBankowe konto)
        {
            _obserwowaneKonto = konto;

            // --- Subskrypcja ---
            _obserwowaneKonto.SrodkiWplynely += RejestrujWplate;
            _obserwowaneKonto.SrodkiWyplacone += RejestrujWyplate;
        }

        // --- Reakcja ---
        private void RejestrujWplate(object sender, TransakcjaEventArgs e)
        {
            Console.WriteLine($"  -> [LOG] Zaksięgowano wpłatę: +{e.Kwota:C}. Saldo po operacji: {e.AktualneSaldo:C}");
        }

        private void RejestrujWyplate(object sender, TransakcjaEventArgs e)
        {
            Console.WriteLine($"  -> [LOG] Zaksięgowano wypłatę: -{e.Kwota:C}. Saldo po operacji: {e.AktualneSaldo:C}");
        }

        // Metoda pozwalająca anulować subskrypcję
        public void PrzestanRejestrowac()
        {
            Console.WriteLine("\n[LOG] Rejestrator kończy pracę. Anulowanie subskrypcji...\n");
            _obserwowaneKonto.SrodkiWplynely -= RejestrujWplate;
            _obserwowaneKonto.SrodkiWyplacone -= RejestrujWyplate;
        }
    }

    /// <summary>
    /// SUBSKRYBENT 2: System Powiadomień SMS
    /// Interesuje go TYLKO zagrożenie (nieudana wypłata).
    /// </summary>
    public class SystemPowiadomienSMS
    {
        // Przy tworzeniu, przekazujemy mu konto, które ma obserwować
        public SystemPowiadomienSMS(KontoBankowe konto)
        {
            // --- Subskrypcja ---
            konto.OdmowaWyplaty += WyslijAlertSMS;
        }

        // --- Reakcja ---
        private void WyslijAlertSMS(object sender, OdmowaEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  >>> [ALERT SMS] Uwaga! Nastąpiła próba wypłaty {e.KwotaDoWyplaty:C}, " +
                              $"gdy na koncie było tylko {e.ObecneSaldo:C}. Transakcję odrzucono.");
            Console.ResetColor();
        }
    }

    // --- 4. Program Główny (Orkiestracja) ---

    /// <summary>
    /// Klasa Program łączy wszystko w całość.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Tworzymy nadawcę (konto z saldem początkowym 1000)
            KontoBankowe mojeKonto = new KontoBankowe(1000);

            // 2. Tworzymy subskrybentów i podpinamy ich pod konto
            RejestratorTransakcji logger = new RejestratorTransakcji(mojeKonto);
            SystemPowiadomienSMS alertSms = new SystemPowiadomienSMS(mojeKonto);

            Console.WriteLine("--- Rozpoczynamy symulację ---");
            Console.WriteLine($"Saldo początkowe: {mojeKonto.Saldo:C}\n");

            // Scenariusz 1: Udana wpłata
            Console.WriteLine("--- Scenariusz 1: Wpłata 500 ---");
            mojeKonto.Wplac(500);
            // SPODZIEWANY EFEKT: Reaguje tylko RejestratorTransakcji.

            Console.WriteLine("\n--- Scenariusz 2: Udana wypłata 200 ---");
            mojeKonto.Wyplac(200);
            // SPODZIEWANY EFEKT: Reaguje tylko RejestratorTransakcji.

            Console.WriteLine("\n--- Scenariusz 3: Nieudana wypłata 5000 ---");
            mojeKonto.Wyplac(5000);
            // SPODZIEWANY EFEKT: Reaguje tylko SystemPowiadomienSMS.

            
            // Scenariusz 4: Anulowanie subskrypcji
            logger.PrzestanRejestrowac();

            Console.WriteLine("--- Scenariusz 4: Wpłata 100 (po anulowaniu subskrypcji logera) ---");
            mojeKonto.Wplac(100);
            // SPODZIEWANY EFEKT: NIKT nie reaguje. Loger już nie słucha.

            Console.WriteLine("\n--- Scenariusz 5: Ponowna odmowa (po anulowaniu subskrypcji logera) ---");
            mojeKonto.Wyplac(2000);
            // SPODZIEWANY EFEKT: Reaguje SystemPowiadomienSMS (on nie anulował subskrypcji).


            Console.WriteLine("\n\n--- Zakończono symulację ---");
            Console.ReadKey();
        }
    }
}
