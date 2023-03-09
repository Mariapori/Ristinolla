using Microsoft.AspNetCore.SignalR;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Ristinolla.Data
{
    public class Pelaaja
    {
        public string YhteysID { get; set; }
        public string Nimi { get; set; }
        public bool Pelaamassa { get; set; }
        public bool OdottaaVuoroa { get; set; }
        public Pelaaja? Vastustaja { get; set; }
    }

    public class Peli
    {
        public string Id { get; set; } = string.Empty;
        public Pelaaja Aloittaja { get; set; }
        public Pelaaja Haastettu { get; set; }
        public Pelaaja Vuoro { get; set; }
        public List<Ruutu> Ruudut { get; set; }
        public Pelaaja? Voittaja { get; set; } 

        public Peli()
        {
            Id = Guid.NewGuid().ToString();
            Ruudut = new List<Ruutu>();
            for (int i = 0; i < 9; i++)
            {
                Ruudut.Add(new Ruutu { Id = i, Pelaaja = null });
            }
        }
    }
    public class GameService : Hub
    {
        private static ObservableCollection<Pelaaja> pelaajaList;
        private static ObservableCollection<Peli> peliList = new ObservableCollection<Peli>();
        private static object _lock = new object();

        public async void RekisteroiYhteys(string nimi)
        {
            Pelaaja pel;
            lock (_lock)
            {
                pel = new Pelaaja { Nimi = nimi, Pelaamassa = false, OdottaaVuoroa = false, YhteysID = Context.ConnectionId, Vastustaja = null };
                if (pelaajaList is not null)
                {
                    pelaajaList.Add(pel);
                }
                else
                {
                    pelaajaList = new ObservableCollection<Pelaaja>();
                    pelaajaList.Add(pel);
                }
            }
            await Clients.Caller.SendAsync("rekisteroity", pel);
            await Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);

        }

        public async void Pelaa(Pelaaja haast)
        {
            var peli = new Peli();
            peli.Aloittaja = haast;
            peli.Haastettu = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
            lock (_lock)
            {
                if (peli.Haastettu != null && peli.Aloittaja != null)
                {
                    peli.Aloittaja.Pelaamassa = true;
                    peli.Haastettu.Pelaamassa = true;
                    Random rng = new Random();
                    int aloittaja = rng.Next(0, 1);
                    if (aloittaja == 0)
                    {
                        peli.Vuoro = peli.Aloittaja;
                    }
                    else
                    {
                        peli.Vuoro = peli.Haastettu;
                    }
                    peliList.Add(peli);
                }
            }
            await Groups.AddToGroupAsync(peli.Aloittaja.YhteysID, peli.Id);
            await Groups.AddToGroupAsync(peli.Haastettu.YhteysID, peli.Id);

            await Clients.Group(peli.Id).SendAsync("Peliin", peli);

        }

        public async Task Aseta(string peliID, int ruutuID)
        {
            bool gameover = false;
            Peli peli;
            lock (_lock)
            {
                var pelaaja = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
                peli = peliList.FirstOrDefault(o => o.Id == peliID);
                var ruutu = peli.Ruudut.FirstOrDefault(o => o.Id == ruutuID);

                if (peli.Vuoro.Nimi == pelaaja.Nimi && ruutu.Pelaaja == null && peli.Voittaja == null)
                {
                    ruutu.Pelaaja = pelaaja.Nimi;
                    if (pelaaja.Nimi == peli.Aloittaja.Nimi)
                    {
                        peli.Vuoro = peli.Haastettu;
                    }
                    else
                    {
                        peli.Vuoro = peli.Aloittaja;
                    }
                    if (Voittiko(peli, pelaaja))
                    {
                        gameover = true;
                    }
                }

            }
            await Clients.Group(peliID).SendAsync("refresh", peli);
        }

        public async void Haasta(Pelaaja haastettava)
        {
            if(haastettava.Vastustaja == null)
            {
                var pelaaja = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
                await Clients.Client(haastettava.YhteysID).SendAsync("Haaste", pelaaja);
            }
        }

        public async Task<Peli> GetGame(string Id)
        {
            return peliList.FirstOrDefault(o => o.Id == Id);
        }
        public async Task<Pelaaja> Me()
        {
            return pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
        }


        public override Task OnDisconnectedAsync(Exception? exception)
        {
            if(pelaajaList != null)
            {
                lock (_lock)
                {
                    var pelaaja = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
                    pelaajaList.Remove(pelaaja);
                }
                Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);
            }
            return Task.CompletedTask;
        }

        public bool Voittiko(Peli peli, Pelaaja vuorossa)
        {
            if (peli.Ruudut != null && peli.Ruudut.Where(o => o.Pelaaja != null).Count() > 3)
            {
                var ruudutArray = peli.Ruudut.ToArray();
                int[,] voittolinjat = { { 0, 1, 2 }, { 3, 4, 5 }, { 6, 7, 8 }, { 0, 3, 6 }, { 1, 4, 7 }, { 2, 5, 8 }, { 0, 4, 8 }, { 2, 4, 6 } };
                for (int i = 0; i < 8; i++)
                {
                    var a = ruudutArray[voittolinjat[i, 0]].Pelaaja ?? "A";
                    var b = ruudutArray[voittolinjat[i, 1]].Pelaaja ?? "B";
                    var c = ruudutArray[voittolinjat[i, 2]].Pelaaja ?? "C";
                    if (a == b && c == a)
                    {
                        peli.Voittaja = vuorossa;
                        return true;
                    }
                }
                if (peli.Ruudut.Where(o => o.Pelaaja != null).Count() == 9)
                {
                    peli.Voittaja = null;
                    return true;
                }
                return false;
            }
            else
            {
                return false;
            }
        }
    }
}
