using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace ShamanPushAidServicioWinForm
{
    public class EmailHelpers
    {
        public static bool Send(List<string> To, string Subject, string Body, List<string> PathFiles, Attachment atachment = null)
        {
            //Logger log = LogManager.GetCurrentClassLogger();

            if(To.Count > 0)
            //if (!string.IsNullOrEmpty(To) && new MailAddress(To).Address == To)
            {
                //log.Info("Preparando para el envio a: " + To);
                
                if (ConfigurationManager.AppSettings.Count > 0)
                {
                    //Preparo el cliente SMTP
                    SmtpClient smtpParamedic = new SmtpClient();

                    smtpParamedic.Host = ConfigurationManager.AppSettings["MailServer"];
                    smtpParamedic.Port = int.Parse(ConfigurationManager.AppSettings["MailPort"]);
                    smtpParamedic.EnableSsl = bool.Parse(ConfigurationManager.AppSettings["MailSSL"]);
                    smtpParamedic.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtpParamedic.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["MailAddress"], ConfigurationManager.AppSettings["MailPassword"]);

                    //Preparo el EMAIL
                    string FromAdrress = ConfigurationManager.AppSettings["MailAddress"];
                    string FromName = ConfigurationManager.AppSettings["MailFrom"];
                    MailMessage eMail = new MailMessage();
                    foreach (var item in To)
                    {
                        if (!string.IsNullOrEmpty(item) && new MailAddress(item).Address == item)
                            eMail.To.Add(new MailAddress(item));
                    }
                    
                    eMail.From = new MailAddress(FromAdrress, FromName, Encoding.UTF8);
                    eMail.Subject = Subject;
                    eMail.SubjectEncoding = Encoding.UTF8;
                    eMail.Body = Body;
                    eMail.BodyEncoding = Encoding.UTF8;
                    eMail.IsBodyHtml = true;
                    eMail.Priority = MailPriority.High;

                    //Adjunto los archivos que son de los comprobantes
                    if (PathFiles != null)
                    {
                        foreach (string item in PathFiles)
                            eMail.Attachments.Add(new Attachment(item));
                    }
                    else if (atachment != null)
                        eMail.Attachments.Add(atachment);


                    //Envio de Email
                    try
                    {
                        //log.Info("Enviando email");
                        smtpParamedic.Send(eMail);
                        //log.Info("Envio OK");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        string mensaje = "Error enviando email de " + To + " - REF: " + ex.Message;
                        //log.Error(mensaje);
                    }
                }
            }
            return false;
        }
    }
}
