using System;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Json;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Net.Mail;
using JsonRequest.Methods;
using ShamanPushAidServicio.MapaAndLicenceService;

namespace ShamanPushAidServicio
{
    public partial class Service1 : ServiceBase
    {
        Timer t = new Timer();
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            t.Elapsed += delegate { ElapsedHandler(); };
            t.Interval = Convert.ToDouble(ConfigurationManager.AppSettings["TimerInterval"]);
            t.Start();
        }

        protected override void OnPause()
        {
            t.Stop();
        }

        protected override void OnContinue()
        {
            t.Start();
        }

        protected override void OnStop()
        {
            t.Stop();
        }

        public void ElapsedHandler()
        {
            ///*------> Conecto a DB <---------*/
            //if (this.setConexionDB())
            //{
            /*------> Proceso <--------*/
            this.PushAidPreIncidente();
            //}
        }

        private void addLog(bool rdo, string logProcedure, string logDescription)
        {

            string path;

            path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            path = path + "\\" + modFechasCs.DateToSql(DateTime.Now).Replace("-", "_") + ".log";

            if (!File.Exists(path))
            {
                // Create a file to write to.
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine("Log " + DateTime.Now.Date);
                }
            }

            using (StreamWriter sw = File.AppendText(path))
            {
                string rdoStr = "Ok";
                if (rdo == false)
                {
                    rdoStr = "Error";
                }
                sw.WriteLine(DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00") + "\t" + rdoStr + "\t" + logProcedure + "\t" + logDescription);
            }
        }

        #region proceso

        /// <summary>
        /// Envia notificaciones a Shaman AID.
        //  Ejecuta InterClientesC.AIDServicios.GetMensajesPendientes (InterClientesC.GetMensajesPendientes())
        //  Armar un Json con los datos, dependiendo del tipo de mensaje (1...5)
        //  (para los asignados (3) hay que calcular el tiempo con http://200.49.156.125:57779/Service.asmx?op=GetDistanciaTiempo)
        //  Eliminar el mensaje pendiente de Shaman con InterClientesC.AIDServicios.SetPreIncidenteMensaje())
        /// </summary>
        public void PushAidPreIncidente()
        {
            try
            {
                InterClientesC.AIDServicios objServicios = new InterClientesC.AIDServicios();

                List<MensajesPendientes> mensajes = objServicios.GetMensajesPendientes<MensajesPendientes>();
                SendMethods jsonSend = new SendMethods();
                if (mensajes.Count > 0)
                {
                    addLog(true, "PushAidPreIncidente: ", string.Format("Se encontraron {0} mensajes nuevos para enviar.", mensajes.Count));
                    foreach (var mensaje in mensajes)
                    {
                        bool result = false;
                        ////TODO: Eliminar
                        //mensaje.PreIncidenteId = 16;
                        //mensaje.movLatitud = "-34.4381152";
                        //mensaje.movLongitud = "-58.8057913";
                        //mensaje.domLatitud = "-34.8008554";
                        //mensaje.domLongitud = "-58.447388";

                        switch (Convert.ToInt32(mensaje.MensajeId))
                        {
                            case (int)TipoMensaje.Aceptacion:
                                result = jsonSend.ConfirmOrder(new Order { preIncidentId = mensaje.PreIncidenteId, message = mensaje.Mensaje, NroServicio = mensaje.NroIncidente });
                                break;
                            case (int)TipoMensaje.Cancelado:
                                result = jsonSend.CancelOrder(new CancelOrder { preIncidentId = mensaje.PreIncidenteId, message = mensaje.Mensaje });
                                break;
                            case (int)TipoMensaje.MovilAsignado:
                                result = jsonSend.OrderMobileAssigned(
                                    new OrderMobileAssigned
                                    {
                                        preIncidentId = mensaje.PreIncidenteId,
                                        mobileNumber = mensaje.MovilId,
                                        doctor = mensaje.doctor,
                                        nurse = mensaje.nurse,
                                        driver = mensaje.driver,
                                        estimatedTime = GetEstimatedTime(mensaje),
                                        message = mensaje.Mensaje
                                    });
                                break;
                            case (int)TipoMensaje.Finalizado:
                                result = jsonSend.CompleteOrder(
                                    new CompleteOrder
                                    {
                                        preIncidentId = mensaje.PreIncidenteId,
                                        diagnostic = mensaje.diagnostic,
                                        treatment = mensaje.treatment
                                    });
                                break;
                            case (int)TipoMensaje.PushText:
                                result = jsonSend.PushText(new PushText());
                                break;
                            default:
                                break;
                        }
                        if (result)
                            objServicios.SetPreIncidenteMensaje(mensaje.PreIncidenteId, mensaje.MensajeId);
                    }
                }
                else
                    addLog(true, "PushAidPreIncidente: ", "No se encontraron mensajes nuevos para enviar.");
            }
            catch (Exception ex)
            {
                addLog(false, "PushAidPreIncidente", "Fallo PushAidPreIncidente. " + ex.Message);
            }
        }

        private string GetEstimatedTime(MensajesPendientes mensaje)
        {
            ServiceSoapClient serviceSoapClient = new ServiceSoapClient();
            return GetTime(serviceSoapClient.GetDistanciaTiempo(mensaje.movLatitud, mensaje.movLongitud, mensaje.domLatitud, mensaje.domLongitud));
        }

        private string GetTime(string vDisTpo)
        {
            string vTpo = "00:00";
            try
            {
                string[] vTpoExp = vDisTpo.Split('/')[1].Split(' ');

                if (vTpoExp.Length > 1)
                    if (vTpoExp[1].Substring(0, 1).ToLower() == "h")
                    {
                        vTpo = double.Parse(vTpoExp[0]).ToString("00:");
                        if (vTpoExp.Length > 2)
                            vTpo = vTpo + double.Parse(vTpoExp[2]).ToString("00");
                    }
                    else
                        vTpo = "00:" + double.Parse(vTpoExp[0]).ToString("00");

                return vTpo;
            }
            catch (Exception ex)
            {
                addLog(false, "GetTime", "Fallo GetTime. " + ex.Message);
            }
            return vTpo;
        }

        enum TipoMensaje
        {
            Aceptacion = 1, Cancelado, MovilAsignado, Finalizado, PushText = 10
        }
        #endregion
    }


}
