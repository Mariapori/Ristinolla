using Microsoft.AspNetCore.SignalR;

namespace Ristinolla.Data
{
    public class Pelaaja
    {
        public string YhteysID { get; set; }
        public string Nimi { get; set; }
        public bool Pelaamassa { get; set; }
        public bool OdottaaVuoroa { get; set; }
    }
    public class GameService : Hub
    {
        private List<Pelaaja> pelaajaList;
        private object _lock = new object();

        public void RekisteroiYhteys(string nimi)
        {
            lock (_lock)
            {
                Pelaaja pelaaja = new Pelaaja { Nimi = nimi, Pelaamassa = false, OdottaaVuoroa = false, YhteysID = Context.ConnectionId };
                if(pelaajaList is not null)
                {
                    pelaajaList.Add(pelaaja);
                }
                else
                {
                    pelaajaList = new List<Pelaaja>();
                    pelaajaList.Add(pelaaja);
                }
                Clients.All.SendAsync("UusiPelaaja", pelaaja);
            }
        }
        public void Haaste(Pelaaja haastettava)
        {
            Clients.Client(haastettava.YhteysID).SendAsync("OtaHaaste", Context.ConnectionId);
        }
          
    }
}
