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
        public int ruutuKoko { get; set; }

        public Peli(int ruutuKoko)
        {
            Id = Guid.NewGuid().ToString();
            Ruudut = new List<Ruutu>();
            this.ruutuKoko = ruutuKoko;
            for (int i = 0; i < this.ruutuKoko*this.ruutuKoko; i++)
            {
                Ruudut.Add(new Ruutu { Id = i, Pelaaja = null });
            }

        }
        public Peli()
        {
            Id = Guid.NewGuid().ToString();
            Ruudut = new List<Ruutu>();
            this.ruutuKoko = 3;
            for (int i = 0; i < this.ruutuKoko * this.ruutuKoko; i++)
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

        public async Task<int> GetGameCount()
        {
            int count = 0;
            lock (_lock)
            {
                count = peliList.Count;
            }
            return count;
        }

        public async Task Poistu()
        {

                var pelit = peliList.Where(o => o.Haastettu.YhteysID == Context.ConnectionId || o.Aloittaja.YhteysID == Context.ConnectionId);
                if (pelit.Any())
                {
                    foreach (var peli in pelit)
                    {
                        if (peli.Ruudut.Where(o => o.Pelaaja != null).Count() != peli.ruutuKoko*peli.ruutuKoko)
                        {
                            peli.Voittaja = peli.Aloittaja.YhteysID != Context.ConnectionId ? peli.Aloittaja : peli.Haastettu;
                            peli.Aloittaja.Pelaamassa = false;
                            peli.Haastettu.Pelaamassa = false;
                        }
                        await Clients.Group(peli.Id).SendAsync("refresh", peli);
                        peliList.Remove(peli);
                    }
                }
            
            await Clients.All.SendAsync("gamecount", peliList.Count);
            await Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);
        }

        public async void Pelaa(Pelaaja haast, int? pelinkoko)
        {
            Peli peli;
            if (pelinkoko.HasValue)
            {
                peli = new Peli(pelinkoko.Value);
            }
            else
            {
                peli = new Peli();
            }

            peli.Aloittaja = pelaajaList.FirstOrDefault(o => o.YhteysID == haast.YhteysID);
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
            await Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);
            await Clients.Group(peli.Id).SendAsync("Peliin", peli);
            await Clients.All.SendAsync("gamecount", peliList.Count);
        }

        public async Task Aseta(string peliID, int ruutuID)
        {
            bool gameover = false;
            Peli peli;
            lock (_lock)
            {
                var pelaaja = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
                peli = peliList.FirstOrDefault(o => o.Id == peliID);
                var ruutu = peli?.Ruudut.FirstOrDefault(o => o.Id == ruutuID);

                if (peli?.Vuoro.Nimi == pelaaja?.Nimi && ruutu?.Pelaaja == null && peli?.Voittaja == null)
                {
                    ruutu.Pelaaja = pelaaja?.Nimi;
                    if (pelaaja?.Nimi == peli?.Aloittaja.Nimi)
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
                        peli.Aloittaja.Pelaamassa = false;
                        peli.Haastettu.Pelaamassa = false;
                        peliList.Remove(peli);
                    }
                }

            }
            await Clients.Group(peliID).SendAsync("refresh", peli);
            await Clients.All.SendAsync("gamecount", peliList.Count);
            await Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);
        }

        public async void Haasta(Pelaaja haastettava, int pelinKoko)
        {
            if(haastettava.Vastustaja == null)
            {
                var pelaaja = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
                await Clients.Client(haastettava.YhteysID).SendAsync("Haaste", pelaaja, pelinKoko);
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


        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if(pelaajaList != null)
            {
                lock (_lock)
                {
                    var pelaaja = pelaajaList.FirstOrDefault(o => o.YhteysID == Context.ConnectionId);
                    pelaajaList.Remove(pelaaja);
                    foreach(var peli in peliList.Where(o => o.Aloittaja.Nimi == pelaaja.Nimi || o.Haastettu.Nimi == pelaaja.Nimi))
                    {
                        if(peli.Voittaja == null)
                        {
                            peli.Voittaja = peli.Aloittaja.Nimi != pelaaja.Nimi ? peli.Aloittaja : peli.Haastettu;
                            peliList.Remove(peli);
                        }    
                    }
                }
                await Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);
                await Clients.All.SendAsync("gamecount", peliList.Count);
            }
        }

        public bool Voittiko(Peli peli, Pelaaja vuorossa)
        {
            int ruutuKoko = peli.ruutuKoko;
            var grid = peli.Ruudut.Select(r => r.Pelaaja).ToArray();

            // Loop through all cells
            for (int row = 0; row < ruutuKoko; row++)
            {
                for (int col = 0; col < ruutuKoko; col++)
                {
                    if (!string.IsNullOrEmpty(grid[row * ruutuKoko + col]))
                    {
                        // Check in all directions (horizontal, vertical, and diagonals)
                        if (CheckDirection(grid, ruutuKoko, row, col, 0, 1, vuorossa.Nimi) ||  // Horizontal
                            CheckDirection(grid, ruutuKoko, row, col, 1, 0, vuorossa.Nimi) ||  // Vertical
                            CheckDirection(grid, ruutuKoko, row, col, 1, 1, vuorossa.Nimi) ||  // Diagonal \
                            CheckDirection(grid, ruutuKoko, row, col, 1, -1, vuorossa.Nimi))   // Diagonal /
                        {
                            peli.Voittaja = vuorossa;
                            return true;
                        }
                    }
                }
            }

            // Check for a draw
            if (peli.Ruudut.All(r => r.Pelaaja != null))
            {
                peli.Voittaja = null; // Draw
                return true;
            }

            return false;
        }

// Helper to check a direction
        private bool CheckDirection(string[] grid, int size, int startRow, int startCol, int deltaRow, int deltaCol, string player)
        {
            int count = 0;
            int maxCells = Math.Min(5, size); // Limit to 5 cells or grid size, whichever is smaller

            for (int i = 0; i < maxCells; i++)
            {
                int row = startRow + i * deltaRow;
                int col = startCol + i * deltaCol;

                // Check boundaries and match
                if (row >= 0 && row < size && col >= 0 && col < size && grid[row * size + col] == player)
                {
                    count++;
                    if (count == maxCells) return true; // Victory line found
                }
                else
                {
                    break; // Stop if mismatch or out of bounds
                }
            }
            return false;
        }

        
    }
}
