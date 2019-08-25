using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace WebApplication1.EMAIL
{
    public static class MEmail
    {
        public static async Task Send(string message)
        {
            return;

            MailAddress from = new MailAddress("alert@site.com", "Some name");
            MailAddress to = new MailAddress("Manager@site.ru");
            MailMessage m = new MailMessage(from, to);
            m.Subject = "Новое сообщение на сайте";
            m.Body = message;
            SmtpClient smtp = new SmtpClient("smtp.site.com", 587);
            smtp.Credentials = new NetworkCredential("alert@site.com", "password");
            smtp.EnableSsl = true;
            await smtp.SendMailAsync(m);
        }
    }
}