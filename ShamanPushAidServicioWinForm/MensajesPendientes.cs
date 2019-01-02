using System;

namespace ShamanPushAidServicioWinForm
{
    public class MensajesPendientes
    {
        public string MensajeId { get; set; }
        public int PreIncidenteId { get; set; }
        public string NroIncidente { get; set; }
        public string MovilId { get; set; }
        public string doctor { get; set; }
        public string nurse { get; set; }
        public string driver { get; set; }
        public string domLatitud { get; set; }
        public string domLongitud { get; set; }
        public string movLatitud { get; set; }
        public string movLongitud { get; set; }
        public string diagnostic { get; set; }
        public string treatment { get; set; }
        public string Mensaje { get; set; }
    }
}
