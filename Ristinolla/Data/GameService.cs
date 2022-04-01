using Microsoft.AspNetCore.SignalR;
using System.Collections.ObjectModel;

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
    public class GameService : Hub
    {
        private ObservableCollection<Pelaaja> pelaajaList;
        private object _lock = new object();

        public async void RekisteroiYhteys(string nimi)
        {
            lock (_lock)
            {
                Pelaaja pelaaja = new Pelaaja { Nimi = nimi, Pelaamassa = false, OdottaaVuoroa = false, YhteysID = Context.ConnectionId, Vastustaja = null };
                if (pelaajaList is not null)
                {
                    pelaajaList.Add(pelaaja);
                }
                else
                {
                    pelaajaList = new ObservableCollection<Pelaaja>();
                    pelaajaList.Add(pelaaja);
                }
            }
            await Clients.Caller.SendAsync("rekisteroity", Context.ConnectionId);
            await Clients.All.SendAsync("pelaajaPaivitys", pelaajaList);

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
    }
}
