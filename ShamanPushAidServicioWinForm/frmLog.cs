using System;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using JsonRequest.Methods;
using ShamanPushAidServicioWinForm.MapaAndLicenceService;
using System.Data.SqlClient;

namespace ShamanPushAidServicioWinForm
{
    public partial class frmLog : Form
    {
        public frmLog()
        {
            InitializeComponent();
            this.tmrRefresh.Enabled = true;
            this.tmrRefresh_Tick(null, null);
        }

        private void addLog(bool rdo, string logProcedure, string logDescription, bool clear = false)
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
                    this.txtLog.Text = "Log " + DateTime.Now.Date + Environment.NewLine;
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
                if (clear) { this.txtLog.Text = ""; }
                this.txtLog.Text = this.txtLog.Text + DateTime.Now.Hour.ToString("00") + ":" + DateTime.Now.Minute.ToString("00") + "\t" + rdoStr + "\t" + logProcedure + "\t" + logDescription + Environment.NewLine;

            }

        }

        private void tmrRefresh_Tick(object sender, EventArgs e)
        {
            this.tmrRefresh.Enabled = false;

            /*------> Proceso <--------*/
            this.PushAidPreIncidente();

            this.tmrRefresh.Enabled = true;
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
                ConnectionStringCache connectionString = new ConnectionStringCache(ConfigurationManager.AppSettings["ConexionCache"]);
                InterClientesC.AIDServicios objServicios = new InterClientesC.AIDServicios(connectionString);

                List<MensajesPendientes> mensajes = objServicios.GetMensajesPendientes<MensajesPendientes>();
                //mensajes = new List<MensajesPendientes>();
                //mensajes.Add(new MensajesPendientes
                //{
                //    PreIncidenteId = "346046",
                //    MensajeId = "10",
                //    Mensaje = "No hay Novedad"
                //});

                SendMethods jsonSend = new SendMethods();
                if (mensajes != null && mensajes.Count > 0)
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
                            case (int)TipoMensaje.ArriveOrder:
                                result = jsonSend.ArriveOrder(
                                    new ArriveOrder
                                    {
                                        preIncidentId = mensaje.PreIncidenteId,
                                        message = "El movil ya se encuentra en su domicilio"
                                    });
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

        private int GetShamanUserIdByIncidente(int preIncidenteId)
        {
            try
            {
                int userId = 0;
                string queryString = "SELECT ord.UserId " +
                                        "FROM Orders ord " +
                                        "INNER JOIN UsersCompanies usr ON ord.UserId = usr.UserId " +
                                        "INNER JOIN Companies cmp ON usr.CompanyId = cmp.CompanyId " +
                                        "WHERE cmp.Serial = " + ConfigurationManager.AppSettings["Serial"] +
                                        " AND ord.ShamanPreIncidenteId = " + preIncidenteId;
                string connectionString = ConfigurationManager.AppSettings["ConexionGestion"];
                
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(queryString, connection);
                    command.Connection.Open();
                    var res = command.ExecuteScalar();
                    userId = Convert.ToInt32(res);
                }
                return userId;
            }
            catch (Exception ex)
            {
                addLog(false, "PushAidPreIncidente", "Fallo PushAidPreIncidente. " + ex.Message);
            }
            return 0;
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
            Aceptacion = 1, Cancelado, MovilAsignado, Finalizado, ArriveOrder = 10
        }
        #endregion
    }
}
